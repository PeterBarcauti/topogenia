// map-data.mjs
// Fictional "Achats fournisseurs" map shown in the mockup: grid constants, lanes, steps, arrows and dissolution frame.

/** Grid of annex B 3.1 with AR-19: 184 px lane margin, 30 px before column 0, start view x = 184 + 8. */
export const GRID = {
  header: 184,
  viewX: 192,
  viewY: 16,
  col: 220,
  row: 120,
  margin: 30,
  stepW: 170,
  stepH: 64,
  band: 28,
  frameMargin: 14,
  frameMarginTop: 26,
};

export const LANES = [
  { code: 'PRJ-C01', name: 'Demandeur', type: 'person', typeLabel: 'Personne ou rôle', tint: 1, rows: 1 },
  { code: 'PRJ-C02', name: 'Service Achats', type: 'service', typeLabel: 'Service', tint: 2, rows: 2 },
  { code: 'PRJ-C03', name: 'Logiciel de gestion comptable', type: 'system', typeLabel: 'Système', tint: 3, rows: 2 },
];

/**
 * Steps of the map. sheet = [state, completeness %]; state is draft, validated or needs_review (codes of the
 * server, spec 9.4 asks the classes to match them). pictos are shown at the bottom right (spec 9.4).
 */
export const STEPS = [
  { code: 'E001', type: 'start', lane: 0, col: 0, label: 'Besoin identifié' },
  {
    code: 'E002', type: 'task', lane: 0, col: 1, label: 'Rédiger la demande d’achat', selected: true,
    sheet: ['draft', 62], pictos: { docs: 5, duration: '20 min' },
  },
  { code: 'E005', type: 'end', lane: 0, col: 2, label: 'Demande refusée' },
  {
    code: 'E014', type: 'task', lane: 0, col: 3, label: 'Relancer le fournisseur', hover: true, pictos: { duration: '10 min' },
    warning: 'Cette étape n’est reliée à rien',
  },
  { code: 'E011', type: 'wait', lane: 0, col: 4, label: 'Attendre la livraison', pictos: { docs: 1 } },
  { code: 'E019', type: 'task', lane: 0, col: 5, label: 'Contrôler la livraison' },
  {
    code: 'E020', type: 'task', lane: 1, row: 1, col: 0, label: 'Classer la demande',
    warning: 'Cette étape n’est reliée à rien',
  },
  { code: 'E003', type: 'decision', lane: 1, col: 2, label: 'Demande conforme ?' },
  { code: 'E007', type: 'parallel', lane: 1, col: 3, label: 'Lancer la commande' },
  {
    code: 'E008', type: 'task', lane: 1, col: 4, label: 'Émettre le bon de commande',
    sheet: ['validated', 100], pictos: { docs: 1, duration: '15 min' },
  },
  { code: 'E016', type: 'parallel', lane: 1, col: 5, label: '' },
  {
    // label of three lines with a pictogram: the text moves up (annex B 2.10) and keeps its three lines
    code: 'E006', type: 'task', lane: 1, row: 1, col: 2, label: 'Demander une précision sur le besoin exprimé',
    sheet: ['needs_review', 45], pictos: { irritants: 1 },
  },
  { code: 'E009', type: 'task', lane: 1, row: 1, col: 4, label: 'Prévenir le magasin de réception', cut: true },
  { code: 'E015', type: 'start', lane: 2, col: 0, label: 'Facture reçue' },
  {
    code: 'E012', type: 'task', lane: 2, col: 1, label: 'Rapprocher la facture', macro: 'Hebdomadaire',
    sheet: ['validated', 100], pictos: { docs: 3 },
  },
  { code: 'E013', type: 'inclusive', lane: 2, col: 2, label: 'Écarts ?' },
  // foreign step covered by the dissolution frame: dimmed (annex B 2.9, AR-53)
  { code: 'E021', type: 'task', lane: 2, col: 3, label: 'Archiver la facture', outside: true, pictos: { docs: 1 } },
  { code: 'E017', type: 'task', lane: 2, col: 4, label: 'Mettre en paiement', sheet: ['draft', 25], pictos: { docs: 1, duration: '1 h 30' } },
  { code: 'E018', type: 'task', lane: 2, row: 1, col: 3, label: 'Signaler l’écart au fournisseur' },
];

/**
 * Arrows, as orthogonal polylines in map coordinates (annex B 3.4 and spec 9.7). p(code, side) gives the
 * middle of a side (l, r, t, b) of a step box; for a diamond it is the point of the diamond.
 */
export const EDGES = (p) => [
  { points: [p('E001', 'r'), p('E002', 'l')] },
  // elbow: the vertical segment runs in the alley just before the arrival (rule 5)
  { points: [p('E002', 'r'), [445, 60], [445, 180], p('E003', 'l')] },
  // decision with three exits: one point per exit (AR-24); "oui" sits on a horizontal segment, the two
  // vertical exits have no horizontal segment; the bottom exit moves the gateway label right of its axis (AR-58)
  { points: [p('E003', 'r'), p('E007', 'l')], label: 'oui' },
  // the middle of this vertical arrow is the lane separator: the label sits in the free band above the diamond
  { points: [p('E003', 't'), p('E005', 'b')], label: 'non', labelAt: [555, 134] },
  { points: [p('E003', 'b'), p('E006', 't')], label: 'à revoir', labelAt: [555, 251] },
  // target on the left: loop b to b (rule 1), Bezier curve 32 px under the lowest end (rule 6); it carries the
  // document checked in the sheet of PRJ-E002, so its label shows the document icon and the count (rule 9)
  { points: [p('E006', 'b'), p('E002', 'b')], loop: true, docs: ['Précisions sur la demande'] },
  // parallel branch: second row of the same lane
  { points: [p('E007', 'r'), p('E008', 'l')] },
  { points: [p('E007', 'b'), [775, 300], p('E009', 'l')] },
  { points: [p('E008', 't'), p('E011', 'b')] },
  { points: [p('E011', 'r'), p('E019', 'l')] },
  { points: [p('E008', 'r'), p('E016', 'l')] },
  // the join enters the diamond by its left point g, never by an exit point; the shared stub is 8 px
  { points: [p('E009', 'r'), [1122, 300], [1122, 180], p('E016', 'l')] },
  { points: [p('E015', 'r'), p('E012', 'l')] },
  { points: [p('E012', 'r'), p('E013', 'l')] },
  { points: [p('E013', 'r'), p('E021', 'l')] },
  { points: [p('E021', 'r'), p('E017', 'l')] },
  // second exit of the inclusive gateway: bottom point, free column then free row (rule 2), label on the horizontal
  { points: [p('E013', 'b'), [555, 540], p('E018', 'l')], label: 'écart', labelAt: [614, 540] },
];

/** Dissolution frame (annex B 2.9): dashed rectangle around the member steps, label button; E021 is a foreign step. */
export const FRAME = { members: ['E017', 'E018'], label: 'Reliquat de PRJ-E004 · 3' };
