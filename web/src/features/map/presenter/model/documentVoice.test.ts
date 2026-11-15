// documentVoice.test.ts
// What a presentation says of a document (ADR 101): its type from the medium entered or the extension of its name, what it holds read from its name without what a file name carries, the sentence in the language shown.

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { CONTENT_WORDS, contentOf, documentSaid, documentsSaid, extensionOf, kindOf } from './documentVoice.ts';

const t = (key: string, options?: Record<string, unknown>) => i18next.t(key, options);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the type of a document (ADR 101)', () => {
  it.each([
    ['Suivi CP.xlsx', 'xlsx'], ['Bon.PDF', 'pdf'], ['photo.jpeg', 'jpeg'], ['Bon de commande', null], ['Rapport v2.1', null],
    ['M. Durand', null], ['Note.interne', null], ['a.b', null], ['plan.dwg ', 'dwg'], ['Compte rendu 12.03.2024', null],
  ])('the extension of « %s » is %s', (name, extension) => {
    expect(extensionOf(name)).toBe(extension);
  });

  it('the medium entered says the type: paper, an email, a software, whatever the name', () => {
    expect((['paper', 'email', 'software'] as const).map((medium) => kindOf({ name: 'Bon.pdf', medium }))).toEqual(['paper', 'email', 'software']);
  });

  it('a spreadsheet is an Excel one or a CSV file when its name tells so, a spreadsheet otherwise', () => {
    expect(['Suivi.xlsx', 'Suivi.xls', 'Suivi.xlsm', 'export.csv', 'Suivi', 'Suivi.pdf'].map((name) => kindOf({ name, medium: 'spreadsheet' })))
      .toEqual(['excel', 'excel', 'excel', 'csv', 'spreadsheet', 'spreadsheet']);
  });

  it('a file, another medium or none: the extension of the name, else a file when the medium says so, else a document', () => {
    const names = ['a.xlsx', 'a.csv', 'a.docx', 'a.doc', 'a.pdf', 'a.vsdx', 'a.pptx', 'a.msg', 'a.eml', 'a.png', 'a.JPG', 'a.svg', 'a.zip', 'Bon de commande'];
    expect(names.map((name) => kindOf({ name, medium: 'file' })))
      .toEqual(['excel', 'csv', 'word', 'word', 'pdf', 'visio', 'powerpoint', 'email', 'email', 'image', 'image', 'image', 'file', 'file']);
    expect(['a.pdf', 'a.zip', 'Bon de commande'].map((name) => [kindOf({ name, medium: 'other' }), kindOf({ name, medium: null }), kindOf({ name })]))
      .toEqual([['pdf', 'pdf', 'pdf'], ['document', 'document', 'document'], ['document', 'document', 'document']]);
  });
});

describe('what a document holds, read from its name (ADR 101)', () => {
  it.each([
    ['Suivi_CP_2024-03_v2.xlsx', 'Suivi CP'],
    ['Bon de commande', 'Bon de commande'],
    ['bon-de-commande_V10b.pdf', 'bon de commande'],
    ['Rapport qualité rév. 4 (copie).docx', 'Rapport qualité copie'],
    ['Plan version 3.vsdx', 'Plan'],
    ['Version finale du plan.pdf', 'Version finale du plan'],
    ['Compte rendu 12.03.2024', 'Compte rendu'],
    ['20240312_Devis_000123.pdf', 'Devis'],
    ['scan0001.pdf', 'scan'],
    ['Rapport 8D A3.pdf', 'Rapport 8D A3'],
    ['R&D « budget » 2025.xlsx', 'R&D budget 2025'],
    ['L’offre d\'achat.docx', 'L’offre d\'achat'],
    ['发票 2024.pdf', '发票 2024'],
    ['Счёт_на_оплату_v3.pdf', 'Счёт на оплату'],
  ])('« %s » holds « %s »', (name, content) => {
    expect(contentOf(name)).toBe(content);
  });

  it.each(['2024-03_v2.pdf', 'A.pdf', '000123', 'v3.xlsx', '  ', '2024/03/12 (1).pdf'])('« %s » tells nothing readable', (name) => {
    expect(contentOf(name)).toBe('');
  });

  it('is a title, not a sentence: eight words at most', () => {
    const name = 'Tableau de suivi des demandes de congés payés du service comptabilité.xlsx';
    expect([CONTENT_WORDS, contentOf(name)]).toEqual([8, 'Tableau de suivi des demandes de congés payés']);
  });
});

describe('a document as said (ADR 101)', () => {
  it('its type with its article, then what it holds; its type alone when its name tells nothing', () => {
    expect(documentSaid({ name: 'Suivi_CP_2024-03_v2.xlsx', medium: 'spreadsheet' }, t)).toBe('un tableur Excel : Suivi CP');
    expect(documentSaid({ name: 'Bon de commande', medium: 'paper' }, t)).toBe('un document papier : Bon de commande');
    expect(documentSaid({ name: '2024-03_v2.pdf' }, t)).toBe('un PDF');
    expect(documentSaid({ name: 'Commandes clients', medium: 'software' }, t)).toBe('des données d’un logiciel : Commandes clients');
    expect(documentSaid({ name: 'Bon de commande', medium: 'file' }, t)).toBe('un fichier : Bon de commande');
  });

  it('a document whose type nothing tells is said by its title alone, and « un document » when its name tells nothing', () => {
    expect([{ name: 'Bon de commande' }, { name: 'Bon_de_commande_v2', medium: 'other' as const }, { name: 'archive_2024.zip' }, { name: '000123' }].map((document) => documentSaid(document, t)))
      .toEqual(['Bon de commande', 'Bon de commande', 'archive 2024', 'un document']);
  });

  it('every type has its words in French and in English', async () => {
    const kinds = ['paper', 'email', 'software', 'spreadsheet', 'excel', 'csv', 'word', 'pdf', 'visio', 'powerpoint', 'image', 'file', 'document'];
    expect(kinds.map((kind) => t(`presenter.say.kind.${kind}`))).toEqual([
      'un document papier', 'un courriel', 'des données d’un logiciel', 'un tableur', 'un tableur Excel', 'un fichier CSV', 'un document Word', 'un PDF',
      'un diagramme Visio', 'une présentation PowerPoint', 'une image', 'un fichier', 'un document',
    ]);
    await i18next.changeLanguage('en');
    expect(kinds.map((kind) => t(`presenter.say.kind.${kind}`))).toEqual([
      'a paper document', 'an email', 'data from software', 'a spreadsheet', 'an Excel spreadsheet', 'a CSV file', 'a Word document', 'a PDF',
      'a Visio diagram', 'a PowerPoint presentation', 'an image', 'a file', 'a document',
    ]);
    expect(documentSaid({ name: 'Leave_tracking_v2.xlsx' }, t)).toBe('an Excel spreadsheet: Leave tracking');
  });

  it('two documents said the same way are said once, in the order of the map', () => {
    const documents = [{ name: '2024-01.pdf' }, { name: 'Devis.pdf' }, { name: '2024-02.pdf' }, { name: 'Devis_v2.pdf' }, { name: 'Bon', medium: 'paper' as const }, { name: 'Bon' }];
    expect(documentsSaid(documents, t)).toEqual(['un PDF', 'un PDF : Devis', 'un document papier : Bon', 'Bon']);
  });
});

describe('an extension that is not at the very end of the name is never said, and may tell the type (R4-02)', () => {
  it.each([
    ['Suivi.xlsx (onglet Congés)', 'excel', 'Suivi onglet Congés'],
    ['Suivi.xlsx, Budget.xlsx', 'excel', 'Suivi Budget'],
    ['Fiche de poste.doc.pdf', 'pdf', 'Fiche de poste'],
    ['Suivi CP.xlsx - Raccourci.lnk', 'excel', 'Suivi CP Raccourci'],
    ['Rapport.xlsx.', 'excel', 'Rapport'],
    ['Inventaire.xlsx\u200B', 'excel', 'Inventaire'],
  ])('« %s » is of the type %s and holds « %s »', (name, kind, content) => {
    expect([kindOf({ name }), contentOf(name)]).toEqual([kind, content]);
  });

  it('said with the type the extension tells, the sheet of the workbook after it', () => {
    expect(documentSaid({ name: 'Suivi.xlsx (onglet Congés)' }, t)).toBe('un tableur Excel : Suivi onglet Congés');
    expect(documentSaid({ name: 'Suivi.xlsx (onglet Congés)', medium: 'spreadsheet' }, t)).toBe('un tableur Excel : Suivi onglet Congés');
  });
});

describe('a code is not said: an acronym stuck to a number by a dash, by an underscore or without a separator (R4-03)', () => {
  it.each([
    ['PR-012_Achats.docx', 'un document Word : Achats'],
    ['PR-ACH-001 Procédure achats.docx', 'un document Word : Procédure achats'],
    ['FOR-RH-007-Demande-de-conges.docx', 'un document Word : Demande de conges'],
    ['Mode opératoire MO-PROD-0042.pdf', 'un PDF : Mode opératoire'],
    ['DOC-2024-001.pdf', 'un PDF'],
    ['PR-012.docx', 'un document Word'],
    ['PR012.docx', 'un document Word'],
    ['PR012A.docx', 'un document Word'],
    ['PR–012 Achats.docx', 'un document Word : Achats'],
    ['DSC00123.JPG', 'une image'],
  ])('« %s » is said « %s »', (name, said) => {
    expect(documentSaid({ name }, t)).toBe(said);
  });

  // An acronym stays when no number is stuck to it or when a date follows it; a word in lower case, or of more than four capitals, is no acronym.
  it.each([
    ['Suivi_CP_2024-03_v2.xlsx', 'un tableur Excel : Suivi CP'],
    ['Suivi-CP-2024-03-v2.xlsx', 'un tableur Excel : Suivi CP'],
    ['CR-2024-03-12.docx', 'un document Word : CR'],
    ['CA2023.xlsx', 'un tableur Excel : CA'],
    ['BON-DE-COMMANDE-123.pdf', 'un PDF : BON DE COMMANDE 123'],
    ['DEVIS0153.pdf', 'un PDF : DEVIS'],
    ['compte-rendu-12.docx', 'un document Word : compte rendu 12'],
    ['note-12.docx', 'un document Word : note 12'],
    ['scan0001.pdf', 'un PDF : scan'],
    ['Rapport 8D A3.pdf', 'un PDF : Rapport 8D A3'],
  ])('« %s » keeps its words: « %s »', (name, said) => {
    expect(documentSaid({ name }, t)).toBe(said);
  });

  // An underscore sticks a code too, when its number has three digits or more and is no date.
  it.each([
    ['IMG_1234.JPG', 'une image'],
    ['DSC_0001.jpg', 'une image'],
    ['FA_0042_Remboursement.pdf', 'un PDF : Remboursement'],
    ['Note_RH_3.docx', 'un document Word : Note RH 3'],
    ['Planning_S_12.xlsx', 'un tableur Excel : Planning S 12'],
  ])('« %s », written with underscores, is said « %s »', (name, said) => {
    expect(documentSaid({ name }, t)).toBe(said);
  });

  it('the marks of an email answered or forwarded are not said either', () => {
    expect(documentSaid({ name: 'RE: TR: Demande de congés.msg' }, t)).toBe('un courriel : Demande de congés');
  });
});

describe('an address or a path typed as a name: its last segment alone (R4-04)', () => {
  it.each([
    ['C:\\Users\\accueil\\Documents\\Suivi CP.xlsx', 'un tableur Excel : Suivi CP'],
    ['\\\\serveur\\partage\\Musée\\Inventaire.xlsx', 'un tableur Excel : Inventaire'],
    ['/partage/association/Statuts.pdf', 'un PDF : Statuts'],
    ['https://localhost/sites/musee/Documents%20partages/Proc%C3%A9dure%20cong%C3%A9s.docx', 'un document Word : Procédure congés'],
    ['https://localhost/visites/Planning_2024.pdf?version=3#page=2', 'un PDF : Planning 2024'],
    ['https://localhost/', 'un document'],
    ['www.exemple.org', 'un document'],
    ['accueil@exemple.org', 'un document'],
  ])('« %s » is said « %s »', (name, said) => {
    expect(documentSaid({ name }, t)).toBe(said);
  });

  it('a slash between the words of a title is no path', () => {
    expect(documentSaid({ name: 'Entrées/Sorties 2024.xlsx' }, t)).toBe('un tableur Excel : Entrées Sorties 2024');
  });
});

describe('the last word, the versions, the dates, the signs and the short titles (R4-05 to R4-09)', () => {
  it.each([
    // R4-05: a last word after a dot is an extension only when it is a known one.
    ['Rapport.final', 'Rapport final'], ['Suivi.CP', 'Suivi CP'], ['Liste.prix', 'Liste prix'], ['Réunion du 12.nov', 'Réunion du 12 nov'], ['plan.dwg', 'plan'], ['Notes.md', 'Notes'],
    // R4-06: « vers » before a number is a word, not a mark of version: the number is said with it (UX-MAJ-06).
    ['Transfert vers 2 sites.pptx', 'Transfert vers 2 sites'], ['Flux vers 3 entrepôts.vsdx', 'Flux vers 3 entrepôts'],
    // R4-07: what is left of a date or of a time.
    ['Rapport 2024-03-12T10-15.pdf', 'Rapport'], ['Réunion 10h30.docx', 'Réunion'], ['見積書_2024年3月.pdf', '見積書'],
    // R4-08: a number said keeps its sign, stuck or apart (UX-MAJ-06); the quotes and the signs of other writings.
    ['Budget 100%.xlsx', 'Budget 100%'], ['Budget 100 %.xlsx', 'Budget 100 %'], ['Devis 1500€.pdf', 'Devis 1500€'], ['Facture n° 2024-0153.pdf', 'Facture'],
    ['„Raport” końcowy.docx', 'Raport końcowy'], ['Rapport＿最終.xlsx', 'Rapport 最終'], ['\u{1F4C4} Rapport qualité.pdf', 'Rapport qualité'], ['Entrées & Sorties.xlsx', 'Entrées & Sorties'],
    ['Suivi\u200BCP.xlsx', 'Suivi CP'],
  ])('« %s » holds « %s »', (name, content) => {
    expect(contentOf(name)).toBe(content);
  });

  it('a short title is said: two letters or a letter and a digit, even apart (R4-09)', () => {
    expect([{ name: 'R&D' }, { name: '8D' }, { name: 'R&D.pptx' }, { name: 'P&L 2024.xlsx' }, { name: '5S.xlsx' }, { name: 'A.pdf' }].map((document) => documentSaid(document, t)))
      .toEqual(['R&D', '8D', 'une présentation PowerPoint : R&D', 'un tableur Excel : P&L 2024', 'un tableur Excel : 5S', 'un PDF']);
  });

  it('a last word that is no known extension tells no extension', () => {
    expect(['Rapport.final', 'Suivi.CP', 'Fiche de poste.doc.pdf', 'Suivi CP.xlsx - Raccourci.lnk'].map(extensionOf)).toEqual([null, null, 'pdf', 'xlsx']);
  });
});

describe('the numbers of a title (audit 3.2, UX-MAJ-06)', () => {
  // One to four digits, no zero at the head, alone beside a word of letters, before or after it; with its sign.
  it.each([
    ['Certificat ISO 9001.pdf', 'Certificat ISO 9001'], ['Annexe 2.docx', 'Annexe 2'], ['Bilan 2025.xlsx', 'Bilan 2025'], ['3 devis comparatifs.pdf', '3 devis comparatifs'],
    ['Salle 7 - plan.pdf', 'Salle 7 plan'], ['Facture n° 12.pdf', 'Facture 12'], ['Budget 100 %.xlsx', 'Budget 100 %'],
    ['https://localhost/2024/Annexe 2.pdf', 'Annexe 2'],
  ])('« %s » holds « %s »: the number is said', (name, content) => {
    expect(contentOf(name)).toBe(content);
  });

  // A date or a run of numbers, a number after a mark of version, of five digits or more, with a zero at its head, beside no word of letters; a code, a path.
  it.each([
    ['Compte rendu 12.03.2024', 'Compte rendu'], ['Réunion 12.3 Bureau.docx', 'Réunion Bureau'], ['Plan v 2.pdf', 'Plan'], ['Lot 12345.xlsx', 'Lot'],
    ['Annexe 02.docx', 'Annexe'], ['Rapport 2024-03-12T10-15.pdf', 'Rapport'], ['Certificat ISO-9001.pdf', 'Certificat'], ['2025.pdf', ''], ['12 - 15.pdf', ''],
    ['C:\\Archives\\2024\\Bilan.xlsx', 'Bilan'], ['IMG_1234.JPG', ''],
  ])('« %s » holds « %s »: the number is left out', (name, content) => {
    expect(contentOf(name)).toBe(content);
  });
});
