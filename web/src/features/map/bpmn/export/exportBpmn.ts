// exportBpmn.ts
// Exports the map shown as a .bpmn (OMG BPMN 2.0.2, ADR 106): its copy read as an export (its event written by the host), its diagram compiled in the language the map is shown in, its words too, the classification of the project in its documentation, the file named by the rule of the exports.

import { api } from '../../../../api/client.ts';
import { download, exportFileName } from '../../../../shared/download.ts';
import { i18next, type Translate } from '../../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../../shared/i18n/localizedName.ts';
import { EXPORT_TOAST_KEY } from '../../../../shared/ui/toasts.ts';
import { shownTexts } from '../../state/shownTexts.ts';
import type { MapEditor } from '../../state/editor.ts';
import { diagramOfMap, readCopy } from '../bpmnSource.ts';
import { bpmnXml } from '../model/bpmnXml.ts';

const t: Translate = (key, options) => i18next.t(key, options);

/** The media type of a .bpmn: an XML document. */
export const BPMN_TYPE = 'application/xml';

export interface BpmnExportChoice {
  readonly withSubProcesses: boolean;
  readonly withDetails: boolean;
}

/** The text and the name of the .bpmn of the map shown, the classification of its project read with it. */
export async function bpmnFile(editor: MapEditor, choice: BpmnExportChoice): Promise<{ readonly text: string; readonly name: string } | null> {
  const { map } = editor.store.getState();
  if (map === null) return null;
  const [document, project] = await Promise.all([readCopy(map.id, 'bpmn'), api.get('/api/projects/{id}', { path: { id: map.project_id } })]);
  const language = editor.store.getState().shown?.language ?? i18next.language;
  // Every word the file writes is of the language shown, not of the interface: the roles, the headings, the classification (ADR 106).
  const texts = await shownTexts(language);
  const definitions = diagramOfMap(map, document, { ...choice, language, classification: texts(`classification.${project.classification}`), t: texts });
  const name = localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, language);
  return { text: bpmnXml(definitions), name: exportFileName(map.project_code, name, '.bpmn') };
}

/** Gives the .bpmn to the browser, the person told of the start, of the end or of a failure, as every export. */
export async function exportBpmn(editor: MapEditor, choice: BpmnExportChoice): Promise<void> {
  editor.notify(t('export.running'), undefined, undefined, EXPORT_TOAST_KEY);
  try {
    const file = await bpmnFile(editor, choice);
    if (file === null) return;
    download(new Blob([file.text], { type: BPMN_TYPE }), file.name);
    editor.notify(t('export.done'), 'success', undefined, EXPORT_TOAST_KEY);
  } catch {
    editor.notify(t('export.failed'), 'error', undefined, EXPORT_TOAST_KEY);
  }
}
