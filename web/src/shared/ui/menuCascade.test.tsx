// menuCascade.test.tsx
// The submenu of the shared menu: a row with a chevron that opens at the pointer after a short delay, at a click or a key, closes by the arrow or Escape, stays in the window, ARIA 1.2 (ADR 117).

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../i18n/i18n.ts';
import { KeyboardProvider } from '../keyboard/KeyboardContext.tsx';
import type { CommandItem } from '../commands/commands.ts';
import { Menu, type MenuSubmenu } from './Menu.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
});

const item = (id: string, label: string, extra: Partial<CommandItem> = {}): CommandItem => ({ id, label, icon: undefined, shortcut: undefined, disabled: false, ...extra });

const ITEMS = [item('open', 'Ouvrir'), item('pdf', 'Exporter en PDF', { separatorBefore: true }), item('png', 'Exporter en PNG'), item('gone', 'Perdu', { disabled: true }), item('copy', 'Copier le projet'), item('props', 'Propriétés', { separatorBefore: true })];
const SUBMENUS: readonly MenuSubmenu[] = [
  { id: 'export', name: 'Exporter la carte', ids: ['pdf', 'png', 'gone'], texts: { pdf: 'PDF', png: 'Image PNG' } },
  { id: 'project', name: 'Projet', ids: ['copy'] },
];

function open(items: readonly CommandItem[] = ITEMS) {
  const onRun = vi.fn();
  const onClose = vi.fn();
  render(<KeyboardProvider><Menu label="Menu Fichier" testId="file" at={{ x: 40, y: 60 }} items={items} submenus={SUBMENUS} onRun={onRun} onClose={onClose} /></KeyboardProvider>);
  return { onRun, onClose };
}

const row = (name: string) => screen.getByRole('menuitem', { name });
const sub = (name: string) => screen.queryByRole('menu', { name });
const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

describe('the row of a submenu', () => {
  it('stands where its first item stood, with a popup, closed, and its items are not drawn', () => {
    open();
    const root = screen.getByRole('menu', { name: 'Menu Fichier' });
    expect(within(root).getAllByRole('menuitem').map((entry) => entry.textContent)).toEqual(['Ouvrir', 'Exporter la carte', 'Projet', 'Propriétés']);
    expect([row('Exporter la carte').getAttribute('aria-haspopup'), row('Exporter la carte').getAttribute('aria-expanded')]).toEqual(['menu', 'false']);
    expect(row('Ouvrir')).not.toHaveAttribute('aria-haspopup');
    expect(screen.getByTestId('file-cascade-export')).toBe(row('Exporter la carte'));
    expect(sub('Exporter la carte')).toBeNull();
  });

  it('keeps the separator of the item it replaces', () => {
    open();
    expect(row('Exporter la carte')).toHaveClass('menu-item--group');
    expect(row('Projet')).not.toHaveClass('menu-item--group');
  });

  it('a submenu none of whose items is shown is not drawn', () => {
    open(ITEMS.filter((entry) => entry.id !== 'copy'));
    expect(screen.queryByRole('menuitem', { name: 'Projet' })).toBeNull();
  });
});

describe('opened by the pointer', () => {
  it('a click opens it, names it, and focuses its first entry', async () => {
    open();
    await userEvent.click(row('Exporter la carte'));
    const menu = sub('Exporter la carte');
    expect(menu).toHaveAttribute('role', 'menu');
    expect(row('Exporter la carte')).toHaveAttribute('aria-expanded', 'true');
    const entries = within(menu as HTMLElement).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual(['PDF', 'Image PNG', 'Perdu']);
    expect(entries[0]).toHaveFocus();
    expect(entries[0]).not.toHaveClass('menu-item--group');
    expect(screen.getByTestId('file-cascade-export-menu')).toBe(menu);
  });

  it('the hover opens it after a short delay, without taking the focus; the hover of another row closes it', async () => {
    open();
    await userEvent.hover(row('Exporter la carte'));
    expect(sub('Exporter la carte')).toBeNull();
    expect(await screen.findByRole('menu', { name: 'Exporter la carte' })).toBeInTheDocument();
    expect(row('Ouvrir')).toHaveFocus();
    await userEvent.hover(row('Projet'));
    expect(sub('Exporter la carte')).toBeNull();
    expect(await screen.findByRole('menu', { name: 'Projet' })).toBeInTheDocument();
    await userEvent.hover(row('Ouvrir'));
    expect(sub('Projet')).toBeNull();
  });

  it('a row left before the delay does not open', async () => {
    open();
    await userEvent.hover(row('Exporter la carte'));
    await userEvent.hover(row('Ouvrir'));
    await wait(400);
    expect(sub('Exporter la carte')).toBeNull();
  });

  it('the pointer in the submenu keeps it open', async () => {
    open();
    await userEvent.click(row('Exporter la carte'));
    await userEvent.hover(screen.getByRole('menuitem', { name: 'Exporter en PNG' }));
    await wait(400);
    expect(sub('Exporter la carte')).not.toBeNull();
  });

  it('a click on an entry runs it and closes everything; a click outside closes everything', async () => {
    const { onRun, onClose } = open();
    await userEvent.click(row('Exporter la carte'));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Exporter en PNG' }));
    expect([onRun.mock.calls, onClose.mock.calls.length]).toEqual([[['png']], 1]);
    await userEvent.click(row('Exporter la carte'));
    await userEvent.click(document.body);
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it('a disabled entry does nothing', async () => {
    const { onRun, onClose } = open();
    await userEvent.click(row('Exporter la carte'));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Perdu' }));
    expect([onRun.mock.calls.length, onClose.mock.calls.length]).toEqual([0, 0]);
  });
});

describe('by the keyboard', () => {
  it('the right arrow, Enter or Space opens it on its first entry', async () => {
    open();
    for (const key of ['{ArrowRight}', '{Enter}', ' ']) {
      row('Exporter la carte').focus();
      await userEvent.keyboard(key);
      expect(within(sub('Exporter la carte') as HTMLElement).getAllByRole('menuitem')[0]).toHaveFocus();
      await userEvent.keyboard('{ArrowLeft}');
      expect(sub('Exporter la carte')).toBeNull();
    }
  });

  it('the right arrow does nothing on an ordinary row', async () => {
    const { onClose } = open();
    await userEvent.keyboard('{ArrowRight}');
    expect([row('Ouvrir') === document.activeElement, screen.queryAllByRole('menu').length, onClose.mock.calls.length]).toEqual([true, 1, 0]);
  });

  it('up and down move in the submenu only, the left arrow closes it and gives the focus back to its row', async () => {
    open();
    row('Exporter la carte').focus();
    await userEvent.keyboard('{ArrowRight}{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Exporter en PNG' })).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Exporter en PDF' })).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}{ArrowLeft}');
    expect(sub('Exporter la carte')).toBeNull();
    expect(row('Exporter la carte')).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(row('Projet')).toHaveFocus();
  });

  it('Escape closes the submenu only and gives the focus back to its row; Escape at the first level closes everything', async () => {
    const { onClose } = open();
    row('Exporter la carte').focus();
    await userEvent.keyboard('{ArrowRight}{Escape}');
    expect([sub('Exporter la carte'), onClose.mock.calls.length]).toEqual([null, 0]);
    expect(row('Exporter la carte')).toHaveFocus();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('Enter on an entry runs it and closes everything', async () => {
    const { onRun, onClose } = open();
    row('Projet').focus();
    await userEvent.keyboard('{ArrowRight}{Enter}');
    expect([onRun.mock.calls, onClose.mock.calls.length]).toEqual([[['copy']], 1]);
  });

  it('moving the focus to another row of the first level closes an open submenu', async () => {
    open();
    await userEvent.hover(row('Exporter la carte'));
    await screen.findByRole('menu', { name: 'Exporter la carte' });
    row('Exporter la carte').focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(sub('Exporter la carte')).toBeNull();
  });
});

describe('the place of the submenu', () => {
  const rects = (root: { left: number; right: number }) => {
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function rect(this: HTMLElement) {
      const isRoot = this.getAttribute('role') === 'menu' && this.getAttribute('aria-label') === 'Menu Fichier';
      const base = isRoot ? { ...root, top: 60, bottom: 300 } : { left: root.left, right: root.right, top: 100, bottom: 134 };
      return { ...base, x: base.left, y: base.top, width: base.right - base.left, height: base.bottom - base.top, toJSON: () => ({}) };
    });
  };

  it('opens to the right of its menu, aligned on its row', async () => {
    rects({ left: 40, right: 280 });
    open();
    await userEvent.click(row('Exporter la carte'));
    const menu = sub('Exporter la carte') as HTMLElement;
    expect([menu.style.left, menu.style.top]).toEqual(['278px', '95px']);
  });

  it('opens to the left of its menu when it would leave the window on the right', async () => {
    rects({ left: 800, right: 1040 });
    open();
    await userEvent.click(row('Exporter la carte'));
    expect((sub('Exporter la carte') as HTMLElement).style.left).toBe('562px');
  });
});
