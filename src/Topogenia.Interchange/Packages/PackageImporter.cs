// PackageImporter.cs
// Adapter of the port of the import: the guards of PackageReader, then a trial rebuild that resolves every reference and counts.

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// <see cref="IPackageImporter"/> on <see cref="PackageReader"/> and <see cref="PackageBuild"/> (ADR 052), with the chain
/// of converters that brings an earlier format up to the current one (ADR 053).
/// </summary>
public sealed class PackageImporter(PackageLimits limits, PackageChain chain) : IPackageImporter
{
    /// <summary>The importer of the application, with the limits of spec 5.4 and the chain of the application.</summary>
    public PackageImporter()
        : this(PackageLimits.Default)
    {
    }

    /// <summary>An importer with other limits, which a test gives to reach a limit, and the chain of the application.</summary>
    public PackageImporter(PackageLimits limits)
        : this(limits, PackageChain.Application)
    {
    }

    /// <summary>
    /// The guards, then a trial rebuild under the code of the package, with a placeholder user for every login: a
    /// reference that names nothing or a login that is not one refuses the package already at the preview, and the
    /// counts are those of the records.
    /// </summary>
    public IReadPackage Read(byte[] content)
    {
        var entries = PackageReader.Read(content, limits, chain);
        var code = entries.Manifest["projectCode"]!.GetValue<string>();
        var logins = new Dictionary<string, (string Login, Guid Id)>(StringComparer.Ordinal);
        var trial = new PackageBuild(entries, new OperationContext(Guid.Empty, DateTime.UnixEpoch, "en", AppSettings.Default), code, login =>
        {
            // One placeholder per login, case ignored, as the users of the import: a member named twice is refused here too.
            // A login that no user could have (a space around it, a control character...) is never looked up in the database.
            if (!Login.TryParse(login, out _))
                throw ImportedRecords.Refusal(["login"]);
            var normalised = Login.Normalize(login);
            if (!logins.TryGetValue(normalised, out var known))
                logins[normalised] = known = (login, Guid.CreateVersion7());
            return known.Id;
        }, withMembers: true);
        trial.Run();
        var counts = new PackageCounts(trial.Count<ProcessMap>(), trial.Count<Lane>(), trial.LiveSteps, trial.Count<Flow>(), trial.Count<StepSheet>(),
            trial.Count<Document>(), trial.Count<PainPoint>(), trial.Count<Translation>());
        return new ReadPackage(entries, code, [.. logins.Values.Select(item => item.Login)], Members(entries), counts);
    }

    public IReadOnlyList<IRecord> Build(IReadPackage package, ImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (package is not ReadPackage read)
            throw new ArgumentException("The package was not read by this importer.", nameof(package));
        var code = ProjectCode.Parse(plan.ProjectCode).Value;
        return new PackageBuild(read.Entries, plan.Context, code, plan.User, plan.WithMembers).Run();
    }

    private static List<PackageMember> Members(PackageEntries entries)
    {
        if (!entries.Content.TryGetValue(PackageFormat.Members, out var members))
            return [];
        var json = new ImportJson(PackageFormat.Members);
        return [.. json.Items(members, "members").Select(node => new PackageMember(json.Text(node, "login"), json.Text(node, "displayName")))];
    }

    private sealed class ReadPackage(PackageEntries entries, string projectCode, IReadOnlyList<string> logins, IReadOnlyList<PackageMember> members,
        PackageCounts counts) : IReadPackage
    {
        public PackageEntries Entries { get; } = entries;

        public string ProjectCode { get; } = projectCode;

        public bool WithPeople => Entries.WithPeople;

        public IReadOnlyList<string> Logins { get; } = logins;

        public IReadOnlyList<PackageMember> Members { get; } = members;

        public PackageCounts Counts { get; } = counts;
    }
}
