// wallPrint.test.ts
// The poster of a map (Makigami M9): the printed area of A4 and A3, the pages a length takes with 10 mm shared, the names A1… Z1, AA1, the scale from the pages across and the pages down from the height, the windows that tile the map and share their overlap, the cut marks in the margins and the ticks of the neighbours, the bounds of the pages across, the plan too large left without pages, the pages across offered first.

import {
  ACROSS_MAX, boundedAcross, CUT_MARK, defaultAcross, MM_PER_PIXEL, PAPERS, pagesFor, printedArea, rowName, WALL_FOOTER, WALL_MARGIN, WALL_OVERLAP, WALL_PAGES_MAX, wallPlan,
  type WallPage,
} from './wallPrint.ts';

const close = (actual: number, expected: number) => {
  expect(actual).toBeCloseTo(expected, 6);
};

describe('the printed area and the pages of a length', () => {
  it('is the sheet landscape less its margins and the band of its footer', () => {
    expect([printedArea('a4'), printedArea('a3')]).toEqual([{ width: 277, height: 184 }, { width: 400, height: 271 }]);
    expect(PAPERS.a3.height - 2 * WALL_MARGIN - WALL_FOOTER).toBe(271);
  });

  it('takes one page up to the room, then one more each time the room less the overlap is passed', () => {
    const room = 184;
    expect([pagesFor(1, room), pagesFor(room, room), pagesFor(room + 0.01, room)]).toEqual([1, 1, 2]);
    expect([pagesFor(2 * room - WALL_OVERLAP, room), pagesFor(2 * room - WALL_OVERLAP + 0.01, room), pagesFor(3 * room - 2 * WALL_OVERLAP, room)]).toEqual([2, 3, 3]);
  });

  it('names the rows as a spreadsheet names its columns', () => {
    expect([0, 1, 25, 26, 27, 51, 52, 701, 702].map(rowName)).toEqual(['A', 'B', 'Z', 'AA', 'AB', 'AZ', 'BA', 'ZZ', 'AAA']);
  });

  it('bounds the pages across from 1 to 10, made whole', () => {
    expect([0, 1, 2.6, 10, 11, Number.NaN, -3].map(boundedAcross)).toEqual([1, 1, 3, 10, 10, 1, 1]);
  });
});

describe('the poster of a map', () => {
  it('takes its scale from the pages across: two A3 make 790 mm for 2 000 units, a row high enough for 600', () => {
    const plan = wallPlan({ width: 2000, height: 600 }, 'a3', 2);
    expect([plan.across, plan.down, plan.tooMany]).toEqual([2, 1, false]);
    close(plan.scale, 0.395);
    expect(plan.percent).toBe(Math.round((0.395 / MM_PER_PIXEL) * 100));
    expect(plan.percent).toBe(149);
    close(plan.wall.width, 790);
    close(plan.wall.height, 237);
    expect(plan.pages.map((page) => [page.name, page.number])).toEqual([['A1', 1], ['A2', 2]]);
    const [first, second] = plan.pages as [WallPage, WallPage];
    expect(first.area).toEqual({ x: WALL_MARGIN, y: WALL_MARGIN, width: 400, height: expect.closeTo(237, 6) as number });
    close(first.window.x, 0);
    close(first.window.width, 400 / 0.395);
    close(second.window.x, 390 / 0.395);
    close(second.window.x + second.window.width, 2000);
    expect(first.pixels).toEqual({ width: 2362, height: 1400 });
  });

  it('adds rows down the height, the last one printing what is left; read in the order A1, A2, B1, B2', () => {
    const tall = wallPlan({ width: 1000, height: 1000 }, 'a4', 1);
    expect([tall.across, tall.down]).toEqual([1, 2]);
    const below = tall.pages[1];
    expect(below?.name).toBe('B1');
    close(below?.window.y ?? 0, 174 / 0.277);
    close(below?.area.height ?? 0, 277 - 174);
    close((below?.window.y ?? 0) + (below?.window.height ?? 0), 1000);
    const square = wallPlan({ width: 1000, height: 600 }, 'a4', 2);
    expect(square.pages.map((page) => `${page.name}:${String(page.row)}${String(page.column)}`)).toEqual(['A1:00', 'A2:01', 'B1:10', 'B2:11']);
  });

  it('tiles the whole map, each window sharing the overlap with the next one, never past the map', () => {
    for (const [map, paper, across] of [[{ width: 5321, height: 1777 }, 'a3', 7], [{ width: 900, height: 2600 }, 'a4', 3], [{ width: 12_000, height: 900 }, 'a4', 10]] as const) {
      const plan = wallPlan(map, paper, across);
      const shared = WALL_OVERLAP / plan.scale;
      for (const page of plan.pages) {
        const right = plan.pages.find((other) => other.row === page.row && other.column === page.column + 1);
        if (right !== undefined) close(page.window.x + page.window.width - right.window.x, shared);
        const below = plan.pages.find((other) => other.row === page.row + 1 && other.column === page.column);
        if (below !== undefined) close(page.window.y + page.window.height - below.window.y, shared);
        expect(page.window.x + page.window.width).toBeLessThanOrEqual(map.width + 1e-6);
        expect(page.window.y + page.window.height).toBeLessThanOrEqual(map.height + 1e-6);
        // The last row prints more than its overlap: one row less would not have been enough.
        expect(page.area.height).toBeGreaterThan(WALL_OVERLAP);
      }
      const last = plan.pages.at(-1);
      close((last?.window.x ?? 0) + (last?.window.width ?? 0), map.width);
      close((last?.window.y ?? 0) + (last?.window.height ?? 0), map.height);
      expect(plan.pages).toHaveLength(plan.across * plan.down);
    }
  });

});

describe('the marks, the bounds and the first offer of a poster', () => {
  it('puts the cut marks in the margins at the corners of the area and the ticks where the right and lower neighbours start', () => {
    const plan = wallPlan({ width: 1000, height: 600 }, 'a4', 2);
    const [first, , , last] = plan.pages as [WallPage, WallPage, WallPage, WallPage];
    expect(first.marks).toHaveLength(12);
    expect(last.marks).toHaveLength(8);
    expect(first.marks[0]).toEqual({ x1: WALL_MARGIN - CUT_MARK.gap - CUT_MARK.length, y1: WALL_MARGIN, x2: WALL_MARGIN - CUT_MARK.gap, y2: WALL_MARGIN });
    const ticks = first.marks.slice(8);
    const x = WALL_MARGIN + first.area.width - WALL_OVERLAP;
    expect(ticks.slice(0, 2).every((tick) => tick.x1 === x && tick.x2 === x)).toBe(true);
    const y = WALL_MARGIN + first.area.height - WALL_OVERLAP;
    expect(ticks.slice(2).every((tick) => tick.y1 === y && tick.y2 === y)).toBe(true);
    for (const page of plan.pages) {
      const { area } = page;
      for (const mark of page.marks) {
        const [left, right, top, bottom] = [Math.min(mark.x1, mark.x2), Math.max(mark.x1, mark.x2), Math.min(mark.y1, mark.y2), Math.max(mark.y1, mark.y2)];
        const outside = right <= area.x - CUT_MARK.gap || left >= area.x + area.width + CUT_MARK.gap || bottom <= area.y - CUT_MARK.gap || top >= area.y + area.height + CUT_MARK.gap;
        expect(outside).toBe(true);
        expect(left >= 0 && right <= PAPERS.a4.width && top >= 0 && bottom <= PAPERS.a4.height - WALL_MARGIN).toBe(true);
      }
    }
  });

  it('lays out no page past the most a print holds, its counts given', () => {
    const plan = wallPlan({ width: 100, height: 10_000 }, 'a3', ACROSS_MAX);
    expect(plan.tooMany).toBe(true);
    expect(plan.pages).toEqual([]);
    expect(plan.across * plan.down).toBeGreaterThan(WALL_PAGES_MAX);
    expect(wallPlan({ width: 1000, height: 0 }, 'a4', 1).pages).toHaveLength(1);
  });

  it('offers first the fewest pages across that print the map at least as large as shown at 100 %, ten at most', () => {
    expect(defaultAcross({ width: 2000, height: 600 }, 'a3')).toBe(2);
    expect(wallPlan({ width: 2000, height: 600 }, 'a3', 1).percent).toBeLessThan(100);
    expect(defaultAcross({ width: 500, height: 300 }, 'a4')).toBe(1);
    expect(defaultAcross({ width: 50_000, height: 900 }, 'a3')).toBe(ACROSS_MAX);
  });
});
