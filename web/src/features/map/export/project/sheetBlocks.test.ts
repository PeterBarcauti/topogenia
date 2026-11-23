// sheetBlocks.test.ts
// A sheet of the copy of the project as blocks of text for the PDF: the sections of the read view in their order, the filled facts only, the used inputs, the numbered procedure, the actor as the team wrote it, never a location of a document nor the owner of a pain point.

import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { PmapSheet } from '../../../../shared/pmap.ts';
import { sheetBlocks, type Block } from './sheetBlocks.ts';

const t = i18next.getFixedT('fr');
const names = new Map([['o1', 'Extraction des livres']]);
const blocksOf = (sheet: PmapSheet, status: 'draft' | 'validated' | 'needs_review' | 'empty' | null = null): Block[] =>
  sheetBlocks({ sheet, status, documentName: (id) => names.get(id) }, t, 'fr');
const summary = (blocks: readonly Block[]) => blocks.map((block) => [block.kind, block.label ?? '', block.number ?? '', block.text, block.detail ?? '']);

/** A full sheet: every fact, an input from outside, a document used with its pain point, a procedure, an output, tools, a free text. */
const FULL: PmapSheet = {
  actor: 'Gestionnaire', purpose: 'Facturer le client', tools: 'Excel, ERP', frequency: 'weekly', duration_minutes: 15, duration_min_minutes: 10, duration_max_minutes: 25,
  lead_time: { value: 2, unit: 'day' },
  inputs: [{ name: 'Bon de commande', location: '\\\\serveur\\partage\\bons', wait_hours: 4, medium: 'paper' }],
  uses: [{ document: 'o1', pain_point: 'Requête non mise à jour', pain_point_code: 'IRR-3', pain_point_status: 'scored', owner: 'camille', action: 'Corriger la requête', due_date: '2026-12-01' }],
  procedure: [{ text: 'Ouvrir le dossier', tool: 'ERP', nature: 'extract' }, { text: 'Vérifier le montant', nature: 'check' }, { text: 'Envoyer' }],
  outputs: [{ name: 'Facture', location: '\\\\contoso\\factures' }],
  free_text: 'Premier paragraphe.\nSecond paragraphe.',
};

describe('the blocks of a sheet', () => {
  it('follow the read view: state, filled facts only, used inputs, the procedure numbered with its tool and nature, outputs, tools, free text', () => {
    expect(summary(blocksOf(FULL, 'validated'))).toEqual([
      ['note', '', '', 'État de la fiche : validée', ''],
      ['heading', '', '', 'Repères', ''],
      ['fact', 'Acteur', '', 'Gestionnaire', ''],
      ['fact', 'Finalité', '', 'Facturer le client', ''],
      ['fact', 'Fréquence', '', 'Hebdomadaire', ''],
      ['fact', 'Durée par exécution', '', '15 minutes (10 à 25 minutes)', ''],
      ['fact', 'Délai', '', '2 jours ouvrés', ''],
      ['heading', '', '', 'Entrées', ''],
      ['item', '', '•', 'Extraction des livres', 'Irritant IRR-3'],
      ['item', '', '•', 'Bon de commande', ''],
      ['heading', '', '', 'Déroulé', ''],
      ['item', '', '1.', 'Ouvrir le dossier', 'ERP · Extraire'],
      ['item', '', '2.', 'Vérifier le montant', 'Contrôler'],
      ['item', '', '3.', 'Envoyer', ''],
      ['heading', '', '', 'Sorties', ''],
      ['item', '', '•', 'Facture', ''],
      ['fact', 'Outils', '', 'Excel, ERP', ''],
      ['heading', '', '', 'Description libre de l’étape', ''],
      ['text', '', '', 'Premier paragraphe.\nSecond paragraphe.', ''],
    ]);
  });

  it('a section with nothing in it is not written, a fact left empty neither; the state is said for a draft and a sheet to review, not for a sheet never filled', () => {
    const blocks = blocksOf({ actor: 'Gestionnaire', purpose: '  ', procedure: [], inputs: [], outputs: [{ name: 'Facture' }] }, 'draft');
    expect(summary(blocks)).toEqual([
      ['note', '', '', 'État de la fiche : brouillon', ''],
      ['heading', '', '', 'Repères', ''],
      ['fact', 'Acteur', '', 'Gestionnaire', ''],
      ['heading', '', '', 'Sorties', ''],
      ['item', '', '•', 'Facture', ''],
    ]);
    expect(blocksOf({ actor: 'A' }, 'empty')[0]?.kind).toBe('heading');
    expect(blocksOf({ actor: 'A' }, 'needs_review')[0]?.text).toBe('État de la fiche : à revoir');
  });

  it('a pain point on an input is cited by its code, without its owner, action or date; no location of a document is written', () => {
    const written = JSON.stringify(blocksOf(FULL, 'validated'));
    expect(written).toContain('Irritant IRR-3');
    for (const unwritten of ['camille', 'serveur', 'partage', 'contoso', 'Requête non mise à jour', 'Corriger la requête', '2026-12-01']) expect(written).not.toContain(unwritten);
    // Without a code the pain point is not cited; a document used that the map does not name is left out.
    const uncoded = blocksOf({ uses: [{ document: 'o1', pain_point: 'Texte' }, { document: 'gone', pain_point_code: 'IRR-9' }] });
    expect(summary(uncoded)).toEqual([['heading', '', '', 'Entrées', ''], ['item', '', '•', 'Extraction des livres', '']]);
  });

  it('an empty sheet gives no block, whatever its state', () => {
    expect(blocksOf({}, 'validated')).toEqual([]);
    expect(blocksOf({ actor: '', procedure: [{}], tools: ' , ', inputs: [], free_text: '  \n ' }, 'draft')).toEqual([]);
  });
});
