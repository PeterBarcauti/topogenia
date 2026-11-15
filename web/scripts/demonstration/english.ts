// english.ts
// The English of the texts of the demonstration, as a translator (or an AI) completes the workbook of the translations: one text a line, the French on the left.

/** Every name of the scenario that the workbook may give: lanes, steps, maps, labels of flows. */
const ENGLISH: Readonly<Record<string, string>> = {
  'Notes de frais': 'Expense reports',
  'Salarié': 'Employee',
  'Manager': 'Manager',
  'Comptabilité': 'Accounting',
  'Logiciel de frais': 'Expense software',
  'Dépense engagée': 'Expense incurred',
  'Saisir la note de frais': 'Enter the expense report',
  'Valider la note': 'Approve the report',
  'Note conforme ?': 'Report compliant?',
  'Payer et archiver': 'Pay and file',
  'Archiver les justificatifs': 'File the receipts',
  'Attendre la date de paiement': 'Wait for the payment date',
  'Paiement fait et note archivée': 'Paid and filed',
  'Note remboursée': 'Report reimbursed',
  'Photographier les justificatifs': 'Photograph the receipts',
  'Saisir les lignes de dépense': 'Enter the expense lines',
  'Joindre les photos aux lignes': 'Attach the photos to the lines',
  'Envoyer la note au manager': 'Send the report to the manager',
  'Générer le fichier de virement': 'Generate the transfer file',
  'Transmettre le fichier à la banque': 'Send the file to the bank',
  'oui': 'yes',
  'non': 'no',
};

/** The English of a French text of the scenario; a text not foreseen stops the walk, so that no cell is left to chance. */
export function english(french: string): string {
  const found = ENGLISH[french];
  if (found === undefined) throw new Error(`No English for « ${french} »: add it to english.ts.`);
  return found;
}
