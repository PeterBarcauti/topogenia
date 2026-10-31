// PackageScopeTests.cs
// What the package leaves out (spec 7.2): the trash, the operation log, the deletion batches, every database identifier;
// and the people: authors by login, or no login at all when the export leaves out the people (spec 5.4, AR-34).

using System.Text;
using System.Text.RegularExpressions;
using Topogenia.Domain.Model;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed partial class PackageScopeTests
{
    /// <summary>An identifier written with its dashes, or as 32 hexadecimal digits alone (a SHA-256 has 64).</summary>
    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|(?<![0-9a-fA-F])[0-9a-fA-F]{32}(?![0-9a-fA-F])")]
    private static partial Regex AnyGuid();

    private static string AllEntries(SampleProject sample, bool withoutPeople) =>
        string.Join('\n', PackageWriter.Entries(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople))
            .Select(entry => Encoding.UTF8.GetString(entry.Content)));

    [Theory]
    [InlineData("CONGES-E015")]
    [InlineData("Archiver le papier")]
    [InlineData("CONGES-C07")]
    [InlineData("Prestataire")]
    [InlineData("Bordereau d'archive")]
    [InlineData("Archiviste")]
    [InlineData("CONGES-I004")]
    [InlineData("Archive the paper")]
    [InlineData("archive")]
    [InlineData("CONGES-E016")]
    [InlineData("step.create")]
    public void Nothing_of_the_trash_of_a_closed_frame_or_of_the_operation_log_is_exported(string text)
    {
        var sample = new SampleProject();

        Assert.DoesNotContain(text, AllEntries(sample, withoutPeople: false), StringComparison.Ordinal);
    }

    [Fact]
    public void The_package_carries_no_identifier_of_the_database_so_no_deletion_batch_either()
    {
        var sample = new SampleProject();
        var package = PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: false),
            new PackageStamp(SampleProject.At(300), "EXEMPLE\\alice"));
        var files = new PackageFiles(package);

        Assert.All(files.Paths, path => Assert.DoesNotMatch(AnyGuid(), files.Text(path)));
        Assert.NotEmpty(sample.Records.OfType<DeletionBatch>());
    }

    [Fact]
    public void With_the_people_the_authors_are_exported_by_login_former_members_included()
    {
        var text = AllEntries(new SampleProject(), withoutPeople: false);

        foreach (var login in new[] { "alice", "bruno", "chloe", "denis" })
            Assert.Contains("\"EXEMPLE\\\\" + login + "\"", text, StringComparison.Ordinal);
        foreach (var property in new[] { "createdBy", "updatedBy", "validatedBy", "changedBy", "actor", "assignee" })
            Assert.Contains("\"" + property + "\": \"EXEMPLE\\\\", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_the_people_no_login_no_member_and_no_person_of_a_lane_or_a_step_is_written_but_the_dates_are()
    {
        var sample = new SampleProject();
        var entries = PackageWriter.Entries(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: true));
        var text = string.Join('\n', entries.Select(entry => Encoding.UTF8.GetString(entry.Content)));

        Assert.DoesNotContain("EXEMPLE", text, StringComparison.Ordinal);
        Assert.DoesNotContain(entries, entry => entry.Path == "members.json");
        // "actor" stays a field of the sheets (a text); the actor of a lane, a login, is checked by the absence of any login.
        foreach (var property in new[] { "createdBy", "updatedBy", "validatedBy", "changedBy", "assignee", "login", "displayName", "exportedBy" })
            Assert.DoesNotContain("\"" + property + "\"", text, StringComparison.Ordinal);
        Assert.Contains("\"createdAt\": \"2026-09-01T07:30:00.0000000Z\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice Martin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_users_named_by_an_export_are_exactly_those_whose_login_it_needs()
    {
        var sample = new SampleProject();

        var named = PackageWriter.UsersNamedBy(sample.Workspace());

        Assert.Equal(new[] { sample.Alice, sample.Bruno, sample.Chloe, sample.Denis }.Order(), named.Order());
    }

    [Fact]
    public void A_user_named_by_the_project_without_a_login_given_is_a_fault_of_the_caller()
    {
        var sample = new SampleProject();
        var logins = sample.Logins.Where(pair => pair.Key != sample.Denis).ToDictionary();

        var failure = Assert.Throws<InvalidOperationException>(() => PackageWriter.Entries(sample.Workspace(), logins, new PackageOptions(WithoutPeople: false)));

        Assert.Contains(sample.Denis.ToString(), failure.Message, StringComparison.Ordinal);
        Assert.NotEmpty(PackageWriter.Entries(sample.Workspace(), logins, new PackageOptions(WithoutPeople: true)));
    }
}
