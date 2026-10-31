// ExchangeCommands.cs
// The commands --export and --import of the executable (spec 6.3, ADR 055): one package written or read, under the rights of the account that runs them.

using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Projects;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Api.Exchange;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Errors;
using Topogenia.Host.Users;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Host.Startup;

/// <summary>
/// The operands and options of <c>--export &lt;code&gt; &lt;file&gt;</c> and <c>--import &lt;file&gt;</c>, as written: the
/// options about the people are required, never a silent default (spec 5.4); <c>--code</c> gives the code of a new project
/// when the code of the package is taken.
/// </summary>
public sealed record ExchangeArguments(string Command, IReadOnlyList<string> Operands, IReadOnlyList<string> Options, IReadOnlyList<string> Codes);

public static partial class HostCommands
{
    public const string Export = "--export";

    public const string Import = "--import";

    public const string WithPeople = "--with-people";

    public const string WithoutPeople = "--without-people";

    public const string WithMembers = "--with-members";

    public const string WithoutMembers = "--without-members";

    public const string CodeOption = "--code";

    private static readonly string[] ExchangeFlags = [WithPeople, WithoutPeople, WithMembers, WithoutMembers];

    /// <summary>
    /// Takes the arguments of an exchange command out of <paramref name="args"/>: the operands that follow the command (two
    /// for <c>--export</c>, one for <c>--import</c>), the flags about the people and each <c>--code</c> with its value; the
    /// other arguments are left, for the configuration.
    /// </summary>
    internal static (ExchangeArguments? Exchange, List<string> Others) SplitExchange(string[] args)
    {
        var others = new List<string>();
        string? command = null;
        var operands = new List<string>();
        var options = new List<string>();
        var codes = new List<string>();
        var expected = 0;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, Export, StringComparison.OrdinalIgnoreCase) || string.Equals(arg, Import, StringComparison.OrdinalIgnoreCase))
            {
                command = arg.ToLowerInvariant();
                expected = command == Export ? 2 : 1;
            }
            else if (expected > 0 && !arg.StartsWith('-'))
            {
                operands.Add(arg);
                expected--;
            }
            else if (ExchangeFlags.Contains(arg, StringComparer.OrdinalIgnoreCase))
                options.Add(arg.ToLowerInvariant());
            else if (string.Equals(arg, CodeOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                codes.Add(args[++i]);
            else
                others.Add(arg);
        }
        return (command is null ? null : new ExchangeArguments(command, operands, options, codes), others);
    }

    /// <summary>The answer of the flags to a question, null when they are absent, given twice or both ways.</summary>
    private static bool? Answer(ExchangeArguments arguments, string yes, string no)
    {
        var given = arguments.Options.Where(option => option == yes || option == no).ToList();
        return given.Count == 1 ? given[0] == yes : null;
    }

    /// <summary>True when the arguments are those of the command: its operands, its question answered once, no option of the other.</summary>
    private static bool IsWellFormed(ExchangeArguments arguments, bool? answer)
    {
        var exporting = arguments.Command == Export;
        string[] foreign = exporting ? [WithMembers, WithoutMembers] : [WithPeople, WithoutPeople];
        return answer is not null && arguments.Operands.Count == (exporting ? 2 : 1) && !arguments.Options.Any(foreign.Contains)
            && arguments.Codes.Count <= (exporting ? 0 : 1);
    }

    private static async Task<int> ExchangeAsync(IServiceProvider services, ProductOptions options, ExchangeArguments arguments, StartupConsole console,
        ILogger logger, CancellationToken cancellationToken)
    {
        var exporting = arguments.Command == Export;
        var answer = exporting ? Answer(arguments, WithPeople, WithoutPeople) : Answer(arguments, WithMembers, WithoutMembers);
        if (!IsWellFormed(arguments, answer))
        {
            console.Write(exporting ? "USAGE_EXPORT" : "USAGE_IMPORT");
            return Usage;
        }
        if (options.Mode == HostMode.Server)
            return await ServerProblemAsync(services, options, console, cancellationToken) ? Problem
                : await RunExchangeAsync(services, new ProcessAccount(), arguments, answer!.Value, console, logger, cancellationToken);
        return await DesktopExchangeAsync(services, arguments, answer!.Value, console, logger, cancellationToken);
    }

    /// <summary>
    /// The desktop data file must exist, be held by no running application nor other program (the lock of the instance is
    /// taken for the command), and be up to date: the start migrates it after its backup. The engine is closed after, its
    /// log written back.
    /// </summary>
    private static async Task<int> DesktopExchangeAsync(IServiceProvider services, ExchangeArguments arguments, bool answer, StartupConsole console,
        ILogger logger, CancellationToken cancellationToken)
    {
        var paths = services.GetRequiredService<DesktopPaths>();
        if (DesktopDataFile.Check(paths.DataFile, services.GetRequiredService<DataFileEnvironment>()) is { } refusal)
        {
            console.Write(refusal.Code, new Dictionary<string, object?> { ["place"] = refusal.Place });
            return Problem;
        }
        if (!File.Exists(paths.DataFile))
        {
            console.Write("COMMANDE_FICHIER_ABSENT");
            return Problem;
        }
        var instance = services.GetRequiredService<DesktopInstance>();
        if (!instance.TryAcquire() || DataFileUse.HeldOpen(paths.DataFile) is not null)
        {
            console.Write("COMMANDE_FICHIER_OUVERT");
            return Problem;
        }
        try
        {
            var state = await services.GetRequiredService<ISchemaMigrations>().CheckAsync(cancellationToken);
            if (state.Status == SchemaStatus.Behind)
            {
                console.Write("COMMANDE_FICHIER_A_MIGRER");
                return Problem;
            }
            return await RunExchangeAsync(services, services.GetRequiredService<IProcessAccount>(), arguments, answer, console, logger, cancellationToken);
        }
        catch (SchemaRefusedException refused)
        {
            LogRefused(logger, refused.Code, refused.Migration ?? "", "");
            console.Write(refused.Code, refused.Migration is null ? null : new Dictionary<string, object?> { ["migration"] = refused.Migration });
            return Problem;
        }
        finally
        {
            await CloseAsync(services.GetRequiredService<SqliteEngine>(), logger, cancellationToken);
            instance.Release();
        }
    }

    /// <summary>
    /// The account that runs the command, signed in as at a first request (`UserRunner.SignInAsync`): its rights are those of
    /// the application, and its export or import is in the audit trail as through the API. A refusal of the application is
    /// written with its message in the language of the console.
    /// </summary>
    private static async Task<int> RunExchangeAsync(IServiceProvider services, IProcessAccount account, ExchangeArguments arguments, bool answer,
        StartupConsole console, ILogger logger, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        try
        {
            provider.GetRequiredService<RequestUser>().User = await provider.GetRequiredService<UserRunner>().SignInAsync(account.Login, null, cancellationToken);
            return arguments.Command == Export
                ? await ExportAsync(provider, arguments.Operands[0], arguments.Operands[1], withoutPeople: !answer, console, cancellationToken)
                : await ImportAsync(provider, arguments.Operands[0], arguments.Codes.FirstOrDefault(), withMembers: answer, console, cancellationToken);
        }
        catch (DomainException refusal)
        {
            LogExchangeRefused(logger, arguments.Command, refusal.Code);
            console.WriteText(services.GetRequiredService<ErrorMessages>().Format(refusal.Code, console.Language, refusal.Details));
            return Problem;
        }
    }

    /// <summary>
    /// Exports the live project of <paramref name="code"/> that the account may export (its facilitator, an administrator,
    /// AR-113), a malformed code refused PROJET_CODE_INVALIDE: the package is written whole or not at all (a file of a
    /// name of its own next to its place, removed when the writing fails, then renamed), an existing file is never replaced.
    /// </summary>
    private static async Task<int> ExportAsync(IServiceProvider services, string code, string file, bool withoutPeople, StartupConsole console,
        CancellationToken cancellationToken)
    {
        var target = Path.GetFullPath(file);
        if (File.Exists(target))
        {
            console.Write("EXPORT_FICHIER_EXISTANT", new Dictionary<string, object?> { ["file"] = target });
            return Problem;
        }
        var wanted = ProjectCode.Parse(code).Value;
        var projects = await services.GetRequiredService<UserRunner>().RunAsync(new ListProjectsQuery(), cancellationToken);
        var project = projects.FirstOrDefault(item => string.Equals(item.Code, wanted, StringComparison.Ordinal)) ?? throw DomainException.NotFound();
        var package = await services.GetRequiredService<PackageExport>().RunAsync(project.Id, withoutPeople, cancellationToken);
        var partial = Path.Combine(Path.GetDirectoryName(target)!, $"{Path.GetFileName(target)}.{Path.GetRandomFileName()}.part");
        try
        {
            await File.WriteAllBytesAsync(partial, package.Content, cancellationToken);
            File.Move(partial, target);
        }
        finally
        {
            File.Delete(partial);
        }
        console.Write("EXPORT_FAIT", new Dictionary<string, object?> { ["code"] = project.Code, ["file"] = target });
        return Success;
    }

    /// <summary>Imports a package as a new project (an administrator), under its code or <paramref name="code"/>, the members taken or not.</summary>
    private static async Task<int> ImportAsync(IServiceProvider services, string file, string? code, bool withMembers, StartupConsole console,
        CancellationToken cancellationToken)
    {
        var source = new FileInfo(Path.GetFullPath(file));
        if (!source.Exists)
        {
            console.Write("IMPORT_FICHIER_INTROUVABLE", new Dictionary<string, object?> { ["file"] = source.FullName });
            return Problem;
        }
        if (source.Length > ExchangeApi.PackageLimit)
            throw new DomainException(ErrorCodes.RequestTooLarge, RefusalKind.TooLarge,
                new Dictionary<string, object?> { ["max"] = ExchangeApi.PackageLimit / (1024 * 1024) });
        var content = await File.ReadAllBytesAsync(source.FullName, cancellationToken);
        var imported = await services.GetRequiredService<PackageImport>().ImportAsync(content, code, withMembers, cancellationToken);
        console.Write("IMPORT_FAIT", new Dictionary<string, object?> { ["code"] = imported.Code });
        return Success;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command {Command} refused: {Code}.")]
    private static partial void LogExchangeRefused(ILogger logger, string command, string code);
}
