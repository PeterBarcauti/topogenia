// paste.ts
// Copy, cut, paste and duplicate: the block taken to the clipboard, put down in a cell or in the flow, the new steps chosen (annex B 2.7).

import { api, type Schema } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { cellRightOf, extractBlock, insertionCell, laneSpan, placeBlock, type ClipboardContent, type ClipboardMode, type FlowPosition } from '../../model/clipboard.ts';
import type { GridCell } from '../../model/geometry/grid.ts';
import { clipboard, setClipboard } from '../clipboard.ts';
import type { MapEditor } from '../editor.ts';

type MapView = Schema<'MapView'>;

/** Where a paste goes: a cell, or before or after a step in the flow (B-2.7-22). */
export type PasteTarget = { readonly cell: GridCell } | { readonly stepId: string; readonly position: FlowPosition };

/** Copies or cuts the steps given; a cut stays in place, greyed, until the paste (B-2.7-09); not an operation of the history (B-2.6-46). */
export function takeToClipboard(editor: MapEditor, stepIds: readonly string[], mode: ClipboardMode): void {
  const map = editor.store.getState().map;
  const content = map === null ? null : extractBlock(map, stepIds, mode);
  if (content === null) return;
  setClipboard(content);
  editor.notify(i18next.t(mode === 'copy' ? 'map.copied' : 'map.cut', { count: content.steps.length }));
}

/** Escape gives up a cut (B-2.5-24, B-2.7-13). */
export function giveUpCut(): void {
  if (clipboard.getState().content?.mode === 'cut') setClipboard(null);
}

/** The anchor of the flow and the cell of the first step of the block. */
function placeOf(map: MapView, target: PasteTarget): { cell: GridCell; anchor: { step_id: string; position: FlowPosition } | null } | null {
  if ('cell' in target) return { cell: target.cell, anchor: null };
  const step = map.steps.find((candidate) => candidate.id === target.stepId);
  return step === undefined ? null : { cell: insertionCell(step, target.position), anchor: { step_id: step.id, position: target.position } };
}

/** The steps of a cut still alive: on the map shown, or on their map read again when the cut comes from another map (B-2.7-11, B-2.7-14). */
async function liveCut(map: MapView, content: ClipboardContent): Promise<ClipboardContent> {
  const source = content.mapId === map.id ? map : await api.get('/api/maps/{id}', { path: { id: content.mapId } }).then((read) => read.map, () => null);
  const alive = new Set(source?.steps.map((step) => step.id) ?? []);
  return { ...content, steps: content.steps.filter((step) => alive.has(step.key)) };
}

/** The call of a paste: new steps for a copy, the steps themselves moved for a cut. */
function pasteCall(map: MapView, content: ClipboardContent, cell: GridCell, anchor: { step_id: string; position: FlowPosition } | null) {
  const placements = placeBlock(content.steps, map.lanes, cell);
  if (placements === null) return null;
  if (content.mode === 'cut')
    return api.post('/api/maps/{id}/transfer', { path: { id: map.id }, body: { steps: placements.map((place) => ({ id: place.key, lane_id: place.laneId, column: place.column, row: place.row })), cut: true, anchor } });
  const models = new Map(content.steps.map((step) => [step.key, step]));
  const steps = placements.map((place) => ({ key: place.key, label: models.get(place.key)?.label ?? '', type: models.get(place.key)?.type ?? 'task', lane_id: place.laneId, column: place.column, row: place.row }));
  return api.post('/api/maps/{id}/paste', { path: { id: map.id }, body: { steps, flows: [...content.flows], anchor } });
}

/**
 * Pastes the clipboard: a copy makes new steps with new codes (B-2.7-07), a cut moves its steps with their codes, sheets and
 * documents, in this map or another of the project (B-2.7-10, B-2.7-11). The steps pasted are chosen (B-2.3-30).
 */
export async function paste(editor: MapEditor, target: PasteTarget): Promise<void> {
  const content = clipboard.getState().content;
  if (content === null) {
    editor.notify(i18next.t('map.nothing_to_paste'));
    return;
  }
  const refused: { gone?: true; lanes?: number } = {};
  const reply = await editor.write(async (map) => {
    const place = placeOf(map, target);
    const block = content.mode === 'cut' ? await liveCut(map, content) : content;
    if (block.steps.length === 0) refused.gone = true;
    else if (place !== null && placeBlock(block.steps, map.lanes, place.cell) === null) refused.lanes = laneSpan(block.steps);
    return refused.gone === undefined && refused.lanes === undefined && place !== null ? pasteCall(map, block, place.cell, place.anchor) : null;
  });
  if (refused.gone === true) {
    setClipboard(null);
    editor.notify(i18next.t('map.cut_gone'), 'error');
  } else if (refused.lanes !== undefined) editor.notify(i18next.t('map.lanes_missing', { count: refused.lanes }), 'error');
  if (reply === null) return;
  if (content.mode === 'cut') setClipboard(null);
  editor.store.setState({ selection: { steps: reply.ids, flows: [] } });
}

/** Duplicates the steps given just right of their block, without touching the clipboard; the copies are chosen (B-2.7-23, B-2.7-24). */
export async function duplicate(editor: MapEditor, stepIds: readonly string[]): Promise<void> {
  const reply = await editor.write((map) => {
    const block = extractBlock(map, stepIds, 'copy');
    const cell = cellRightOf(map, stepIds);
    return block === null || cell === null ? null : pasteCall(map, block, cell, null);
  });
  if (reply !== null) editor.store.setState({ selection: { steps: reply.ids, flows: [] } });
}
