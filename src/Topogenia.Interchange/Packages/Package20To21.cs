// Package20To21.cs
// The link of the chain from the format 2.0 to the format 2.1: the work time of the project, by default (spec 14.7, ADR 085).

using System.Text.Json.Nodes;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;
using Topogenia.Interchange.Schemas;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The format 2.1 adds the entries of the value stream (ADR 085). All of them are optional but the work time of the
/// project, which a package 2.0 does not carry: it receives the work time by default (a working day of 7 hours, 151.67
/// hours of a full-time equivalent in a month), as the projects of a 2.0 database do. Nothing else changes.
/// </summary>
public sealed class Package20To21 : IPackageConverter
{
    public const string Version = "2.0";

    /// <summary>The format 2.0: its entries, which the 2.1 keeps, and its own schema, kept embedded.</summary>
    public PackageDefinition From { get; } = new(Version, PackageFormat.Entries, PackageSchema.For(Version));

    public string To => "2.1";

    public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content[PackageFormat.Project]["project"] is not JsonObject project)
            throw new InvalidOperationException("A package checked against the schema 2.0 has its project.");
        project["workdayHours"] = CanonicalJson.Number(WorkTime.DefaultWorkdayHours);
        project["fteHoursPerMonth"] = CanonicalJson.Number(WorkTime.DefaultFteHoursPerMonth);
    }
}
