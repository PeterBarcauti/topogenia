// CommandCatalogTests.cs
// Every command and query of the application declares the right of its 1.x route, read from one table (annex A 1, A-4-06, A-6-27).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Queries;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Commands;

public sealed class CommandCatalogTests
{
    /// <summary>
    /// The right of each command and query, as the route table of annex A 1 gives it (Lecteur, Contributeur, Animateur,
    /// « Rempl. », admin): every static table of <see cref="CommandCatalog"/>, one file per feature
    /// (`CommandCatalog.Feature.cs`). A new command is added there with the right of its route, or this test fails.
    /// </summary>
    private static readonly Dictionary<Type, string> Rights = typeof(CommandCatalog)
        .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
        .Where(field => field.FieldType == typeof(Dictionary<Type, string>))
        .SelectMany(field => (Dictionary<Type, string>)field.GetValue(null)!)
        .ToDictionary();

    private static IEnumerable<Type> Commands() =>
        typeof(CommandRunner).Assembly.GetTypes().Where(type => !type.IsAbstract && IsCommand(type));

    /// <summary>A command or a query of a project: both declare the right of their route.</summary>
    private static bool IsCommand(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition
                && (definition == typeof(ProjectCommand<>) || definition == typeof(ProjectQuery<>)))
                return true;
        }
        return false;
    }

    private static string Describe(AccessRequirementAttribute requirement) => requirement switch
    {
        RequiresRoleAttribute { Minimum: var minimum } => Codes.Of(minimum),
        RequiresSheetFillAttribute => "sheet_fill",
        RequiresAdminAttribute => "admin",
        _ => requirement.GetType().Name,
    };

    [Fact]
    public void Every_command_is_in_the_catalog_and_the_catalog_names_only_commands() =>
        Assert.Equal(Rights.Keys.Select(type => type.FullName).Order(StringComparer.Ordinal),
            Commands().Select(type => type.FullName).Order(StringComparer.Ordinal));

    [Fact]
    public void Every_command_declares_the_right_of_its_route() =>
        Assert.All(Commands(), type => Assert.Equal(Rights[type], Describe(AccessPolicy.RequirementOf(type))));
}
