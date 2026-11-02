// streamBand.test.ts
// The cell aimed on the canvas follows the grid as it is drawn: rows of 156 px under the value stream layer, 120 px without (spec 14.5, audit 5.1).

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { sampleMap } from '../../../../test/maps.ts';
import { STREAM_BAND } from '../../model/geometry/constants.ts';
import { dropCells } from '../../model/moves.ts';
import { cellAimed, contextOf } from './context.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the cell aimed under the value stream layer (audit 5.1)', () => {
  it('a point 125 px down is in the second lane without the layer, still in the first with it', async () => {
    const { editor } = await editorOf(sampleMap());
    const point = { x: 30 + 85, y: 125 };
    const off = contextOf(editor);
    expect(off === null ? null : cellAimed(off, point)).toMatchObject({ laneId: 'l2', row: 0 });
    editor.preferences.getState().set('showValueStream', true);
    const on = contextOf(editor);
    expect(on === null ? null : cellAimed(on, point)).toMatchObject({ laneId: 'l1', row: 0 });
  });

  it('a step dropped with its centre at 230 px lands in the row of the second lane on rows of 156 px, in a new row under it on rows of 120 px', () => {
    const map = sampleMap();
    const dropped = [{ id: 's2', position: { x: 250, y: 230 - 32 } }];
    expect(dropCells(map, dropped, STREAM_BAND)).toEqual([expect.objectContaining({ id: 's2', laneId: 'l2', row: 0 })]);
    expect(dropCells(map, dropped)).toEqual([expect.objectContaining({ id: 's2', laneId: 'l2', row: 1 })]);
  });
});
