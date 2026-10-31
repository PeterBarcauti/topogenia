// clarificationNote.test.tsx
// The question the facilitator asked, rewritten in place or removed by the facilitator alone, with the version of the sheet; Escape gives the field up and leaves the sheet open; a refusal is said (annex B 4, spec 3.0 section 16).

import { act, fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../shared/i18n/i18n.ts';
import { callsTo } from '../../test/fakeApi.ts';
import { renderSheet } from '../../test/sheetPanel.tsx';
import { AUTHOR_ID, sheetViewOf } from '../../test/sheets.ts';

const validated = { number: 1, validated_at: '2026-10-20T08:00:00Z', validated_by: AUTHOR_ID };
const facilitator = { fill: true, validate: true, request_clarification: true, assign: true };
const asked = (overrides = {}) => sheetViewOf({ status: 'needs_review', validated, clarification_request: 'Comment la facture parvient-elle au client ?', permissions: facilitator, version: 6, ...overrides });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** A click whose write is answered before the test goes on. */
async function press(testId: string) {
  await act(async () => {
    fireEvent.click(screen.getByTestId(testId));
    await Promise.resolve();
  });
}

describe('the question of the facilitator, rewritten or removed (spec 3.0 section 16)', () => {
  it('is rewritten in place, 1000 characters at most, sent trimmed with the version; the state of the sheet stays', async () => {
    const { sent, record } = await renderSheet(asked(), {
      'PUT /api/steps/s2/sheet/clarification': { json: asked({ clarification_request: 'Par quel canal la facture part-elle ?', version: 7 }) },
    });
    const note = screen.getByRole('note');
    fireEvent.click(within(note).getByRole('button', { name: 'Modifier' }));
    const field = screen.getByTestId('sheet-clarification-field');
    expect([field.getAttribute('maxlength'), (field as HTMLTextAreaElement).value, document.activeElement === field])
      .toEqual(['1000', 'Comment la facture parvient-elle au client ?', true]);
    expect(within(note).queryByRole('button', { name: 'Retirer' })).toBeNull();
    fireEvent.change(field, { target: { value: '   ' } });
    expect(screen.getByTestId('sheet-clarification-save')).toBeDisabled();
    fireEvent.change(field, { target: { value: '  Par quel canal la facture part-elle ?  ' } });
    await press('sheet-clarification-save');
    // The field closes on the answer of the host: until then its text counts in the text of the box.
    await vi.waitFor(() => {
      expect(screen.queryByTestId('sheet-clarification-field')).toBeNull();
    });
    expect(within(screen.getByRole('note')).getByText('Par quel canal la facture part-elle ?')).toBeInTheDocument();
    const call = callsTo(sent, 'PUT', '/api/steps/s2/sheet/clarification')[0];
    expect([call?.body, call?.headers.get('If-Match'), record.notes]).toEqual([{ text: 'Par quel canal la facture part-elle ?' }, '"6"', [['Question modifiée.', 'success']]]);
    expect(screen.queryByTestId('sheet-clarification-field')).toBeNull();
    expect(screen.getByTestId('sheet-state')).toHaveTextContent('à revoir');
  });

  it('Escape or « Annuler » gives the field up and leaves the sheet open, nothing sent', async () => {
    const { sent, record } = await renderSheet(asked());
    fireEvent.click(screen.getByTestId('sheet-clarification-edit'));
    fireEvent.keyDown(screen.getByTestId('sheet-clarification-field'), { key: 'Escape' });
    expect(screen.queryByTestId('sheet-clarification-field')).toBeNull();
    fireEvent.click(screen.getByTestId('sheet-clarification-edit'));
    fireEvent.click(screen.getByTestId('sheet-clarification-edit-cancel'));
    expect(screen.queryByTestId('sheet-clarification-field')).toBeNull();
    expect([callsTo(sent, 'PUT', '/api/steps/s2/sheet/clarification'), record.events]).toEqual([[], []]);
    expect(screen.getByRole('note')).toHaveTextContent('Comment la facture parvient-elle au client ?');
  });

  it('is removed with the version: the sheet « à revoir » is validated again and the box leaves', async () => {
    const { sent, record } = await renderSheet(asked(), {
      'DELETE /api/steps/s2/sheet/clarification': { json: sheetViewOf({ status: 'validated', validated, clarification_request: null, permissions: facilitator, version: 7 }) },
    });
    await press('sheet-clarification-withdraw');
    await vi.waitFor(() => {
      expect(screen.queryByRole('note')).toBeNull();
    });
    expect(callsTo(sent, 'DELETE', '/api/steps/s2/sheet/clarification')[0]?.headers.get('If-Match')).toBe('"6"');
    expect([screen.getByTestId('sheet-state').textContent, record.notes]).toEqual(['validée', [['Question retirée.', 'success']]]);
  });

  it('a refusal of the host is said, the question kept; who is not facilitator reads it without the buttons', async () => {
    const first = await renderSheet(asked(), {
      'DELETE /api/steps/s2/sheet/clarification': { refused: 409, code: 'CONFLIT_VERSION', message: 'La fiche a changé entre-temps.' },
    });
    await press('sheet-clarification-withdraw');
    await vi.waitFor(() => {
      expect(first.record.notes).toEqual([['La fiche a changé entre-temps.', 'error']]);
    });
    expect(screen.getByRole('note')).toHaveTextContent('Comment la facture parvient-elle au client ?');
    first.unmount();
    await renderSheet(asked({ permissions: { fill: true, validate: true, request_clarification: false, assign: false } }));
    expect(within(screen.getByRole('note')).queryAllByRole('button')).toEqual([]);
    expect(screen.getByRole('note')).toHaveTextContent('Précision demandée par l’animateur');
  });
});
