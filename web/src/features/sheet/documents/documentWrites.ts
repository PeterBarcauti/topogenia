// documentWrites.ts
// The writes of the documents of a sheet, each with the version of what it changes: declare, change and remove a document, use one, stop using it, describe its use (annex B 4, A 2.11).

import { api, type Schema } from '../../../api/client.ts';
import { documentBody, type Direction, type DocumentEntry } from '../model/documents.ts';
import type { SheetValue } from '../SheetContext.tsx';

/** The version of the use of a document by the step, 0 for a document not used yet (ADR 045). */
function versionOfUse(sheet: SheetValue, documentId: string): number {
  return sheet.view.inputs.find((input) => input.document.id === documentId)?.use?.version ?? 0;
}

/** A document declared by the step: added, or changed with the version read (B-4-66 to B-4-69, B-4-78). */
export function saveDocument(sheet: SheetValue, direction: Direction, entry: DocumentEntry): Promise<boolean> {
  const id = sheet.props.step.id;
  const body = documentBody(direction, entry);
  return sheet.runDocument(() => (entry.id === null
    ? api.post('/api/steps/{id}/documents', { path: { id }, body })
    : api.put('/api/steps/{id}/documents/{documentId}', { path: { id, documentId: entry.id }, body, version: entry.version })));
}

export function removeDocument(sheet: SheetValue, document: Schema<'Document'>): Promise<boolean> {
  return sheet.runDocument(() => api.delete('/api/steps/{id}/documents/{documentId}', { path: { id: sheet.props.step.id, documentId: document.id }, version: document.version }));
}

/**
 * Ticks or unticks a document produced by another step: the step uses it, the flow carries it (B-4-63), or not any
 * more. A tick sends the use as it was, empty for a new one.
 */
export function tickDocument(sheet: SheetValue, documentId: string, used: boolean, use: Schema<'DocumentUseBody'>): Promise<boolean> {
  const path = { id: sheet.props.step.id, documentId };
  const version = versionOfUse(sheet, documentId);
  return sheet.runDocument(() => (used ? api.put('/api/steps/{id}/uses/{documentId}', { path, body: use, version }) : api.delete('/api/steps/{id}/uses/{documentId}', { path, version })));
}

/** The use described: the wait, the share usable as is, the pain point raised on the producer and its time lost (B-4-71 to B-4-76). */
export function describeUse(sheet: SheetValue, documentId: string, use: Schema<'DocumentUseBody'>): Promise<boolean> {
  return tickDocument(sheet, documentId, true, use);
}

export const NO_USE: Schema<'DocumentUseBody'> = { wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null };
