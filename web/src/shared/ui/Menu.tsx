// Menu.tsx
// A context menu drawn from commands: icon, label, shortcut on the right, red actions, separators, choices in named groups (a list or swatches); kept in the window; arrows, Escape, a click outside; the focus given back (spec 9.5, annex B 2.4, ARIA 1.2).

import { useEffect, useLayoutEffect, useRef, type KeyboardEvent, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import type { CommandItem } from '../commands/commands.ts';
import { useKeyLayer } from '../keyboard/KeyboardContext.tsx';
import { noteKeyboard } from './inputModality.ts';
import { blocksOf, rowsOf, type MenuGroup } from './menuBlocks.ts';
import { MenuEntry, MenuGroupView } from './MenuParts.tsx';
import { fitInWindow, menuPlacement } from './menuPlacement.ts';

export type { MenuGroup } from './menuBlocks.ts';

export interface MenuProps {
  /** The i18n key of the name of the menu, for assistive technologies. */
  readonly label: string;
  readonly testId: string;
  readonly items: readonly CommandItem[];
  /** The runs of choices drawn as named groups (the type and the colour of a lane, the type of a step); none by default. */
  readonly groups?: readonly MenuGroup[];
  /** The point of the window where the menu opens (the pointer of the right click). */
  readonly at: { readonly x: number; readonly y: number };
  readonly onRun: (id: string) => void;
  readonly onClose: () => void;
}

const ITEMS = '[role="menuitem"]:not([aria-disabled="true"]), [role="menuitemradio"]:not([aria-disabled="true"])';

/** The next enabled item from the focused one, in a direction, round the end. */
function move(menu: HTMLElement, step: 1 | -1): void {
  const items = [...menu.querySelectorAll<HTMLElement>(ITEMS)];
  if (items.length === 0) return;
  const index = items.indexOf(document.activeElement as HTMLElement);
  items[(index + step + items.length) % items.length]?.focus();
}

/** The direction a key moves the focus in: up and down everywhere, left and right in a grid of swatches too. */
function directionOf(event: KeyboardEvent<HTMLDivElement>): 1 | -1 | null {
  if (event.key === 'ArrowDown') return 1;
  if (event.key === 'ArrowUp') return -1;
  const inGrid = event.target instanceof Element && event.target.closest('.menu-swatches') !== null;
  if (inGrid && event.key === 'ArrowRight') return 1;
  return inGrid && event.key === 'ArrowLeft' ? -1 : null;
}

/** Gives the focus back to the element that held it when the menu opened, still in the page. */
const focusBack = (opener: RefObject<HTMLElement | null>) => {
  if (opener.current?.isConnected === true) opener.current.focus();
};

/**
 * The first enabled item takes the focus at the opening (B-2.4-03). The focus goes back to what held it before when an
 * entry runs (so that what the entry opens finds it there: a field, a panel opened from the keyboard) and when Escape or
 * Tab closes the menu; a click outside leaves it where the click put it (NF08, WAI-ARIA).
 */
function useFocus(menu: RefObject<HTMLDivElement | null>, onClose: () => void): RefObject<HTMLElement | null> {
  const opener = useRef<HTMLElement | null>(null);
  useEffect(() => {
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    menu.current?.querySelector<HTMLElement>(ITEMS)?.focus();
    const outside = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || menu.current?.contains(event.target) !== true) onClose();
    };
    document.addEventListener('pointerdown', outside, true);
    return () => {
      document.removeEventListener('pointerdown', outside, true);
    };
  }, [menu, onClose]);
  return opener;
}

export function Menu({ label, testId, items, groups = [], at, onRun, onClose }: MenuProps) {
  const { t } = useTranslation();
  const menu = useRef<HTMLDivElement>(null);
  const blocks = blocksOf(items, groups);
  // A floating element: the shortcuts of the canvas are silent; a key pressed in it tells that what an entry opens takes the focus.
  useKeyLayer('modal', (event) => {
    if (event.type === 'keydown') noteKeyboard();
    return false;
  });
  // The rows of 34 px place the menu before it is drawn; drawn, its groups and its long labels may make it higher: it goes up as much.
  useLayoutEffect(() => {
    const top = menu.current === null ? null : fitInWindow(menu.current.getBoundingClientRect(), window.innerHeight);
    if (menu.current !== null && top !== null) menu.current.style.top = `${String(top)}px`;
  }, [items]);
  const opener = useFocus(menu, onClose);
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const direction = directionOf(event);
    if (event.key === 'Escape' || event.key === 'Tab') {
      focusBack(opener);
      onClose();
    } else if (direction !== null) move(event.currentTarget, direction);
    else return;
    event.preventDefault();
    event.stopPropagation();
  };
  const choose = (item: CommandItem) => {
    if (item.disabled) return;
    focusBack(opener);
    onClose();
    onRun(item.id);
  };
  const place = menuPlacement(at, rowsOf(blocks), { width: window.innerWidth, height: window.innerHeight });
  return (
    <div ref={menu} className="menu" role="menu" aria-label={t(label)} data-testid={testId} style={{ left: place.x, top: place.y }} onKeyDown={onKeyDown}>
      {blocks.map((block) => ('group' in block
        ? <MenuGroupView key={block.group.name} group={block.group} items={block.items} testId={testId} onChoose={choose} />
        : <MenuEntry key={block.item.id} item={block.item} testId={testId} onChoose={choose} />))}
    </div>
  );
}
