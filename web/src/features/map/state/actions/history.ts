// history.ts
// Undo and redo of the last operation of the user on the map, through the writes of the map: the history of the host, the message of the operation, the refusals (annex B 2.6, AR-05, AR-108, AR-121).

import { api } from '../../../../api/client.ts';
import { isApiError } from '../../../../api/errors.ts';
import { messageOf } from '../../../../shared/i18n/errorMessage.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { MapEditor } from '../editor.ts';
import type { MapState } from '../mapStore.ts';

/** Undo takes the last operation back, redo plays the last one undone again. */
export type Replay = 'undo' | 'redo';

/** The buttons: an operation to undo or to redo in the history of the host, and none running (B-2.1-24, B-2.6-06). */
export const canReplay = (state: MapState, which: Replay): boolean => !state.replaying && (state.history?.[which].length ?? 0) > 0;

/** The undos and redos asked by an editor and not answered yet. */
const running = new WeakMap<MapEditor, number>();

function count(editor: MapEditor, step: 1 | -1): void {
  const value = (running.get(editor) ?? 0) + step;
  running.set(editor, value);
  editor.store.setState({ replaying: value > 0 });
}

/**
 * Undoes or redoes after the writes asked before (B-2.6-09): at its turn, the history they left says whether there is
 * something to take back, and nothing is sent when there is not. A refusal of the host (409: OPERATION_DEPASSEE,
 * CARTE_MODIFIEE) shows its message and the map is read again with its stacks, cleared by the host (AR-05); an empty
 * stack on the host (INTROUVABLE) is silent (AR-108); another failure says the operation could not be replayed (B-2.6-49).
 * In both last cases the map and its history are read again; the host keeps its stack (AR-121).
 */
export async function replay(editor: MapEditor, which: Replay): Promise<void> {
  count(editor, 1);
  try {
    const reply = await editor.write(async (map) => {
      if ((editor.store.getState().history?.[which].length ?? 0) === 0) return null;
      try {
        return await (which === 'undo' ? api.post('/api/maps/{id}/undo', { path: { id: map.id } }) : api.post('/api/maps/{id}/redo', { path: { id: map.id } }));
      } catch (failure) {
        if (isApiError(failure) && failure.status === 409) throw failure;
        if (!isApiError(failure, 'INTROUVABLE')) editor.notify(i18next.t(`history.${which}_failed`, { reason: messageOf(failure) }), 'error');
        await editor.store.getState().load(map.id);
        return null;
      }
    });
    // The code of the reply is the kind of the operation (1.x `historique.action.*`): « Annulé : déplacement » (B-2.6-15).
    if (reply?.code != null) editor.notify(i18next.t(`history.${which}_done`, { action: i18next.t(`history.actions.${reply.code}`) }));
  } finally {
    count(editor, -1);
  }
}
