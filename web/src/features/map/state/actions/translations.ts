// translations.ts
// The manual translations of a step, a flow or a lane written from the map, the languages changed only, through the queue of the editor (B-5-28, A-2.14-03, A-2.14-06).

import { api, type Schema } from '../../../../api/client.ts';
import type { TranslatedName } from '../../../translations/ManualTranslationsDialog.tsx';
import type { MapEditor } from '../editor.ts';
import type { TranslatedObject } from '../floating.ts';
import { FLOW_LABEL_MAX } from './flows.ts';
import { LANE_NAME_MAX } from './lanes.ts';
import { STEP_LABEL_MAX } from './steps.ts';

type MapView = Schema<'MapView'>;

/** The name of an object of the map to translate; null when the object is gone or has no name (a flow without label). */
export function translatedOf(map: MapView, object: TranslatedObject): TranslatedName | null {
  switch (object.kind) {
    case 'step': {
      const step = map.steps.find((candidate) => candidate.id === object.id);
      return step === undefined ? null : { name: step.label, sourceLanguage: step.source_language, translations: step.translations, max: STEP_LABEL_MAX };
    }
    case 'flow': {
      const flow = map.flows.find((candidate) => candidate.id === object.id);
      // The language of origin of a flow is the one of its source step (A-2.14-06).
      const source = map.steps.find((step) => step.id === flow?.source_step_id);
      return flow?.label == null || source === undefined ? null : { name: flow.label, sourceLanguage: source.source_language, translations: flow.translations, max: FLOW_LABEL_MAX };
    }
    case 'lane': {
      const lane = map.lanes.find((candidate) => candidate.id === object.id);
      return lane === undefined ? null : { name: lane.name, sourceLanguage: lane.source_language, translations: lane.translations, max: LANE_NAME_MAX };
    }
  }
}

/** Opens the manual translations of an object, from its menu. */
export function openTranslate(editor: MapEditor, object: TranslatedObject): void {
  editor.floating.getState().open({ kind: 'translate', object });
}

/** The call that writes the translations of an object with its version (If-Match), null when the object is gone. */
function writeOf(map: MapView, object: TranslatedObject, translations: Record<string, string>) {
  switch (object.kind) {
    case 'step': {
      const step = map.steps.find((candidate) => candidate.id === object.id);
      return step === undefined ? null : api.put('/api/steps/{id}/translations', { path: { id: step.id }, body: { translations }, version: step.version });
    }
    case 'flow': {
      const flow = map.flows.find((candidate) => candidate.id === object.id);
      return flow === undefined ? null : api.put('/api/flows/{id}/translations', { path: { id: flow.id }, body: { translations }, version: flow.version });
    }
    case 'lane': {
      const lane = map.lanes.find((candidate) => candidate.id === object.id);
      return lane === undefined ? null : api.patch('/api/lanes/{id}', { path: { id: lane.id }, body: { translations }, version: lane.version });
    }
  }
}

/**
 * Writes the languages changed: a text is a manual translation, an empty text deletes it (A-2.14-04). The reply carries the
 * map, applied by the editor; true when it is saved, false when it was refused (the editor said why).
 */
export async function translateObject(editor: MapEditor, object: TranslatedObject, changes: Record<string, string>): Promise<boolean> {
  return (await editor.write((map) => writeOf(map, object, changes))) !== null;
}
