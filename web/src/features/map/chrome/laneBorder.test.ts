// laneBorder.test.ts
// The aim of a border of lanes pulled (ADR 096): the lane and its end, a row each height of a row at the zoom of the view, within the room the lane had when the pull started, never the rows drawn meanwhile.

import { sampleMap, stepOf } from '../../../test/maps.ts';
import { layoutMap } from '../model/mapLayout.ts';
import { estimateMeasure } from '../model/textSize.ts';
import { pullAim } from './laneBorder.ts';

const lanesOf = (map: ReturnType<typeof sampleMap>, rows = {}) => layoutMap(map, 'fr', estimateMeasure, null, rows).lanes;

describe('a border of lanes pulled (ADR 096)', () => {
  it('down, the lane above at its bottom; up, the lane below at its top; a row each height of a row pulled; nothing past the ends', () => {
    const map = sampleMap();
    const lanes = lanesOf(map);
    expect([pullAim(map, {}, lanes, 1, 1, 200), pullAim(map, {}, lanes, 1, 1, -30)]).toEqual([
      { laneId: 'l1', end: 'bottom', count: 2 }, { laneId: 'l2', end: 'top', count: 1 },
    ]);
    expect([pullAim(map, {}, lanes, 1, 0, 50), pullAim(map, {}, lanes, 1, 2, -50), pullAim(map, {}, lanes, 1, 1, 3)]).toEqual([null, null, null]);
  });

  it('at the zoom of the view, and with the band of the value stream layer: rows of their height on the screen', () => {
    const map = sampleMap();
    expect(pullAim(map, {}, lanesOf(map), 0.5, 1, 100)?.count).toBe(2);
    const banded = layoutMap(map, 'fr', estimateMeasure, { language: 'fr', workdayHours: 7, t: (key: string) => key }).lanes;
    expect([pullAim(map, {}, banded, 1, 1, 150)?.count, pullAim(map, {}, banded, 1, 1, 250)?.count]).toEqual([1, 2]);
  });

  it('within the room of the lane before the pull, whatever the lanes drawn meanwhile show: none when it is full', () => {
    const map = sampleMap({ steps: [...sampleMap().steps, stepOf('low', 'l1', 4, { row: 48 })] });
    expect(pullAim(map, {}, lanesOf(map), 1, 1, 600)?.count).toBe(2);
    expect(pullAim(map, {}, lanesOf(map, { l1: 51 }), 1, 1, 600)?.count).toBe(2);
    expect(pullAim(map, { l1: 51 }, lanesOf(map, { l1: 51 }), 1, 1, 600)).toBeNull();
  });
});
