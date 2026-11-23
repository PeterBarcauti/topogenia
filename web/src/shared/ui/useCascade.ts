// useCascade.ts
// The state of the submenus of a menu: the one that is open and whether it takes the focus, the short delay of the hover, the focus given back to its row (ADR 117).

import { useCallback, useEffect, useRef, useState, type FocusEvent, type PointerEvent, type RefObject } from 'react';

/** The delay before the hover of a row opens its submenu, as the menus of Windows wait. */
export const SUBMENU_DELAY = 150;

/** The submenu open, and whether it takes the focus on its first entry (a click or a key) or leaves it where it is (the hover). */
export interface CascadeState {
  readonly id: string;
  readonly focus: boolean;
}

export interface Cascade {
  readonly open: CascadeState | null;
  readonly show: (id: string, focus: boolean) => void;
  /** Closes the open submenu; the focus goes back to its row when asked, or when it was in the submenu. */
  readonly close: (back: boolean) => void;
  readonly onPointerOver: (event: PointerEvent<HTMLDivElement>) => void;
  readonly onFocus: (event: FocusEvent<HTMLDivElement>) => void;
}

const ROWS = '[role="menuitem"], [role="menuitemradio"]';

export function useCascade(menu: RefObject<HTMLDivElement | null>): Cascade {
  const [open, setOpen] = useState<CascadeState | null>(null);
  const current = useRef<CascadeState | null>(null);
  const timer = useRef<number | undefined>(undefined);
  const cancel = useCallback(() => {
    window.clearTimeout(timer.current);
    timer.current = undefined;
  }, []);
  useEffect(() => cancel, [cancel]);
  const set = useCallback((next: CascadeState | null) => {
    current.current = next;
    setOpen(next);
  }, []);
  const show = useCallback((id: string, focus: boolean) => {
    cancel();
    set({ id, focus });
  }, [cancel, set]);
  const close = useCallback((back: boolean) => {
    cancel();
    const opened = current.current;
    if (opened === null) return;
    const inside = document.activeElement?.closest('[role="menu"]') !== menu.current;
    if (back || inside) menu.current?.querySelector<HTMLElement>(`[data-cascade="${opened.id}"]`)?.focus();
    set(null);
  }, [cancel, menu, set]);
  const onPointerOver = useCallback((event: PointerEvent<HTMLDivElement>) => {
    const row = event.target instanceof Element ? event.target.closest<HTMLElement>(ROWS) : null;
    if (row === null) return;
    if (row.closest('[role="menu"]') !== event.currentTarget) {
      cancel();
      return;
    }
    const id = row.dataset.cascade;
    if (id !== undefined && current.current?.id === id) return;
    cancel();
    if (current.current !== null) close(false);
    if (id !== undefined) {
      timer.current = window.setTimeout(() => {
        set({ id, focus: false });
      }, SUBMENU_DELAY);
    }
  }, [cancel, close, set]);
  const onFocus = useCallback((event: FocusEvent<HTMLDivElement>) => {
    const target = event.target;
    if (current.current === null || target.closest('[role="menu"]') !== event.currentTarget) return;
    if (target.dataset.cascade !== current.current.id) close(false);
  }, [close]);
  return { open, show, close, onPointerOver, onFocus };
}
