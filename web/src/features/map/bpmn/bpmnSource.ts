// bpmnSource.ts
// The BPMN 2.0 diagram of the map shown, from its copy read by the host (ADR 106): read for the view without any event, read for a .bpmn as an export; in the language the map is shown in, its name and its summary from the .pmap.

import { api, type Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import { pmapOf, type PmapDocument } from '../../../shared/pmap.ts';
import { bpmnOf } from './model/bpmnCompile.ts';
import type { BpmnDefinitions } from './model/bpmnModel.ts';
import { shown } from './model/bpmnText.ts';

/** What the copy is read for: the view (no event) or a .bpmn (an export, with its event). */
export type BpmnPurpose = 'view' | 'bpmn';

/**
 * The copy of the map with the sheets as the person sees them: for a .bpmn, with the maps below it; for the view, the map
 * alone, without the places of its documents (audit of the copy 64, P2-03), its detailed steps known from the map.
 */
export async function readCopy(mapId: string, purpose: BpmnPurpose): Promise<PmapDocument> {
  return pmapOf(await api.get('/api/maps/{id}/pmap', { path: { id: mapId }, query: { purpose } }));
}

/** No step, no lane: the mark of a detail the copy of the view does not carry, which the view never draws (ADR 106). */
const DETAIL_MARK = { lanes: [], steps: [], flows: [] } as const;

/** The copy of the view with its detailed steps marked, as the map knows them: each is drawn as a sub-process with its [+]. */
export function withDetailsOfMap(document: PmapDocument, map: Schema<'MapView'>): PmapDocument {
  const detailed = new Set(map.steps.filter((step) => step.sub_map_id !== null).map((step) => step.code));
  return {
    ...document,
    map: { ...document.map, steps: (document.map.steps ?? []).map((step) => (step.sub_map === undefined && detailed.has(step.id) ? { ...step, sub_map: DETAIL_MARK } : step)) },
  };
}

/** What is asked of the diagram besides its copy. */
export interface BpmnAsked {
  readonly language: string;
  readonly withDetails: boolean;
  readonly withSubProcesses: boolean;
  readonly classification: string | null;
  readonly t: Translate;
}

/** The diagram of the map shown: its name in the language shown, its summary from its copy when it describes it. */
export function diagramOfMap(map: Schema<'MapView'>, document: PmapDocument, asked: BpmnAsked): BpmnDefinitions {
  const origin = document.project.language ?? asked.language;
  const summary = document.map.summary === undefined ? null : shown(document.map.summary.text, document.map.summary.translations, origin, asked.language).text;
  return bpmnOf(document, {
    language: asked.language, withSubProcesses: asked.withSubProcesses, withDetails: asked.withDetails, classification: asked.classification, summary,
    mapTitle: localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, asked.language),
    exporterVersion: __APP_VERSION__, t: asked.t,
  });
}
