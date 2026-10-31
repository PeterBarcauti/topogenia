// SampleCoverageTests.cs
// The fictional project of the exchange tests uses every function its round trips must prove (spec 7.4, S-7.4-02, ADR 055).

using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

/// <summary>
/// The project of `samples/conges.topogenia` holds each value of the closed lists that change the structure or a rule (step
/// types, lane types, automation, RACI letters, sheet states, document directions and links, roles of the members, objects
/// and sources of the translations) and at least one value of the others, and each kind of record the package carries.
/// </summary>
public sealed class SampleCoverageTests
{
    private static readonly IReadOnlyList<IRecord> Records = new SampleProject().Records;

    private static IEnumerable<T> Live<T>()
        where T : IRecord => Records.OfType<T>().Where(record => record is not ISoftDeletable { DeletedAt: not null });

    [Fact]
    public void The_sample_holds_every_value_of_the_closed_lists_of_the_structure_and_of_the_rules()
    {
        Assert.Equal(Enum.GetValues<StepType>(), Live<Step>().Select(step => step.StepType).Distinct().Order());
        Assert.Equal(Enum.GetValues<LaneType>(), Live<Lane>().Select(lane => lane.LaneType).Distinct().Order());
        Assert.Equal(Enum.GetValues<AutomationStatus>(), Live<Step>().Select(step => step.AutomationStatus).Distinct().Order());
        Assert.Equal(Enum.GetValues<RaciRole>(), Records.OfType<StepRaci>().Select(role => role.Role).Distinct().Order());
        Assert.Equal(Enum.GetValues<SheetStatus>().Where(status => status != SheetStatus.Empty), Records.OfType<StepSheet>().Select(sheet => sheet.Status).Distinct().Order());
        Assert.Equal(Enum.GetValues<DocumentDirection>(), Records.OfType<Document>().Select(document => document.Direction).Distinct().Order());
        Assert.Equal(Enum.GetValues<LocationType>(), Records.OfType<Document>().Where(document => document.LocationType is not null)
            .Select(document => document.LocationType!.Value).Distinct().Order());
        Assert.Equal(Enum.GetValues<MemberRole>(), Records.OfType<ProjectMember>().Select(member => member.Role).Distinct().Order());
        Assert.Equal(Enum.GetValues<TranslationObject>(), Records.OfType<Translation>().Select(translation => translation.ObjectType).Distinct().Order());
        Assert.Equal(Enum.GetValues<TranslationSource>(), Records.OfType<Translation>().Select(translation => translation.Source).Distinct().Order());
        Assert.Contains(Records.OfType<StepSheet>(), sheet => sheet.Frequency is not null);
        Assert.Contains(Records.OfType<SheetOperation>(), operation => operation.Nature is not null);
        Assert.True(Records.OfType<PainPointStatusChange>().Select(change => change.StatusAfter).Distinct().Count() > 1);
    }

    /// <summary>The entries of the value stream of the format 2.1 (ADR 085), each at least once, so that the sample shows them.</summary>
    [Fact]
    public void The_sample_holds_every_entry_of_the_value_stream()
    {
        Assert.Contains(Live<Step>(), step => step.StepType == StepType.Start && step.VolumeQuantity is not null);
        Assert.Contains(Live<Flow>(), flow => flow.SharePercent is not null);
        Assert.Contains(Live<Lane>(), lane => lane.Headcount is not null);
        Assert.Contains(Records.OfType<StepSheet>(), sheet => sheet.LeadTime is not null);
        Assert.Contains(Records.OfType<StepSheetVersion>(), version => version.LeadTime is not null);
        Assert.Contains(Records.OfType<StepSheet>(), sheet => sheet.Frequency == Frequency.PerCase);
        Assert.NotEqual(WorkTime.DefaultWorkdayHours, Records.OfType<Project>().Single().WorkdayHours);
    }

    [Fact]
    public void The_sample_holds_each_kind_of_record_of_the_package_and_the_cases_the_package_must_keep()
    {
        Assert.NotEmpty(Records.OfType<StepSheetVersion>());
        Assert.Contains(Records.OfType<DocumentUse>(), use => use.PercentCompleteAccurate is not null && use.PainPointId is not null);
        Assert.Contains(Records.OfType<Dissolution>(), frame => frame.IsOpen);
        Assert.NotEmpty(Records.OfType<DissolutionStep>());
        Assert.Contains(Records.OfType<DissolutionObject>(), item => item.ObjectType == DissolutionObjectTypes.Flow);
        Assert.Contains(Records.OfType<ProcessMap>(), map => map.ParentStepId is not null && !Live<Step>().Any(step => step.MapId == map.Id));
        Assert.Contains(Live<Step>(), step => step.Row > 0);
        Assert.Contains(Live<Step>(), step => step.AssigneeUserId is not null);
        Assert.Contains(Live<Lane>(), lane => lane.ActorUserId is not null);
        Assert.Contains(Records.OfType<Flow>(), flow => flow.Label is not null);
    }
}
