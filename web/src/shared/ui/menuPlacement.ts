// menuPlacement.ts
// Where a menu stands so that it stays in the window: 240 px wide, rows of 34 px for the reckoning, as the 1.x placed it (B-2.4-01); a menu of the header under its button, scrolling when it is higher than the room under it.

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

/**
 * The top left corner and the greatest height of a menu opened under a button of the header (the File menu, the account,
 * the help): it stays under its button, moved left only to stay in the window, and scrolls in the room left under the
 * button when it is higher (lot 1 of the 3.4); a context menu is placed by {@link menuPlacement}.
 */
export function menuUnder(at: { readonly x: number; readonly y: number }, window: { readonly width: number; readonly height: number }): { x: number; y: number; maxHeight: number } {
  return {
    x: Math.max(0, Math.min(at.x, window.width - MENU_WIDTH)),
    y: at.y,
    maxHeight: Math.max(MENU_ROW_HEIGHT, window.height - at.y - TOP_GAP),
  };
}

/** The part of a submenu over its menu, so that the pointer does not cross a gap, and the room between its top and the top of its first entry (padding and border). */
const SUBMENU_OVERLAP = 2;
const SUBMENU_FIRST_ENTRY = 5;

/**
 * The top left corner and the greatest height of a submenu (ADR 117): to the right of its menu, a little over it, its first
 * entry on its row; to the left when it would leave the window on the right; moved up so that its bottom stays in the
 * window, and scrolling in the height of the window when it is higher.
 */
export function submenuPlacement(anchor: { readonly left: number; readonly right: number; readonly top: number }, size: { readonly width: number; readonly height: number },
  window: { readonly width: number; readonly height: number }): { x: number; y: number; maxHeight: number } {
  const right = anchor.right - SUBMENU_OVERLAP;
  const x = right + size.width <= window.width ? right : Math.max(0, anchor.left - size.width + SUBMENU_OVERLAP);
  const y = Math.max(TOP_GAP, Math.min(anchor.top - SUBMENU_FIRST_ENTRY, window.height - size.height - BOTTOM_GAP));
  return { x, y, maxHeight: window.height - y - TOP_GAP };
}
