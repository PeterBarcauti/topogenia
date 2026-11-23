// maps.ts
// Fictitious maps as the API gives them (MapView), built piece by piece for the tests of the editor of the map.

import type { Schema } from '../api/client.ts';
import { LANE_TINTS } from '../shared/theme/laneTints.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;
type LaneView = Schema<'LaneView'>;
type FlowView = Schema<'FlowView'>;

export const MAP_ID = '0190a1b2-0000-7000-8000-00000000a001';
export const PROJECT_ID = '0190a1b2-0000-7000-8000-00000000b001';

/** The pastel of a tint, by its number from 1 (spec 9.2): the colour the host gives a lane. */
export const pastelOf = (tint: number): string => LANE_TINTS[(tint - 1) % LANE_TINTS.length]?.pastel ?? '';

export const laneOf = (id: string, order: number, overrides: Partial<LaneView> = {}): LaneView => ({
  id, code: `VIS-C0${String(order + 1)}`, name: `Couloir ${id}`, source_language: 'fr', translations: {}, type: 'person',
  color: pastelOf(order + 1), sort_order: order, actor_user_id: null, version: 1, ...overrides,
});

export const sheetOf = (overrides: Partial<Schema<'SheetSummary'>> = {}): Schema<'SheetSummary'> => ({
  status: 'empty', completeness: 0, can_fill: true, frequency: null, duration_minutes: null, ...overrides,
});

export const stepOf = (id: string, laneId: string, column: number, overrides: Partial<StepView> = {}): StepView => ({
  id, code: `VIS-E${String(column + 1).padStart(3, '0')}`, label: `Étape ${id}`, source_language: 'fr', type: 'task', lane_id: laneId,
  column, row: 0, path: `Visites › Étape ${id}`, sub_map_id: null, sub_map_empty: true, assignee_user_id: null, automation_status: 'manual',
  last_modified: '2026-10-20T08:00:00Z', sheet: sheetOf(), version: 1, translations: {}, markers: { documents: [], pain_points: [] }, ...overrides,
});

export const flowOf = (id: string, source: string, target: string, overrides: Partial<FlowView> = {}): FlowView => ({
  id, source_step_id: source, target_step_id: target, label: null, documents: [], version: 1, translations: {}, ...overrides,
});

/** The value stream of a map without any entry (ADR 085): nothing quantified, the work time by default. */
export const streamOf = (overrides: Partial<Schema<'MapStream'>> = {}): Schema<'MapStream'> => ({
  map_id: MAP_ID, work_time: { workday_hours: 7, fte_hours_per_month: 151.67, days_per_month: 21.67 }, steps: {}, flows: {}, lanes: {},
  main_path: null, issues: [],
  totals: {
    entry_volume: null, expected_lead_hours: null, work_per_case_minutes: null, charge_hours: 0, charge_complete: true, fte: 0,
    rolled_complete_accurate: null, rated_receptions: 0, receptions: 0, lost_hours_per_month: 0, open_pain_points: 0, steps_with_content: 0,
    steps_quantified: 0,
  },
  handoffs: { flows: [], detailed_steps: {}, count: 0, per_case: null },
  value_classes: {
    value_added: 0, necessary: 0, waste: 0, unclassified: 0, value_added_minutes: null, necessary_minutes: null, waste_minutes: null, unclassified_minutes: null,
    value_added_share: null, coverage: null,
  },
  media_breaks: { steps: [], detailed_steps: {}, count: 0, documents: 0, documents_with_medium: 0 },
  analysis: { lead: null, leads_entered: 0, places: [] },
  ...overrides,
});

/** A map of the project VIS whose viewer may change the structure unless told otherwise. */
export const mapOf = (overrides: Partial<MapView> = {}): MapView => ({
  id: MAP_ID, project_id: PROJECT_ID, project_code: 'VIS', name: 'Visites', source_language: 'fr', translations: {}, parent_step_id: null,
  depth: 1, recommended_depth: 4, breadcrumb: [{ map_id: MAP_ID, name: 'Visites', source_language: 'fr', translations: {} }], lanes: [], steps: [], flows: [],
  warnings: [], raci: [], raci_letters: 'ACI', rights: { structure: true, role: 'facilitator' }, version: 3, structure_version: 7,
  dissolution: null, dissolution_sheet: null, languages: { origin: 'fr', total: 0, written: {} }, value_stream: streamOf(), stream_subjects: {},
  ...overrides,
});

/** Two lanes, four steps (a start, a task, a decision with two exits, an end) and their flows, one labelled, one carrying documents. */
export function sampleMap(overrides: Partial<MapView> = {}): MapView {
  return mapOf({
    lanes: [laneOf('l1', 0, { name: 'Commercial', translations: { en: 'Visits' } }), laneOf('l2', 1, { name: 'Comptabilité', type: 'department' })],
    steps: [
      stepOf('s1', 'l1', 0, { type: 'start', label: 'Demande reçue' }),
      stepOf('s2', 'l1', 1, { label: 'Vérifier la demande', translations: { en: 'Check the request' } }),
      stepOf('s3', 'l1', 2, { type: 'decision', label: 'Conforme ?' }),
      stepOf('s4', 'l2', 3, { type: 'end', label: 'Facture émise' }),
    ],
    flows: [
      flowOf('f1', 's1', 's2'),
      flowOf('f2', 's2', 's3', { documents: ['Bon de commande', 'Devis'] }),
      flowOf('f3', 's3', 's4', { label: 'oui', translations: { en: 'yes' } }),
      flowOf('f4', 's3', 's2', { label: 'non' }),
    ],
    // A project of nine texts written in French, five of them translated into English: shown in English with an
    // interface in English (ADR 083: half of the texts at least).
    languages: { origin: 'fr', total: 9, written: { fr: 9, en: 5 } },
    ...overrides,
  });
}

/** An open frame of the dissolved step VIS-E009 « Préparer la commande » around the steps given, its remainder empty unless told otherwise. */
export const frameOf = (stepIds: readonly string[], overrides: Partial<Schema<'FrameView'>> = {}): Schema<'FrameView'> => ({
  id: 'd1', step_id: 'gone', step_code: 'VIS-E009', step_label: 'Préparer la commande', frame_step_ids: [...stepIds], documents: [], usages: [], pain_points: [],
  sheet_id: null, ...overrides,
});

/** The reply of a write of the map (`StructureReply`): the map up to date, an empty history unless told otherwise. */
export const replyOf = (map: MapView | null, overrides: Partial<Schema<'StructureReply'>> = {}): Schema<'StructureReply'> => ({
  id: null, code: null, ids: [], operation_id: 'op1', warnings: [], structure_version: 8, map, history: { undo: [], redo: [] }, ...overrides,
});

/**
 * The selection of the trial of the 3.4 (R8): three steps chosen in a staircase over three lanes (`c1`, `c2`, `c3`, the
 * last one detailed with its frequency under it), and steps not chosen on their left, their right, under them in the lane
 * of the last one, and above the second one, detailed with its frequency hanging towards it.
 */
export const staircaseMap = (): MapView => mapOf({
  lanes: [laneOf('l1', 0), laneOf('l2', 1), laneOf('l3', 2)],
  steps: [
    stepOf('left', 'l1', 0, { code: 'VIS-E001' }),
    stepOf('c1', 'l1', 1, { code: 'VIS-E002' }),
    stepOf('above', 'l1', 2, { code: 'VIS-E003', sub_map_id: 'm3', sub_map_empty: false, sheet: sheetOf({ frequency: 'weekly' }) }),
    stepOf('c2', 'l2', 2, { code: 'VIS-E004' }),
    stepOf('c3', 'l3', 3, { code: 'VIS-E005', sub_map_id: 'm5', sub_map_empty: false, sheet: sheetOf({ frequency: 'monthly' }) }),
    stepOf('right', 'l3', 4, { code: 'VIS-E006' }),
    stepOf('lowLeft', 'l3', 1, { code: 'VIS-E007', row: 1 }),
    stepOf('below', 'l3', 3, { code: 'VIS-E008', row: 1 }),
  ],
  flows: [
    flowOf('f1', 'left', 'c1'), flowOf('f2', 'c1', 'c2', { documents: ['Facture'] }), flowOf('f3', 'c2', 'c3', { documents: ['Facture'] }),
    flowOf('f4', 'c3', 'right'), flowOf('f5', 'c3', 'below'), flowOf('f6', 'lowLeft', 'below'),
  ],
});
