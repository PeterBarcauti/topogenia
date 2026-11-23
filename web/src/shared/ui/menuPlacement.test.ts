// menuPlacement.test.ts
// Where a submenu stands: to the right of its menu, to the left when it would leave the window, in the height of the window (ADR 117).

import { submenuPlacement } from './menuPlacement.ts';

const WINDOW = { width: 1000, height: 700 };
const MENU = { left: 100, right: 340 };

describe('the place of a submenu', () => {
  it('to the right of its menu, a little over it, its first entry on its row', () => {
    expect(submenuPlacement({ ...MENU, top: 200 }, { width: 240, height: 300 }, WINDOW)).toEqual({ x: 338, y: 195, maxHeight: 497 });
  });

  it('to the left when it would leave the window on the right', () => {
    expect(submenuPlacement({ left: 700, right: 940, top: 200 }, { width: 240, height: 300 }, WINDOW).x).toBe(462);
  });

  it('never beyond the left edge of the window', () => {
    expect(submenuPlacement({ left: 10, right: 250, top: 200 }, { width: 240, height: 300 }, { width: 400, height: 700 }).x).toBe(0);
  });

  it('moved up so that its bottom stays in the window', () => {
    expect(submenuPlacement({ ...MENU, top: 600 }, { width: 240, height: 300 }, WINDOW).y).toBe(384);
  });

  it('higher than the window: at the top, scrolling in the height of the window', () => {
    expect(submenuPlacement({ ...MENU, top: 300 }, { width: 240, height: 900 }, WINDOW)).toMatchObject({ y: 8, maxHeight: 684 });
  });
});
