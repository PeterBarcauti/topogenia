// menuPlacement.ts
// Where a menu stands so that it stays in the window: 240 px wide, rows of 34 px for the reckoning, as the 1.x placed it (B-2.4-01).

/** The width of a menu and the height of a row, the values of `--menu-width` and `--menu-row-height` (spec 9.5). */
export const MENU_WIDTH = 240;
export const MENU_ROW_HEIGHT = 34;

/** The space kept between a menu and the bottom or the top of the window. */
const BOTTOM_GAP = 16;
const TOP_GAP = 8;

/**
 * The top of a menu drawn whose bottom leaves the window: moved up as much, never above the top (it scrolls when it is
 * higher than the window); null when it stays as it is.
 */
export function fitInWindow(drawn: { readonly top: number; readonly bottom: number; readonly height: number }, windowHeight: number): number | null {
  if (drawn.bottom <= windowHeight - TOP_GAP) return null;
  return Math.max(TOP_GAP, windowHeight - drawn.height - TOP_GAP);
}

/**
 * The top left corner of a menu of `rows` rows opened at a point: moved left so that it does not leave the window on the
 * right, moved up so that its rows stay above the bottom, never above the top (1.x `MenuContextuel`).
 */
export function menuPlacement(at: { readonly x: number; readonly y: number }, rows: number, window: { readonly width: number; readonly height: number }): { x: number; y: number } {
  return {
    x: Math.max(0, Math.min(at.x, window.width - MENU_WIDTH)),
    y: Math.max(TOP_GAP, Math.min(at.y, window.height - rows * MENU_ROW_HEIGHT - BOTTOM_GAP)),
  };
}
