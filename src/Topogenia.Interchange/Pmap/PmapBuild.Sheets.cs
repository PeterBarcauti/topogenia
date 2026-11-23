// PmapBuild.Sheets.cs
// The sheet blocks of a document written as drafts, with their documents and their uses, by the rules of the API (AR-93), in the language of the file.

using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Pmap;

public static partial class PmapBuild
{
    private sealed partial class Builder
    {
        /// <summary>
        /// The sheets, once every map is built: each block becomes the draft of its step (`SheetWorkflow.Save`, never a
        /// validated version), its outputs and external inputs are added (`DocumentEdits.Add`), then the uses of the outputs
        /// of other steps of the same map are ticked (`DocumentUses.Use`), their pain points raised on the producing step
        /// and given the status of the file (`PainPointRules.ChangeStatus`).
        /// </summary>
        public void Sheets()
        {
            var outputs = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var withSheet = built.Where(item => item.Step.Sheet is not null).ToList();
            foreach (var (step, id, path) in withSheet)
            {
                var sheet = step.Sheet!;
                var place = $"{path}.sheet";
                var input = new SheetInput(new Dictionary<string, SheetFieldValue?>
                {
                    [SheetContentReader.ActorKey] = new(sheet.Actor, null),
                    [SheetContentReader.PurposeKey] = new(sheet.Purpose, null),
                    [SheetContentReader.ToolsKey] = new(sheet.Tools, null),
                    [SheetContentReader.FrequencyKey] = new(sheet.Frequency, null),
                    [SheetContentReader.DurationKey] = new(null, sheet.DurationMinutes),
                    [SheetContentReader.DurationMinKey] = new(null, sheet.DurationMinMinutes),
                    [SheetContentReader.DurationMaxKey] = new(null, sheet.DurationMaxMinutes),
                    [SheetContentReader.LeadTimeKey] = new(sheet.LeadTime?.Unit, sheet.LeadTime?.Value),
                }, [.. sheet.Procedure.Select(task => (ProcedureLine?)new ProcedureLine(task.Text, task.Tool, task.Nature))], sheet.FreeText);
                Rule(place, () => SheetWorkflow.Save(workspace, context, id, input));
                SheetCount++;
                foreach (var (output, index) in sheet.Outputs.Select((output, index) => (output, index)))
                {
                    var documentId = Rule($"{place}.outputs[{index}]", () => DocumentEdits.Add(workspace, context, id,
                        new DocumentInput(Codes.Of(DocumentDirection.Output), output.Name, output.Location, null, output.ShareConfirmed) { Medium = output.Medium }));
                    if (output.Id is not null)
                        outputs[output.Id] = documentId;
                }
                foreach (var (entry, index) in sheet.Inputs.Select((entry, index) => (entry, index)))
                    Rule($"{place}.inputs[{index}]", () => DocumentEdits.Add(workspace, context, id,
                        new DocumentInput(Codes.Of(DocumentDirection.Input), entry.Name, entry.Location, entry.WaitHours, entry.ShareConfirmed) { Medium = entry.Medium }));
            }
            foreach (var (step, id, path) in withSheet)
            {
                foreach (var (use, index) in step.Sheet!.Uses.Select((use, index) => (use, index)))
                    Rule($"{path}.sheet.uses[{index}]", () => Use(id, outputs[use.Document], use));
            }
            InFileLanguage();
        }

        /// <summary>
        /// A use ticked by the rule of the API, then its pain point given the status of the file when it is not the one of
        /// its creation (ADR 116 point 10), open or dealt with, by the rule of the core: its history starts at the opening.
        /// </summary>
        private DocumentUseResult Use(Guid stepId, Guid documentId, PmapUse use)
        {
            var result = DocumentUses.Use(workspace, context, stepId, documentId, use.Input);
            if (result.PainPointId is { } painPointId && use.Status != PainPointRules.ProducedStatus)
                PainPointRules.ChangeStatus(workspace, context, stepId, painPointId, use.Status);
            return result;
        }

        /// <summary>
        /// The sheets, documents and pain points of the steps built, in the language of the file: the rules of the core write
        /// the language of the project, which is that of the file when the file opens a project, but not when it is inserted
        /// into a project of another language (3.3, F-208; audit of the copy 64, P1-04). Their texts are those of the file.
        /// </summary>
        private void InFileLanguage()
        {
            if (string.Equals(language, workspace.TextLanguage, StringComparison.Ordinal))
                return;
            var steps = built.Select(item => item.Id).ToHashSet();
            foreach (var sheet in workspace.All<StepSheet>().Where(sheet => steps.Contains(sheet.StepId) && sheet.SourceLanguage != language).ToList())
                workspace.Put(sheet with { SourceLanguage = language });
            foreach (var document in workspace.All<Document>().Where(document => steps.Contains(document.StepId) && document.SourceLanguage != language).ToList())
                workspace.Put(document with { SourceLanguage = language });
            foreach (var pain in workspace.All<PainPoint>().Where(pain => pain.StepId is { } step && steps.Contains(step) && pain.SourceLanguage != language).ToList())
                workspace.Put(pain with { SourceLanguage = language });
        }
    }
}
