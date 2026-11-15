// layout.ts
// The writes of the places of the steps: a drag, the arrows of the keyboard, the Align wand (annex B 2.3, 2.5, 2.8).

import { api } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { alignMap } from '../../model/alignment.ts';
import { cellBody, keyMoves, landsOnFreeCells, usefulMoves, type StepCell } from '../../model/moves.ts';
import type { MapEditor, Write } from '../editor.ts';

/**
 * The moves that change something, sorted by column (B-2.3-24): a block of several steps on free and distinct cells in
 * one layout, as it is (B-2.3-25); otherwise one grouped move, the host shifting what is in the way (AR-44, B-2.3-26).
 */
const sendMoves = (moves: (map: Parameters<Write>[0]) => readonly StepCell[] | null): Write => (map) => {
  const useful = usefulMoves(map, moves(map) ?? []);
  if (useful.length === 0) return null;
  const steps = useful.map(cellBody);
  return landsOnFreeCells(map, useful)
    ? api.put('/api/maps/{id}/layout', { path: { id: map.id }, body: { lane_order: null, steps, purpose: 'move' } })
    : api.post('/api/maps/{id}/move', { path: { id: map.id }, body: { steps } });
};

/** The steps dropped after a drag, in the cells reckoned from the map shown. */
export async function moveSteps(editor: MapEditor, moves: readonly StepCell[]): Promise<void> {
  await editor.write(sendMoves(() => moves));
}

/** The chosen steps moved by an arrow of the keyboard; nothing when the move is refused (B-2.5-18 to B-2.5-20). */
export async function moveByKeys(editor: MapEditor, stepIds: readonly string[], columns: number, lanes: number): Promise<void> {
  await editor.write(sendMoves((map) => keyMoves(map, stepIds, columns, lanes)));
}

/** The Align wand: the steps that move in one layout named `align`, and the count said; « already aligned » when none moves (B-2.8-15 to B-2.8-17). */
export async function align(editor: MapEditor): Promise<void> {
  let moved = -1;
  const reply = await editor.write((map) => {
    const cells = alignMap(map);
    moved = cells.length;
    return cells.length === 0 ? null : api.put('/api/maps/{id}/layout', { path: { id: map.id }, body: { lane_order: null, steps: cells.map(cellBody), purpose: 'align' } });
  });
  if (moved === 0) editor.notify(i18next.t('map.already_aligned'), 'info');
  else if (reply !== null) editor.notify(i18next.t('map.aligned', { count: moved }), 'success');
}
