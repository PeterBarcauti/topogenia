// documentLocation.ts
// What the line of a document offers of its location: the text its copy button copies (its path, its link, else its name) and the folder the host shows on the desktop mode (ADR 104).

import { api, type Schema } from '../../../api/client.ts';

type Document = Schema<'Document'>;

/** What the copy button of a document copies: its path, its link, or its name when no location was entered. */
export type CopyKind = 'path' | 'link' | 'name';

export function copyKindOf(document: Document): CopyKind {
  if (document.location === null || document.location === '') return 'name';
  return document.location_type === 'path' ? 'path' : 'link';
}

/** The text copied: the location as it was entered, or the name of a document that has none. */
export function copiedTextOf(document: Document): string {
  return document.location === null || document.location === '' ? document.name : document.location;
}

/** True when the folder of the document can be shown: a path, on the desktop mode only (the host has no such route elsewhere). */
export function showsFolder(document: Document, desktop: boolean): boolean {
  return desktop && copyKindOf(document) === 'path';
}

/**
 * Asks the host to show the folder that holds the document. Only the identifiers leave: the host reads the path it
 * keeps, and it never opens the document itself (ADR 104).
 */
export async function showFolder(document: Document): Promise<void> {
  await api.post('/api/steps/{id}/documents/{documentId}/folder', { path: { id: document.step_id, documentId: document.id } });
}
