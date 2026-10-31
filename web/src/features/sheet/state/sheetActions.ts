// sheetActions.ts
// What the sheet panel sends besides its typing: the validation after the last saving, the request of a clarification, changed or removed, the person responsible (annex B 4, spec 3.0 section 16).

import { api } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import type { SheetValue } from '../SheetContext.tsx';

/** Runs a write of the sheet; a refusal is said and gives false. */
async function attempt(sheet: SheetValue, write: () => Promise<void>): Promise<boolean> {
  try {
    await write();
    return true;
  } catch (failure) {
    sheet.props.notify(messageOf(failure), 'error');
    return false;
  }
}

/**
 * Validates the sheet: what waits is saved first; when that saving failed, nothing is validated, since the host would
 * fix another content than the one shown (B-4-53). The message of the failure is already said.
 */
export async function validateSheet(sheet: SheetValue): Promise<boolean> {
  await sheet.save.flush();
  if (sheet.save.failed()) return false;
  return attempt(sheet, async () => {
    sheet.apply(await api.post('/api/steps/{id}/sheet/validate', { path: { id: sheet.props.step.id }, version: sheet.save.version() }));
  });
}

/** The facilitator asks the author a question on a validated sheet, which goes « à revoir » (B-4-56, B-4-57). */
export function askClarification(sheet: SheetValue, text: string): Promise<boolean> {
  return attempt(sheet, async () => {
    sheet.apply(await api.post('/api/steps/{id}/sheet/clarification', { path: { id: sheet.props.step.id }, body: { text: text.trim() }, version: sheet.save.version() }));
  });
}

/** The facilitator rewrites the question asked; the state of the sheet stays (spec 3.0 section 16). */
export function changeClarification(sheet: SheetValue, text: string): Promise<boolean> {
  return attempt(sheet, async () => {
    sheet.apply(await api.put('/api/steps/{id}/sheet/clarification', { path: { id: sheet.props.step.id }, body: { text: text.trim() }, version: sheet.save.version() }));
  });
}

/** The facilitator removes the question asked; a sheet « à revoir » is validated again (spec 3.0 section 16). */
export function withdrawClarification(sheet: SheetValue): Promise<boolean> {
  return attempt(sheet, async () => {
    sheet.apply(await api.delete('/api/steps/{id}/sheet/clarification', { path: { id: sheet.props.step.id }, version: sheet.save.version() }));
  });
}

/** Gives the sheet to a member, or to nobody in particular, through the map; then reads the sheet again for its person responsible (B-4-15). */
export async function assignSheet(sheet: SheetValue, userId: string | null): Promise<void> {
  if (!await sheet.props.onAssign(userId)) return;
  await attempt(sheet, async () => {
    sheet.apply(await api.get('/api/steps/{id}/sheet', { path: { id: sheet.props.step.id } }));
  });
}
