// commandBar.ts
// The flows typed in the command bar, sent all or nothing through the writes of the map; a refusal kept for the bar, line by line (annex B 2.13, A-2.6-28).

import { api } from '../../../../api/client.ts';
import { isApiError } from '../../../../api/errors.ts';
import { messageOf } from '../../../../shared/i18n/errorMessage.ts';
import type { MapEditor } from '../editor.ts';

/** What the bar shows after sending: the number of flows drawn, or the message of the refusal and those of its lines. */
export type CommandOutcome =
  | { readonly ok: true; readonly count: number }
  | { readonly ok: false; readonly message: string; readonly lines: readonly string[] };

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null;

/** The messages of the lines refused, in their order (« L2 : ... »), from the details of COMMANDE_ERREURS; none for another failure. */
export function lineMessages(failure: unknown): string[] {
  const errors = isApiError(failure) ? failure.details.errors : undefined;
  if (!Array.isArray(errors)) return [];
  return errors.flatMap((error: unknown) => (isRecord(error) && typeof error.message === 'string' ? [error.message] : []));
}

/**
 * Sends the text of the bar after the writes before it (B-2.6-09) and applies the map of the reply. A failure is not
 * notified: the bar keeps it and stays open (B-2.13-05); a conflict reads the map again.
 */
export async function runCommandBar(editor: MapEditor, text: string): Promise<CommandOutcome> {
  const refused: { outcome: CommandOutcome | null } = { outcome: null };
  const reply = await editor.write(async (map) => {
    try {
      return await api.post('/api/maps/{id}/command', { path: { id: map.id }, body: { text } });
    } catch (failure) {
      refused.outcome = { ok: false, message: messageOf(failure), lines: lineMessages(failure) };
      if (isApiError(failure) && failure.status === 409) await editor.store.getState().load(map.id);
      return null;
    }
  });
  if (reply !== null) return { ok: true, count: reply.ids.length };
  return refused.outcome ?? { ok: false, message: '', lines: [] };
}
