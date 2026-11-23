// menuKeys.ts
// The entries a key moves over in a menu, those of that menu only (not those of a submenu it holds), and the move itself (spec 9.5, ARIA 1.2, ADR 117).

/** The entries a key can reach: enabled items and choices. */
export const ENTRIES = '[role="menuitem"]:not([aria-disabled="true"]), [role="menuitemradio"]:not([aria-disabled="true"])';

/** The enabled entries of a menu, in the order of the page, without those of the submenus it holds. */
export const entriesOf = (menu: HTMLElement): HTMLElement[] => [...menu.querySelectorAll<HTMLElement>(ENTRIES)].filter((entry) => entry.closest('[role="menu"]') === menu);

/** The next enabled entry from the focused one, in a direction, round the end. */
export function move(menu: HTMLElement, step: 1 | -1): void {
  const entries = entriesOf(menu);
  if (entries.length === 0) return;
  const index = entries.indexOf(document.activeElement as HTMLElement);
  entries[(index + step + entries.length) % entries.length]?.focus();
}
