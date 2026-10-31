// PmapBuild.Sheets.cs
// The sheet blocks of a document written as drafts, with their documents and their uses, by the rules of the API (AR-93).

using Topogenia.Domain.Documents;
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
        /// of other steps of the same map are ticked (`DocumentUses.Use`), their pain points raised on the producing step.
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
                    [SheetContentReader.LeadTimeKey] = new(sheet.LeadTime?.Unit, sheet.LeadTime?.Value),
                }, [.. sheet.Procedure.Select(task => (ProcedureLine?)new ProcedureLine(task.Text, task.Tool, task.Nature))], sheet.FreeText);
                Rule(place, () => SheetWorkflow.Save(workspace, context, id, input));
                SheetCount++;
                foreach (var (output, index) in sheet.Outputs.Select((output, index) => (output, index)))
                {
                    var documentId = Rule($"{place}.outputs[{index}]", () => DocumentEdits.Add(workspace, context, id,
                        new DocumentInput(Codes.Of(DocumentDirection.Output), output.Name, output.Location, null, output.ShareConfirmed)));
                    if (output.Id is not null)
                        outputs[output.Id] = documentId;
                }
                foreach (var (entry, index) in sheet.Inputs.Select((entry, index) => (entry, index)))
                    Rule($"{place}.inputs[{index}]", () => DocumentEdits.Add(workspace, context, id,
                        new DocumentInput(Codes.Of(DocumentDirection.Input), entry.Name, entry.Location, entry.WaitHours, entry.ShareConfirmed)));
            }
            foreach (var (step, id, path) in withSheet)
            {
                foreach (var (use, index) in step.Sheet!.Uses.Select((use, index) => (use, index)))
                    Rule($"{path}.sheet.uses[{index}]", () => DocumentUses.Use(workspace, context, id, outputs[use.Document],
                        new DocumentUseInput(use.WaitHours, use.PercentCompleteAccurate, use.PainPoint, use.LostHoursPerMonth)));
            }
        }
    }
}
