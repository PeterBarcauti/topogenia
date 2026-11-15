// ui.test.tsx
// The shared components: dialog and confirmation (focus, Escape, layers), menu and toolbar drawn from commands, field, button (spec 3.5, 9.5, annex B 2.5).

import { useState, type ReactNode } from 'react';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Copy, Scissors, Trash2 } from 'lucide-react';
import { itemsOf, type Command } from '../commands/commands.ts';
import { i18next } from '../i18n/i18n.ts';
import { KeyboardProvider, useKeyLayer } from '../keyboard/KeyboardContext.tsx';
import { createKeyboard, type Keyboard } from '../keyboard/keyboard.ts';
import { Button } from './Button.tsx';
import { Confirm } from './Confirm.tsx';
import { Dialog } from './Dialog.tsx';
import { Field } from './Field.tsx';
import { Menu } from './Menu.tsx';
import { Toolbar } from './Toolbar.tsx';

const COMMANDS: readonly Command<{ selected: number }>[] = [
  { id: 'copy', label: 'Copier', icon: Copy, shortcuts: ['Ctrl+C'], enabled: (c) => c.selected > 0, run: () => undefined },
  { id: 'cut', label: 'Couper', icon: Scissors, shortcuts: ['Ctrl+X'], enabled: () => false, run: () => undefined },
  { id: 'redo', label: 'Rétablir', shortcuts: ['Ctrl+Shift+Z'], enabled: () => true, run: () => undefined },
  { id: 'delete', label: 'Supprimer', icon: Trash2, shortcuts: ['Delete'], enabled: () => true, run: () => undefined },
];

/** A canvas that listens on the layer below the floating elements and counts the keys pressed it hears. */
function Canvas({ heard }: { heard: () => void }) {
  useKeyLayer('canvas', (event) => {
    if (event.type === 'keydown') heard();
    return true;
  });
  return null;
}

/** The page with a canvas, and what is given. */
function Page({ keyboard, canvas, children }: { keyboard: Keyboard; canvas: () => void; children: ReactNode }) {
  return <KeyboardProvider keyboard={keyboard}><Canvas heard={canvas} />{children}</KeyboardProvider>;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the dialog', () => {
  function Opener({ onClose }: { onClose?: () => void }) {
    const [open, setOpen] = useState(false);
    return (
      <>
        <button type="button" onClick={() => {
          setOpen(true);
        }}>{'Ouvrir'}</button>
        {open && <Dialog title="Membres" testId="members" onClose={() => {
          setOpen(false);
          onClose?.();
        }} footer={<button type="button">OK</button>}><input aria-label="Nom" /></Dialog>}
      </>
    );
  }

  it('is a modal named by its title, focuses its first field, and gives the focus back once closed', async () => {
    render(<KeyboardProvider><Opener /></KeyboardProvider>);
    const opener = screen.getByRole('button', { name: 'Ouvrir' });
    await userEvent.click(opener);
    const dialog = screen.getByRole('dialog', { name: 'Membres' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(screen.getByRole('textbox', { name: 'Nom' })).toHaveFocus();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Fermer' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(opener).toHaveFocus();
  });

  it('closes on Escape, even from its field, and keeps Tab inside', async () => {
    const closed = vi.fn();
    render(<KeyboardProvider><Opener onClose={closed} /></KeyboardProvider>);
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    await userEvent.tab();
    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Fermer' })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('textbox', { name: 'Nom' })).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(screen.getByRole('button', { name: 'Fermer' })).toHaveFocus();
    await userEvent.click(screen.getByRole('textbox', { name: 'Nom' }));
    await userEvent.keyboard('{Escape}');
    expect(closed).toHaveBeenCalledTimes(1);
  });

  it('silences the shortcuts of the canvas while it is open (B-2.5-03)', async () => {
    const keyboard = createKeyboard();
    const canvas = vi.fn();
    render(<Page keyboard={keyboard} canvas={canvas}><Opener /></Page>);
    await userEvent.keyboard('{Delete}');
    expect(canvas).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    await userEvent.click(screen.getByRole('button', { name: 'OK' }));
    await userEvent.keyboard('{Delete}');
    expect(canvas).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: 'Fermer' }));
    await userEvent.keyboard('{Delete}');
    expect(canvas).toHaveBeenCalledTimes(2);
  });
});

describe('the confirmation', () => {
  it('asks with Cancel and the confirming button, disabled until ready', async () => {
    const [confirm, cancel] = [vi.fn(), vi.fn()];
    const { rerender } = render(<KeyboardProvider><Confirm title="Supprimer le projet" testId="delete" danger ready={false} confirmLabel="Mettre à la corbeille"
      onConfirm={confirm} onCancel={cancel}>Saisir le code.</Confirm></KeyboardProvider>);
    const go = screen.getByRole('button', { name: 'Mettre à la corbeille' });
    expect(go).toBeDisabled();
    expect(go).toHaveClass('btn--danger');
    rerender(<KeyboardProvider><Confirm title="Supprimer le projet" testId="delete" danger confirmLabel="Mettre à la corbeille"
      onConfirm={confirm} onCancel={cancel}>Saisir le code.</Confirm></KeyboardProvider>);
    await userEvent.click(screen.getByRole('button', { name: 'Mettre à la corbeille' }));
    await userEvent.click(screen.getByRole('button', { name: 'Annuler' }));
    expect([confirm.mock.calls.length, cancel.mock.calls.length]).toEqual([1, 1]);
  });

  it('confirms by « Valider » when no label is given', () => {
    render(<KeyboardProvider><Confirm title="Restaurer" testId="restore" onConfirm={vi.fn()} onCancel={vi.fn()}>Restaurer ?</Confirm></KeyboardProvider>);
    expect(screen.getByTestId('restore-confirm')).toHaveTextContent('Valider');
  });
});

describe('the menu drawn from commands (S-3.5-01, B-2.5-36)', () => {
  const open = (onRun = vi.fn(), onClose = vi.fn(), keyboard?: Keyboard) => {
    render(<KeyboardProvider keyboard={keyboard}><Menu label="Menu de l’étape" testId="step-menu" at={{ x: 40, y: 60 }} onRun={onRun} onClose={onClose}
      items={itemsOf(COMMANDS, ['copy', 'cut', 'redo', 'delete'], { selected: 1 })} /></KeyboardProvider>);
    return { onRun, onClose };
  };

  it('shows each command with its label and its shortcut written with Ctrl and the keys of the language', () => {
    open();
    const menu = screen.getByRole('menu', { name: 'Menu de l’étape' });
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['CopierCtrl+C', 'CouperCtrl+X', 'RétablirCtrl+Maj+Z', 'SupprimerSuppr']);
    expect(within(menu).getByRole('menuitem', { name: 'Couper' })).toHaveAttribute('aria-disabled', 'true');
    // The shortcut is given to assistive technologies in the names of ARIA, its text out of the name of the entry (validated mock-up).
    expect(within(menu).getAllByRole('menuitem').map((item) => item.getAttribute('aria-keyshortcuts'))).toEqual(['Control+C', 'Control+X', 'Control+Shift+Z', 'Delete']);
    expect(menu).toHaveStyle({ left: '40px', top: '60px' });
  });

  it('focuses the first item, moves over the disabled ones, runs on Enter and closes', async () => {
    const { onRun, onClose } = open();
    expect(screen.getByRole('menuitem', { name: 'Copier' })).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Rétablir' })).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(screen.getByRole('menuitem', { name: 'Supprimer' })).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(onRun).toHaveBeenCalledWith('delete');
    expect(onClose).toHaveBeenCalled();
  });

  it('a disabled item does nothing; Escape and a click outside close', async () => {
    const { onRun, onClose } = open();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Couper' }));
    expect(onRun).not.toHaveBeenCalled();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);
    await userEvent.click(document.body);
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it('is a floating element: the shortcuts of the canvas are silent while it is open (B-2.5-03)', () => {
    const keyboard = createKeyboard();
    const canvas = vi.fn(() => true);
    keyboard.add('canvas', canvas);
    open(vi.fn(), vi.fn(), keyboard);
    act(() => {
      document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'a', ctrlKey: true, bubbles: true }));
    });
    expect(canvas).not.toHaveBeenCalled();
  });
});

describe('the toolbar drawn from commands', () => {
  it('named groups of icon buttons, named with their shortcut, one tab stop, arrows to move', async () => {
    const onRun = vi.fn();
    const items = itemsOf(COMMANDS, ['copy', 'cut', 'delete'], { selected: 1 });
    render(<Toolbar label="Ruban" testId="ribbon" onRun={onRun} pressed={new Set(['copy'])}
      groups={[{ id: 'clipboard', label: 'Presse-papiers', items: items.slice(0, 2) }, { id: 'edit', label: 'Édition', items: items.slice(2) }]} />);
    const bar = screen.getByRole('toolbar', { name: 'Ruban' });
    expect(within(bar).getAllByRole('group').map((group) => group.getAttribute('aria-label'))).toEqual(['Presse-papiers', 'Édition']);
    const copy = within(bar).getByRole('button', { name: 'Copier' });
    expect([copy.title, copy.getAttribute('aria-pressed'), copy.tabIndex]).toEqual(['Copier (Ctrl+C)', 'true', 0]);
    expect(within(bar).getByRole('button', { name: 'Couper' })).toBeDisabled();
    expect(within(bar).getByRole('button', { name: 'Supprimer' }).tabIndex).toBe(-1);
    copy.focus();
    await userEvent.keyboard('{End}');
    expect(within(bar).getByRole('button', { name: 'Supprimer' })).toHaveFocus();
    await userEvent.keyboard('{ArrowRight}{Enter}');
    expect(onRun).toHaveBeenCalledWith('copy');
  });
});

describe('the field and the button', () => {
  it('ties the label, the help and the error to the control', () => {
    render(<Field label="Code court" testId="project-code" hint="3 à 6 lettres" error="Code déjà pris">{(control) => <input {...control} />}</Field>);
    const input = screen.getByRole('textbox', { name: 'Code court' });
    expect(input).toHaveAttribute('data-testid', 'project-code');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('3 à 6 lettres Code déjà pris');
    expect(screen.getByRole('alert')).toHaveTextContent('Code déjà pris');
  });

  it('a field without help nor error describes nothing', () => {
    render(<Field label="Nom" testId="name">{(control) => <textarea {...control} />}</Field>);
    expect(screen.getByRole('textbox', { name: 'Nom' })).not.toHaveAttribute('aria-describedby');
  });

  it('a button carries its test identifier, its variant and its icon, and is never a submit by accident', () => {
    render(<Button testId="new-project" variant="primary" icon={Copy}>{'Nouveau projet'}</Button>);
    const button = screen.getByRole('button', { name: 'Nouveau projet' });
    expect([button.getAttribute('type'), button.dataset.testid, button.className]).toEqual(['button', 'new-project', 'btn btn--primary']);
    expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  });
});
