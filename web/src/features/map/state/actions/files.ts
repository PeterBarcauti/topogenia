// files.ts
// What the File menu of the map does without a dialog of its own: the copy of the project downloaded, the copy of the map shown saved (F-208), the project to replace read before its dialog (annex B 2.4, ADR 058).

import { api, type Schema } from '../../../../api/client.ts';
import { download, exportFileName } from '../../../../shared/download.ts';
import { messageOf } from '../../../../shared/i18n/errorMessage.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../../shared/i18n/localizedName.ts';
import { downloadPmapCopy } from '../../../project/files/pmapCopy.ts';
import type { MapEditor } from '../editor.ts';

/** The days the trash keeps a project when the trash cannot be read (`trash_retention_days`, 30 by default). */
const DEFAULT_RETENTION_DAYS = 30;

/** The project of a map: its identifier, its code, and its name, the one of its root map, first of the breadcrumb (1.x `nomProjet`). */
export const projectOf = (map: Schema<'MapView'>): { readonly id: string; readonly code: string; readonly name: string } =>
  ({ id: map.project_id, code: map.project_code, name: map.breadcrumb[0]?.name ?? map.name });

/** « Télécharger une copie (.pmap) »: the maps without the sheets, saved by the browser, then said (B-2.4-54, B-1.6-23, B-1.6-24). */
export async function downloadCopy(editor: MapEditor, map: Schema<'MapView'>): Promise<void> {
  try {
    await downloadPmapCopy(projectOf(map));
    editor.notify(i18next.t('files.downloaded'), 'success');
  } catch (failure) {
    editor.notify(messageOf(failure), 'error');
  }
}

/**
 * « Enregistrer la carte ouverte (.pmap) » (3.3, lot 4, F-208): the map shown with the maps detailed below it, its sheets as
 * the person sees them and its comments of presentation (.pmap 1.5), saved by the browser under the name of the map in the
 * language shown, then said; an export with its event, as the copy of a project. The file opens as a project, and later
 * inserts as a detailed step of another map.
 */
export async function downloadMapCopy(editor: MapEditor, map: Schema<'MapView'>): Promise<void> {
  try {
    const text = await api.get('/api/maps/{id}/pmap', { path: { id: map.id }, query: { purpose: 'copy' } });
    const language = editor.store.getState().shown?.language ?? i18next.language;
    const name = localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, language);
    download(new Blob([text], { type: 'application/json' }), exportFileName(map.project_code, name, '.pmap'));
    editor.notify(i18next.t('files.map_downloaded'), 'success');
  } catch (failure) {
    editor.notify(messageOf(failure), 'error');
  }
}

/**
 * « Remplacer par un paquet » opens its dialog with the version of the project read now (If-Match of the replacement)
 * and the days its trash keeps the old one; a project that cannot be read is said instead.
 */
export async function askReplacement(editor: MapEditor, map: Schema<'MapView'>): Promise<void> {
  try {
    const [project, trash] = await Promise.all([
      api.get('/api/projects/{id}', { path: { id: map.project_id } }),
      api.get('/api/trash').catch(() => null),
    ]);
    const replaced = { id: project.id, code: project.code, version: project.version, retentionDays: trash?.retention_days ?? DEFAULT_RETENTION_DAYS };
    editor.floating.getState().open({ kind: 'replace', replaced });
  } catch (failure) {
    editor.notify(messageOf(failure), 'error');
  }
}
