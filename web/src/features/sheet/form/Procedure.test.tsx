// Procedure.test.tsx
// The procedure: one line at least, a line of number, text, tool and nature; Enter, Backspace, the buttons, Alt+arrows and the handle; the focus that follows; « Contrôler » in green (annex B 4, AR-89).

import { useState } from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import type { ProcedureLine } from '../model/draft.ts';
import { Procedure } from './Procedure.tsx';

const line = (text: string, overrides: Partial<ProcedureLine> = {}): ProcedureLine => ({ text, tool: '', nature: null, ...overrides });

/** The procedure held by the test as the form holds it; every change kept. */
function showProcedure(initial: readonly ProcedureLine[]) {
  const changes: (readonly ProcedureLine[])[] = [];
  function Holder() {
    const [lines, setLines] = useState(initial);
    return <Procedure lines={lines} onChange={(next) => { changes.push(next); setLines(next); }} />;
  }
  render(<Holder />);
  const texts = () => screen.getAllByRole('textbox', { name: /^Opération \d+$/ }).map((field) => (field as HTMLInputElement).value);
  return { changes, texts };
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('a line of the procedure (B-4-29 to B-4-32, B-4-34, B-4-38)', () => {
  it('one empty line at least, with the example; a number, a text of 1000, a tool of 200 from the list, a nature among ten', () => {
    showProcedure([]);
    const row = screen.getByTestId('procedure-line-1');
    const text = within(row).getByRole('textbox', { name: 'Opération 1' });
    const tool = within(row).getByRole('combobox', { name: 'Outil de l’opération 1' });
    expect([row.querySelector('.procedure-number')?.textContent, text.getAttribute('placeholder'), text.getAttribute('maxlength'), tool.getAttribute('maxlength'), tool.getAttribute('list')])
      .toEqual(['1', 'Ex. Extraire les ventes du mois', '1000', '200', 'sheet-tools-list']);
    expect(within(within(row).getByRole('combobox', { name: 'Nature de l’opération 1' })).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Nature…', 'Extraire', 'Copier-coller', 'Ressaisir', 'Mettre en forme', 'Calculer', 'Contrôler', 'Relancer', 'Classer', 'Envoyer', 'Attendre',
    ]);
  });

  it('the example on the first line only; « Contrôler » draws its line in green', () => {
    showProcedure([line('Saisir'), line('Vérifier', { nature: 'check' })]);
    expect([screen.getByTestId('procedure-text-2').getAttribute('placeholder'), screen.getByTestId('procedure-line-2').classList.contains('is-check'),
      screen.getByTestId('procedure-line-1').classList.contains('is-check')]).toEqual([null, true, false]);
  });

  it('a tool and a nature typed change their line', () => {
    const { changes } = showProcedure([line('Saisir')]);
    fireEvent.change(screen.getByTestId('procedure-tool-1'), { target: { value: 'ERP' } });
    fireEvent.change(screen.getByTestId('procedure-nature-1'), { target: { value: 'check' } });
    fireEvent.change(screen.getByTestId('procedure-nature-1'), { target: { value: '' } });
    expect(changes).toEqual([[line('Saisir', { tool: 'ERP' })], [line('Saisir', { tool: 'ERP', nature: 'check' })], [line('Saisir', { tool: 'ERP' })]]);
  });
});

describe('the keys of a line (B-4-35 to B-4-37)', () => {
  it('Enter inserts the next line and takes the focus to it', () => {
    const { texts } = showProcedure([line('a'), line('b')]);
    fireEvent.keyDown(screen.getByTestId('procedure-text-1'), { key: 'Enter' });
    expect([texts(), document.activeElement]).toEqual([['a', '', 'b'], screen.getByTestId('procedure-text-2')]);
  });

  it('Backspace on an empty line removes it, the focus on the line before; nothing on a line with text nor on the last line left', () => {
    const { texts } = showProcedure([line('a'), line(''), line('c')]);
    fireEvent.keyDown(screen.getByTestId('procedure-text-3'), { key: 'Backspace' });
    fireEvent.keyDown(screen.getByTestId('procedure-text-2'), { key: 'Backspace' });
    expect([texts(), document.activeElement]).toEqual([['a', 'c'], screen.getByTestId('procedure-text-1')]);
    fireEvent.keyDown(screen.getByTestId('procedure-text-1'), { key: 'Backspace' });
    expect(texts()).toEqual(['a', 'c']);
  });

  it('Backspace never removes the only line', () => {
    const { changes } = showProcedure([]);
    fireEvent.keyDown(screen.getByTestId('procedure-text-1'), { key: 'Backspace' });
    expect(changes).toEqual([]);
  });
});

describe('moving a line (B-4-33, AR-89)', () => {
  it('↑ and ↓ move it by one, closed at the ends; the focus follows the line', () => {
    const { texts } = showProcedure([line('a'), line('b'), line('c')]);
    expect([screen.getByRole('button', { name: 'Monter l’opération 1' }), screen.getByRole('button', { name: 'Descendre l’opération 3' })].map((button) => button.hasAttribute('disabled')))
      .toEqual([true, true]);
    fireEvent.click(screen.getByRole('button', { name: 'Monter l’opération 3' }));
    expect([texts(), document.activeElement]).toEqual([['a', 'c', 'b'], screen.getByTestId('procedure-text-2')]);
    fireEvent.click(screen.getByRole('button', { name: 'Descendre l’opération 1' }));
    expect(texts()).toEqual(['c', 'a', 'b']);
  });

  it('Alt+↑ and Alt+↓ move the line typed in, nothing past the ends', () => {
    const { texts } = showProcedure([line('a'), line('b'), line('c')]);
    fireEvent.keyDown(screen.getByTestId('procedure-text-2'), { key: 'ArrowDown', altKey: true });
    expect([texts(), document.activeElement]).toEqual([['a', 'c', 'b'], screen.getByTestId('procedure-text-3')]);
    fireEvent.keyDown(screen.getByTestId('procedure-text-3'), { key: 'ArrowDown', altKey: true });
    fireEvent.keyDown(screen.getByTestId('procedure-text-1'), { key: 'ArrowUp', altKey: true });
    fireEvent.keyDown(screen.getByTestId('procedure-text-3'), { key: 'ArrowUp' });
    expect(texts()).toEqual(['a', 'c', 'b']);
    fireEvent.keyDown(screen.getByTestId('procedure-text-3'), { key: 'ArrowUp', altKey: true });
    expect(texts()).toEqual(['a', 'b', 'c']);
  });

  it('the handle left of the number drags a line onto another, which is marked while aimed; the handle is for the mouse only', () => {
    const { texts } = showProcedure([line('a'), line('b'), line('c')]);
    const handle = screen.getByTestId('procedure-handle-3');
    expect([handle.getAttribute('draggable'), handle.getAttribute('aria-hidden'), handle.title]).toEqual(['true', 'true', 'Glisser l’opération 3 pour la déplacer']);
    const transfer = { setData: vi.fn(), effectAllowed: '' };
    fireEvent.dragStart(handle, { dataTransfer: transfer });
    fireEvent.dragOver(screen.getByTestId('procedure-line-1'));
    expect([screen.getByTestId('procedure-line-1').classList.contains('is-target'), transfer.setData.mock.calls, transfer.effectAllowed]).toEqual([true, [['text/plain', '2']], 'move']);
    fireEvent.drop(screen.getByTestId('procedure-line-1'));
    expect([texts(), document.activeElement, screen.getByTestId('procedure-line-1').classList.contains('is-target')]).toEqual([['c', 'a', 'b'], screen.getByTestId('procedure-text-1'), false]);
  });

  it('a drop without a line dragged, or a drag ended out of the list, changes nothing', () => {
    const { changes } = showProcedure([line('a'), line('b')]);
    expect(fireEvent.dragOver(screen.getByTestId('procedure-line-1'))).toBe(true);
    expect(screen.getByTestId('procedure-line-1').classList.contains('is-target')).toBe(false);
    fireEvent.drop(screen.getByTestId('procedure-line-1'));
    fireEvent.dragStart(screen.getByTestId('procedure-handle-1'), { dataTransfer: { setData: vi.fn(), effectAllowed: '' } });
    fireEvent.dragEnd(screen.getByTestId('procedure-handle-1'));
    fireEvent.drop(screen.getByTestId('procedure-line-2'));
    expect(changes).toEqual([]);
  });
});

describe('adding and removing (B-4-33)', () => {
  it('« Ajouter une opération » adds a line at the end and takes the focus to it; × removes a line; closed on a single empty line', () => {
    const { texts } = showProcedure([line('a')]);
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une opération' }));
    expect([texts(), document.activeElement]).toEqual([['a', ''], screen.getByTestId('procedure-text-2')]);
    fireEvent.click(screen.getByRole('button', { name: 'Retirer l’opération 1' }));
    expect(texts()).toEqual(['']);
    expect(screen.getByRole('button', { name: 'Retirer l’opération 1' })).toBeDisabled();
  });
});
