// TextSearchParity.cs
// The text functions of a query that both engines would not answer alike are refused on both, before any SQL (ADR 038).

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Topogenia.Persistence.Model;

/// <summary>
/// The translation of the text functions of a query, the same on both engines (ADR 038). A function whose SQL gives
/// other rows on each engine is refused on both when the query is compiled (<see cref="InvalidOperationException"/>,
/// a defect of the caller, like a refused text): `ToUpper` and `ToLower` (SQLite folds ASCII letters only),
/// `EF.Functions.Like` (character classes `[a-z]` in SQL Server only, trailing spaces ignored in a `varchar`), `IndexOf`,
/// `Substring` and `Length` (SQLite counts code points and stops at a NUL character, SQL Server counts UTF-16 code units).
/// A provider adds its own translators (<see cref="UseTextSearchParity{TContext}"/>): SQLite translates `Contains` so that
/// it takes the same searched texts as SQL Server. `StartsWith`, `EndsWith` and, in SQL Server, `Contains` are translated
/// by the provider before any translator; they compare alike once `LIKE` is case-sensitive in SQLite (its settings).
/// </summary>
public static class TextSearchParity
{
    /// <summary>The methods refused in a query, by name, on <see cref="string"/>.</summary>
    private static readonly HashSet<string> RefusedStringMethods = new(StringComparer.Ordinal)
    {
        nameof(string.ToUpper),
        nameof(string.ToLower),
        nameof(string.ToUpperInvariant),
        nameof(string.ToLowerInvariant),
        nameof(string.IndexOf),
        nameof(string.Substring),
    };

    /// <summary>Registers the refusals of the text functions and the translators of the provider, if any.</summary>
    public static DbContextOptionsBuilder<TContext> UseTextSearchParity<TContext>(this DbContextOptionsBuilder<TContext> builder,
        Type? providerTranslators = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new Extension(providerTranslators));
        return builder;
    }

    /// <summary>True for a method of a query that both engines would not translate alike.</summary>
    public static bool IsRefused(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return (method.DeclaringType == typeof(string) && RefusedStringMethods.Contains(method.Name))
            || (method.DeclaringType == typeof(DbFunctionsExtensions) && method.Name == nameof(DbFunctionsExtensions.Like));
    }

    /// <summary>True for a member of a query that both engines would not translate alike: the length of a text.</summary>
    public static bool IsRefused(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.DeclaringType == typeof(string) && member.Name == nameof(string.Length);
    }

    private static InvalidOperationException Refusal(string name) =>
        new($"The text function {name} is not translated alike by both engines and is refused in a query (ADR 038).");

    /// <summary>Refuses the methods and members of <see cref="IsRefused(MethodInfo)"/>; any other is left to the provider.</summary>
    private sealed class Refusals : IMethodCallTranslatorPlugin, IMemberTranslatorPlugin, IMethodCallTranslator, IMemberTranslator
    {
        public static Refusals Instance { get; } = new();

        IEnumerable<IMethodCallTranslator> IMethodCallTranslatorPlugin.Translators => [this];

        IEnumerable<IMemberTranslator> IMemberTranslatorPlugin.Translators => [this];

        public SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger) =>
            IsRefused(method) ? throw Refusal($"{method.DeclaringType?.Name}.{method.Name}") : null;

        public SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger) =>
            IsRefused(member) ? throw Refusal($"{member.DeclaringType?.Name}.{member.Name}") : null;
    }

    /// <summary>The services of <see cref="UseTextSearchParity{TContext}"/>: the refusals, then the translators of the provider.</summary>
    private sealed class Extension(Type? providerTranslators) : IDbContextOptionsExtension
    {
        public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this, providerTranslators);

        public void ApplyServices(IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMethodCallTranslatorPlugin>(Refusals.Instance));
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMemberTranslatorPlugin>(Refusals.Instance));
            if (providerTranslators is not null)
                services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IMethodCallTranslatorPlugin), providerTranslators));
        }

        public void Validate(IDbContextOptions options)
        {
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension, Type? providerTranslators) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        private Type? Translators => providerTranslators;

        public override string LogFragment => "TextSearchParity ";

        public override int GetServiceProviderHashCode() => providerTranslators?.GetHashCode() ?? 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo info && info.Translators == providerTranslators;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            ArgumentNullException.ThrowIfNull(debugInfo);
            debugInfo["TextSearchParity"] = providerTranslators?.Name ?? string.Empty;
        }
    }
}
