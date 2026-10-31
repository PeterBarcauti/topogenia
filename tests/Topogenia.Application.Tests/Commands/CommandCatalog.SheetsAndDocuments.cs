// CommandCatalog.SheetsAndDocuments.cs
// Rights of the sheet, document, use and translation commands and reads (annex A 1; manual translations of steps and flows, spec 3.6).

using Topogenia.Application.Documents;
using Topogenia.Application.Sheets;
using Topogenia.Application.Translations;

namespace Topogenia.Application.Tests.Commands;

internal static partial class CommandCatalog
{
    internal static readonly Dictionary<Type, string> SheetsAndDocuments = new()
    {
        [typeof(GetSheetQuery)] = "reader",
        [typeof(SaveSheetCommand)] = "sheet_fill",
        [typeof(ValidateSheetCommand)] = "sheet_fill",
        [typeof(RequestClarificationCommand)] = "facilitator",
        [typeof(ChangeClarificationCommand)] = "facilitator",
        [typeof(WithdrawClarificationCommand)] = "facilitator",
        [typeof(AddDocumentCommand)] = "sheet_fill",
        [typeof(UpdateDocumentCommand)] = "sheet_fill",
        [typeof(DeleteDocumentCommand)] = "sheet_fill",
        [typeof(UseDocumentCommand)] = "sheet_fill",
        [typeof(StopUsingDocumentCommand)] = "sheet_fill",
        [typeof(GetAvailableDocumentsQuery)] = "reader",
        [typeof(GetTranslationsQuery)] = "facilitator",
        [typeof(ImportTranslationsCommand)] = "facilitator",
        [typeof(UpdateStepTranslationsCommand)] = "facilitator",
        [typeof(UpdateFlowTranslationsCommand)] = "facilitator",
    };
}
