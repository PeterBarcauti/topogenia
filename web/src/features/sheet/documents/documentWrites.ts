// documentWrites.ts
// The writes of the documents of a sheet, each with the version of what it changes: declare, change and remove a document, use one, stop using it, describe its use, choose the status of its pain point, of a pain point of the step without a document, or of the one a consumer of an output noted (annex B 4, A 2.11, ADR 116, R10).

import { api, type Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { documentBody, type Direction, type DocumentEntry } from '../model/documents.ts';
import type { StatusCode } from '../model/painPointStatus.ts';
import type { SheetValue } from '../SheetContext.tsx';

/**
 * The version of the use of a document by the step, 0 for a document not used yet (ADR 045): read in the sheet last
 * received when the write leaves, so that a write that waited for another one carries the version it gave back (audit 3.1 V3, U1).
 */
function versionOfUse(sheet: SheetValue, documentId: string): number {
  return sheet.received().inputs.find((input) => input.document.id === documentId)?.use?.version ?? 0;
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
  return sheet.runDocument(() => {
    const version = versionOfUse(sheet, documentId);
    return used ? api.put('/api/steps/{id}/uses/{documentId}', { path, body: use, version }) : api.delete('/api/steps/{id}/uses/{documentId}', { path, version });
  });
}

/** The use described: the wait, the share usable as is, the pain point raised on the producer and its time lost (B-4-71 to B-4-76). */
export function describeUse(sheet: SheetValue, documentId: string, use: Schema<'DocumentUseBody'>): Promise<boolean> {
  return tickDocument(sheet, documentId, true, use);
}

/** The version of a pain point in the sheet last received: one a use shows, or one of the step without a document (R10). */
function versionOfPainPoint(sheet: SheetValue, painPointId: string): number {
  const view = sheet.received();
  return [...view.pain_points, ...view.step_pain_points].find((pain) => pain.id === painPointId)?.version ?? 0;
}

/**
 * The status of a pain point chosen in the list (ADR 116), under the step whose sheet this is: one carried by a use of
 * the step, or one raised on the step that no use carries (R10). In the queue of the documents, after the writes of the
 * use asked before it (the pain point may be born of one), with the version of the pain point they gave back. Changed by
 * someone else meanwhile: the refusal is said and the sheet read again, so that the list shows the status it has now.
 */
export function setPainPointStatus(sheet: SheetValue, painPointId: string, status: StatusCode): Promise<boolean> {
  const id = sheet.props.step.id;
  return sheet.runDocument(async () => {
    try {
      return await api.put('/api/steps/{id}/pain-points/{painPointId}/status', { path: { id, painPointId }, body: { status }, version: versionOfPainPoint(sheet, painPointId) });
    } catch (failure) {
      if (isApiError(failure) && failure.status === 409) await readAgain(sheet);
      throw failure;
    }
  });
}

/**
 * The status of the pain point a consumer of an output noted, chosen on the sheet of the producer (R10): written under the
 * step that receives, whose sheet the host answers with; this sheet is read again then, in the same queue.
 */
export function setConsumerPainPointStatus(sheet: SheetValue, consumerId: string, painPointId: string, status: StatusCode): Promise<boolean> {
  return sheet.runDocument(async () => {
    try {
      await api.put('/api/steps/{id}/pain-points/{painPointId}/status', { path: { id: consumerId, painPointId }, body: { status }, version: versionOfPainPoint(sheet, painPointId) });
    } catch (failure) {
      if (isApiError(failure) && failure.status === 409) await readAgain(sheet);
      throw failure;
    }
    return api.get('/api/steps/{id}/sheet', { path: { id: sheet.props.step.id } });
  });
}

/** The sheet read again after a conflict; a failure to read it leaves the sheet as it was, the refusal said already. */
async function readAgain(sheet: SheetValue): Promise<void> {
  try {
    sheet.apply(await api.get('/api/steps/{id}/sheet', { path: { id: sheet.props.step.id } }));
  } catch {
    // The message of the conflict is said by the queue.
  }
}

export const NO_USE: Schema<'DocumentUseBody'> = {
  wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null, action: null, owner: null, due_date: null,
};
