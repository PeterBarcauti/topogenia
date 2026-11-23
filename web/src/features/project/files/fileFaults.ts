// fileFaults.ts
// The faults of a .pmap file refused by the host (FICHIER_INVALIDE, A-1.2-19), as the dialogs that send a file list them: the opening, the insertion as a detailed step (F-208).

import { isApiError } from '../../../api/errors.ts';

/** A fault of the file as the host lists it: its place (`map.steps[2]`), its code, its message (A-1.2-19). */
export interface FileFault {
  readonly path: string;
  readonly message: string;
}

/** The faults a refusal of the host lists; none for another failure. */
export function faultsOf(failure: unknown): FileFault[] {
  const list = isApiError(failure) ? failure.details.errors : undefined;
  if (!Array.isArray(list)) return [];
  return list.filter((item): item is FileFault => typeof item === 'object' && item !== null && typeof (item as FileFault).message === 'string');
}
