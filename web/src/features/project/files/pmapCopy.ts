// pmapCopy.ts
// Downloads the .pmap copy of a project, named CODE_Short-name_yyyy-mm-dd.pmap (B-1.6-23, B-1.6-24, A-1.2-16).

import { api } from '../../../api/client.ts';
import { download, exportFileName } from '../../../shared/download.ts';

/** Reads the copy as text and gives it to the browser; the sheets only on option, off by default (ADR 054). Gives the file name. */
export async function downloadPmapCopy(project: { readonly id: string; readonly code: string; readonly name: string }, withSheets = false): Promise<string> {
  const text = await api.get('/api/projects/{id}/pmap', { path: { id: project.id }, query: { with_sheets: String(withSheets) } });
  const name = exportFileName(project.code, project.name, '.pmap');
  download(new Blob([text], { type: 'application/json' }), name);
  return name;
}
