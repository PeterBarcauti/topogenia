// Menu.tsx
// A context menu drawn from commands: icon, label, shortcut on the right, red actions, separators, choices in named groups (a list or swatches), submenus opened from a row; kept in the window, a menu of the header under its button; arrows, Escape, a click outside; the focus given back (spec 9.5, annex B 2.4, ARIA 1.2, ADR 117).

import { useEffect, useLayoutEffect, useRef, type KeyboardEvent, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import type { CommandItem } from '../commands/commands.ts';
import { useKeyLayer } from '../keyboard/KeyboardContext.tsx';
import { noteKeyboard } from './inputModality.ts';
import { blocksOf, rowsOf, type MenuGroup, type MenuSubmenu } from './menuBlocks.ts';
import { ENTRIES, move } from './menuKeys.ts';
import { MenuCascade } from './MenuCascade.tsx';
import { MenuEntry, MenuGroupView } from './MenuParts.tsx';
import { fitInWindow, menuPlacement, menuUnder } from './menuPlacement.ts';
import { useCascade } from './useCascade.ts';

export type { MenuGroup, MenuSubmenu } from './menuBlocks.ts';

export interface MenuProps {
  /** The i18n key of the name of the menu, for assistive technologies. */
  readonly label: string;
  /** The values of that name (the code of a pain point). */
  readonly labelValues?: Readonly<Record<string, string | number>>;
  readonly testId: string;
  readonly items: readonly CommandItem[];
  /** The runs of choices drawn as named groups (the type and the colour of a lane, the type of a step); none by default. */
  readonly groups?: readonly MenuGroup[];
  /** The runs of items opened from a row marked by a chevron, each in a menu of its own; none by default. */
  readonly submenus?: readonly MenuSubmenu[];
  /** The point of the window where the menu opens (the pointer of the right click). */
  readonly at: { readonly x: number; readonly y: number };
  /** Opened under a button of the header (`at` under the button): it stays there and scrolls when it is higher than the room left; false for a context menu. */
  readonly underButton?: boolean;
  /** A list of choices: the one checked takes the focus at the opening, as a list of the browser opens on its value; the first item by default. */
  readonly focusChecked?: boolean;
  readonly onRun: (id: string) => void;
  readonly onClose: () => void;
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
 * The first enabled item takes the focus at the opening (B-2.4-03), or the one checked in a list of choices that asks it
 * (ADR 116). The focus goes back to what held it before when an entry runs (so that what the entry opens finds it there:
 * a field, a panel opened from the keyboard) and when Escape or Tab closes the menu; a click outside leaves it where the
 * click put it (NF08, WAI-ARIA).
 */
function useFocus(menu: RefObject<HTMLDivElement | null>, onClose: () => void, focusChecked: boolean): RefObject<HTMLElement | null> {
  const opener = useRef<HTMLElement | null>(null);
  useEffect(() => {
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const checked = focusChecked ? menu.current?.querySelector<HTMLElement>('[role="menuitemradio"][aria-checked="true"]:not([aria-disabled="true"])') : null;
    (checked ?? menu.current?.querySelector<HTMLElement>(ENTRIES))?.focus();
    const outside = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || menu.current?.contains(event.target) !== true) onClose();
    };
    document.addEventListener('pointerdown', outside, true);
    return () => {
      document.removeEventListener('pointerdown', outside, true);
    };
  }, [menu, onClose, focusChecked]);
  return opener;
}

export function Menu({ label, labelValues, testId, items, groups = [], submenus = [], at, underButton = false, focusChecked = false, onRun, onClose }: MenuProps) {
  const { t } = useTranslation();
  const menu = useRef<HTMLDivElement>(null);
  const blocks = blocksOf(items, groups, submenus);
  const cascade = useCascade(menu);
  // A floating element: the shortcuts of the canvas are silent; a key pressed in it tells that what an entry opens takes the focus.
  useKeyLayer('modal', (event) => {
    if (event.type === 'keydown') noteKeyboard();
    return false;
  });
  // The rows of 34 px place a context menu before it is drawn; drawn, its groups and its long labels may make it higher: it goes up as much.
  // A menu of the header never goes up on its button: it scrolls.
  useLayoutEffect(() => {
    const top = menu.current === null || underButton ? null : fitInWindow(menu.current.getBoundingClientRect(), window.innerHeight);
    if (menu.current !== null && top !== null) menu.current.style.top = `${String(top)}px`;
  }, [items, underButton]);
  const opener = useFocus(menu, onClose, focusChecked);
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
  const room = { width: window.innerWidth, height: window.innerHeight };
  const place: { x: number; y: number; maxHeight?: number } = underButton ? menuUnder(at, room) : menuPlacement(at, rowsOf(blocks), room);
  return (
    <div ref={menu} className="menu" role="menu" aria-label={t(label, labelValues)} data-testid={testId} style={{ left: place.x, top: place.y, maxHeight: place.maxHeight }}
      onKeyDown={onKeyDown} onPointerOver={cascade.onPointerOver} onFocus={cascade.onFocus}>
      {blocks.map((block) => {
        if ('group' in block) return <MenuGroupView key={block.group.name} group={block.group} items={block.items} testId={testId} onChoose={choose} />;
        if ('submenu' in block) {
          return (
            <MenuCascade key={block.submenu.id} submenu={block.submenu} items={block.items} testId={testId} open={cascade.open} root={menu} onChoose={choose}
              onOpen={(focus) => { cascade.show(block.submenu.id, focus); }} onClose={cascade.close} />
          );
        }
        return <MenuEntry key={block.item.id} item={block.item} testId={testId} onChoose={choose} />;
      })}
    </div>
  );
}
