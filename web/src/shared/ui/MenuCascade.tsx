// MenuCascade.tsx
// The row of a submenu (a chevron, a popup) and the submenu itself, fixed beside its row, to the right or to the left in the window, with its arrows and Escape (ARIA 1.2, ADR 117).

import { useEffect, useLayoutEffect, useRef, type KeyboardEvent, type RefObject } from 'react';
import { ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { CommandItem } from '../commands/commands.ts';
import { Icon } from './Icon.tsx';
import type { MenuSubmenu } from './menuBlocks.ts';
import { entriesOf, move } from './menuKeys.ts';
import { MENU_WIDTH, submenuPlacement } from './menuPlacement.ts';
import { MenuEntry } from './MenuParts.tsx';
import type { CascadeState } from './useCascade.ts';

export interface CascadeProps {
  readonly submenu: MenuSubmenu;
  readonly items: readonly CommandItem[];
  readonly testId: string;
  readonly open: CascadeState | null;
  /** The menu that holds the row: the submenu is placed beside it. */
  readonly root: RefObject<HTMLDivElement | null>;
  readonly onOpen: (focus: boolean) => void;
  readonly onClose: (back: boolean) => void;
  readonly onChoose: (item: CommandItem) => void;
}

/** The submenu: fixed beside its row, its first entry focused when it was opened by a click or a key. */
function SubmenuPanel({ submenu, items, testId, open, root, onClose, onChoose }: CascadeProps & { open: CascadeState }) {
  const { t } = useTranslation();
  const panel = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const menu = root.current;
    if (panel.current === null || menu === null) return;
    const row = menu.querySelector<HTMLElement>(`[data-cascade="${submenu.id}"]`);
    if (row === null) return;
    const { left, right } = menu.getBoundingClientRect();
    const size = { width: panel.current.offsetWidth || MENU_WIDTH, height: panel.current.scrollHeight };
    const place = submenuPlacement({ left, right, top: row.getBoundingClientRect().top }, size, { width: window.innerWidth, height: window.innerHeight });
    Object.assign(panel.current.style, { left: `${String(place.x)}px`, top: `${String(place.y)}px`, maxHeight: `${String(place.maxHeight)}px` });
  }, [root, submenu.id, items]);
  useEffect(() => {
    if (open.focus && panel.current !== null) entriesOf(panel.current)[0]?.focus();
  }, [open.focus]);
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'ArrowLeft' || event.key === 'Escape') onClose(true);
    else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') move(event.currentTarget, event.key === 'ArrowDown' ? 1 : -1);
    else return;
    event.preventDefault();
    event.stopPropagation();
  };
  return (
    <div ref={panel} className="menu menu-cascade" role="menu" aria-label={t(submenu.name)} data-testid={`${testId}-cascade-${submenu.id}-menu`} onKeyDown={onKeyDown}>
      {items.map((item, index) => (
        <MenuEntry key={item.id} item={index === 0 ? { ...item, separatorBefore: false } : item} testId={testId} onChoose={onChoose} text={submenu.texts?.[item.id]} />
      ))}
    </div>
  );
}

/** The row of a submenu, where its first item stood, after the separator that item had; open by a click, Enter, Space or the right arrow. */
export function MenuCascade(props: CascadeProps) {
  const { t } = useTranslation();
  const { submenu, items, testId, open, onOpen } = props;
  const isOpen = open?.id === submenu.id;
  const className = items[0]?.separatorBefore === true ? 'menu-item menu-item--group' : 'menu-item';
  return (
    <>
      <button type="button" role="menuitem" aria-haspopup="menu" aria-expanded={isOpen} className={className} tabIndex={-1} data-cascade={submenu.id} data-testid={`${testId}-cascade-${submenu.id}`}
        onClick={() => {
          onOpen(true);
        }}
        onKeyDown={(event) => {
          if (event.key !== 'ArrowRight') return;
          event.preventDefault();
          event.stopPropagation();
          onOpen(true);
        }}>
        <span className="menu-icon-space" />
        <span className="menu-label">{t(submenu.name)}</span>
        <span className="menu-trail"><Icon icon={ChevronRight} size={15} /></span>
      </button>
      {open !== null && isOpen && <SubmenuPanel {...props} open={open} />}
    </>
  );
}
