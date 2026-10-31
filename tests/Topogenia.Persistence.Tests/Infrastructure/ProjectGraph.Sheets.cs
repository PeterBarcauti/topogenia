// ProjectGraph.Sheets.cs
// Sheets, procedure, versions, documents, uses, pain points, translations, log entries and audit of the fictitious project.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Tests.Infrastructure;

public sealed partial class ProjectGraph
{
    /// <summary>One sheet per step of the root map: the four statuses and the seven frequencies.</summary>
    public IReadOnlyList<StepSheet> Sheets { get; private set; } = [];

    public IReadOnlyList<Document> Documents { get; private set; } = [];

    public PainPoint PainPoint { get; private set; } = null!;

    public PainPoint MapPainPoint { get; private set; } = null!;

    /// <summary>The use of the output of the first step by the second step, which raises <see cref="PainPoint"/>.</summary>
    public DocumentUse Use { get; private set; } = null!;

    public OperationLogEntry LogEntry { get; private set; } = null!;

    private void BuildSheets()
    {
        var statuses = Enum.GetValues<SheetStatus>();
        var frequencies = Enum.GetValues<Frequency>();
        Sheets = Steps.Select((step, i) => Add(new StepSheet(Guid.CreateVersion7(), step.Id, statuses[i % statuses.Length],
            i == 3 ? null : $"Actor {i}", i == 3 ? null : $"Purpose {i}", $"Tools {i}", frequencies[i], i == 3 ? null : 12.5m + i,
            i == 0 ? "Free text\nover two lines" : null, "fr", i == 2 ? "Please clarify the second row" : null,
            At(8), Owner.Id, i == 0 ? At(9) : null, i == 0 ? Contributor.Id : null, i + 1)
        {
            LeadTime = valueStream && i == 1 ? 2.5m : null,
            LeadTimeUnit = valueStream && i == 1 ? LeadTimeUnit.Day : null,
        })).ToList();
        var natures = Enum.GetValues<OperationNature>();
        for (var position = 1; position <= natures.Length; position++)
            Add(new SheetOperation(Sheets[0].Id, position, $"Operation {position}", position % 2 == 0 ? "Spreadsheet" : null, natures[position - 1]));
        Add(new SheetOperation(Sheets[1].Id, 1, "Operation without nature", null, null));
        Add(new StepSheetVersion(Guid.CreateVersion7(), Sheets[0].Id, 1, """{"actor":"Actor 0","frequency":"daily"}""",
            Frequency.Daily, 12.5m, Owner.Id, At(9)) { LeadTime = valueStream ? 90m : null, LeadTimeUnit = valueStream ? LeadTimeUnit.Minute : null });
        Add(new StepSheetVersion(Guid.CreateVersion7(), Sheets[0].Id, 2, """{"actor":"Actor 0"}""", null, null, Contributor.Id, At(10)));
    }

    private void BuildDocuments()
    {
        Documents =
        [
            Add(new Document(Guid.CreateVersion7(), Steps[0].Id, DocumentDirection.Output, "Order form", "https://intranet.example.invalid/order",
                LocationType.Url, null, true, "fr", At(9), Owner.Id, At(10), Contributor.Id, 2)),
            Add(new Document(Guid.CreateVersion7(), Steps[1].Id, DocumentDirection.Input, "Supplier quote", @"\\files\fictitious\quotes",
                LocationType.Path, 2.25m, false, "en", At(9), Owner.Id, null, null, 1)),
            Add(new Document(Guid.CreateVersion7(), Steps[2].Id, DocumentDirection.Output, "Report", null, null, null, false, "fr",
                At(9), Owner.Id, null, null, 1)),
        ];
        PainPoint = Add(new PainPoint(Guid.CreateVersion7(), Project.Id, Steps[0].Id, ReadableCode('I', 1), "Form filled twice", "fr", 3.5m,
            PainPointStatus.Scored, At(10), Owner.Id, At(11), Contributor.Id, 2));
        MapPainPoint = Add(new PainPoint(Guid.CreateVersion7(), Project.Id, null, ReadableCode('I', 2), "Raised on the map", "en", null,
            PainPointStatus.Reported, At(10), Owner.Id, null, null, 1));
        PainPointStatus? before = null;
        foreach (var status in Enum.GetValues<PainPointStatus>())
        {
            Add(new PainPointStatusChange(Guid.CreateVersion7(), PainPoint.Id, before, status, before is null ? null : "Moved on", Owner.Id, At(11)));
            before = status;
        }
        Use = Add(new DocumentUse(Documents[0].Id, Steps[1].Id, 1.25m, 80, PainPoint.Id, At(11), Contributor.Id, At(12), Owner.Id) { Version = 3 });
        Add(new DocumentUse(Documents[2].Id, Steps[3].Id, null, null, null, At(11), Contributor.Id, null, null));
    }

    private void Journal()
    {
        Add(new Translation(TranslationObject.Project, Project.Id, TranslationField.Name, "en", "Fictitious process (en)", TranslationSource.Auto, At(12), null));
        Add(new Translation(TranslationObject.Map, RootMap.Id, TranslationField.Name, "en", "Root map (en)", TranslationSource.Manual, At(12), Owner.Id) { Version = 2 });
        Add(new Translation(TranslationObject.Lane, Lanes[0].Id, TranslationField.Name, "en", "Lane (en)", TranslationSource.Auto, At(12), null));
        Add(new Translation(TranslationObject.Step, Steps[0].Id, TranslationField.Label, "en", "Step (en)", TranslationSource.Manual, At(12), Contributor.Id));
        Add(new Translation(TranslationObject.Flow, LiveFlow.Id, TranslationField.Label, "en", "yes (en)", TranslationSource.Auto, At(12), null));
        LogEntry = Add(new OperationLogEntry(Guid.CreateVersion7(), Project.Id, RootMap.Id, Owner.Id, "step.create", 1,
            """{"created":["step"]}""", """{"removed":["step"]}""", """{"step":1}""", At(13), At(14), null));
        Add(new OperationLogEntry(Guid.CreateVersion7(), Project.Id, RootMap.Id, Owner.Id, "step.create", 2,
            """{"created":["step"]}""", """{"removed":["step"]}""", """{"step":2}""", At(15), null, LogEntry.Id));
        Add(new AuditEvent(Guid.CreateVersion7(), At(16), Owner.Id, "project.export", Project.Id, """{"members":false}"""));
        Add(new AuditEvent(Guid.CreateVersion7(), At(17), null, "access.denied", null, null));
    }
}
