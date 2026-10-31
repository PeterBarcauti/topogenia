// validation.test.tsx
// « Je valide ma fiche » after the last saving, a conflict included, what the foot says of the version the team sees, the clarification the facilitator asks (annex B 4).

import { act, fireEvent, screen } from '@testing-library/react';
import { i18next } from '../../shared/i18n/i18n.ts';
import { callsTo } from '../../test/fakeApi.ts';
import { renderSheet } from '../../test/sheetPanel.tsx';
import { AUTHOR_ID, contentOf, sheetViewOf } from '../../test/sheets.ts';

const validated = { number: 1, validated_at: '2026-10-20T08:00:00Z', validated_by: AUTHOR_ID };
const facilitator = { fill: true, validate: true, request_clarification: true, assign: true };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the validation (B-4-51 to B-4-53)', () => {
  it('saves what waits first, then validates with the version given back; the sheet validated is shown and said', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ status: 'draft', version: 2 }), {
      'PUT /api/steps/s2/sheet': { json: sheetViewOf({ status: 'draft', version: 3 }) },
      'POST /api/steps/s2/sheet/validate': { json: sheetViewOf({ status: 'validated', completeness: 80, validated, version: 3 }) },
    });
    fireEvent.change(screen.getByTestId('sheet-purpose'), { target: { value: 'Facturer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Je valide ma fiche' }));
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-state')).toHaveTextContent('validée');
    });
    expect(sent.filter((call) => call.method !== 'GET').map((call) => [call.method, call.path, call.headers.get('If-Match')])).toEqual([
      ['PUT', '/api/steps/s2/sheet', '"2"'], ['POST', '/api/steps/s2/sheet/validate', '"3"'],
    ]);
    expect([record.notes, record.summaries.at(-1)?.status]).toEqual([[['Fiche validée : elle est visible par l’équipe.', 'success']], 'validated']);
  });

  it('a saving that fails stops the validation: the host would fix another content than the one shown', async () => {
    const { sent } = await renderSheet(sheetViewOf({ status: 'draft' }), {
      'PUT /api/steps/s2/sheet': { refused: 503, code: 'HTTP_503', message: 'Indisponible.' },
    });
    fireEvent.change(screen.getByTestId('sheet-purpose'), { target: { value: 'Facturer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Je valide ma fiche' }));
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-saving')).toHaveTextContent('Non enregistré');
    });
    expect(callsTo(sent, 'POST', '/api/steps/s2/sheet/validate')).toEqual([]);
  });

  it('closed on an empty sheet until something is typed; once validated, the foot says by whom and when at once (B-4-52, B-4-54)', async () => {
    await renderSheet(sheetViewOf(), {
      'PUT /api/steps/s2/sheet': { json: sheetViewOf({ status: 'draft', completeness: 20, version: 1 }) },
      'POST /api/steps/s2/sheet/validate': { json: sheetViewOf({ status: 'validated', completeness: 20, validated, version: 2 }) },
    });
    expect(screen.getByRole('button', { name: 'Je valide ma fiche' })).toBeDisabled();
    fireEvent.change(screen.getByTestId('sheet-purpose'), { target: { value: 'Facturer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Je valide ma fiche' }));
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-state')).toHaveTextContent('validée');
    });
    expect(screen.getByTestId('sheet-foot-text')).toHaveTextContent('Fiche validée le 20/10/2026 par Camille Bernard.');
  });

  it('open on a draft and on a sheet to review, closed on a validated sheet until something is typed; absent without the right', async () => {
    const button = () => screen.queryByRole('button', { name: 'Je valide ma fiche' });
    const first = await renderSheet(sheetViewOf({ status: 'needs_review' }));
    expect(button()).toBeEnabled();
    first.unmount();
    const draft = await renderSheet(sheetViewOf({ status: 'draft', validated }));
    expect(button()).toBeEnabled();
    draft.unmount();
    const second = await renderSheet(sheetViewOf({ status: 'validated', validated }), { 'PUT /api/steps/s2/sheet': { pending: true } });
    expect(button()).toBeDisabled();
    fireEvent.change(screen.getByTestId('sheet-actor'), { target: { value: 'A' } });
    expect(button()).toBeEnabled();
    second.unmount();
    await renderSheet(sheetViewOf({ status: 'draft', permissions: { fill: true, validate: false, request_clarification: false, assign: false } }));
    expect(button()).toBeNull();
  });
});

describe('the validation after a conflict (B-4-44, B-4-53)', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('the message of the host said, the typing kept; « Je valide » sends it with the version read again, then validates with the version given back', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ status: 'draft', version: 3 }), {
      'GET /api/steps/s2/sheet': [{ json: sheetViewOf({ status: 'draft', version: 3 }) }, { json: sheetViewOf({ status: 'draft', version: 9, content: contentOf({ purpose: 'Sa version' }) }) }],
      'PUT /api/steps/s2/sheet': [{ refused: 409, code: 'CONFLIT_VERSION', message: 'Modifiée par Alex Martin il y a 2 minutes.' }, { json: sheetViewOf({ status: 'draft', version: 10 }) }],
      'POST /api/steps/s2/sheet/validate': { json: sheetViewOf({ status: 'validated', validated, version: 10 }) },
    });
    fireEvent.change(screen.getByTestId('sheet-purpose'), { target: { value: 'Ma version' } });
    await act(() => vi.advanceTimersByTimeAsync(1200));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'GET', '/api/steps/s2/sheet')).toHaveLength(2);
    });
    expect(record.notes).toEqual([['Modifiée par Alex Martin il y a 2 minutes.', 'error']]);
    expect(screen.getByTestId('sheet-purpose')).toHaveValue('Ma version');
    fireEvent.click(screen.getByRole('button', { name: 'Je valide ma fiche' }));
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-state')).toHaveTextContent('validée');
    });
    const typed = { fields: { purpose: { value: 'Ma version', number: null } }, procedure: [], free_text: null };
    expect(sent.filter((call) => call.method !== 'GET').map((call) => [call.method, call.path, call.headers.get('If-Match'), call.method === 'PUT' ? call.body : null])).toEqual([
      ['PUT', '/api/steps/s2/sheet', '"3"', typed], ['PUT', '/api/steps/s2/sheet', '"9"', typed], ['POST', '/api/steps/s2/sheet/validate', '"10"', null],
    ]);
  });
});

describe('what the foot says (B-4-54, B-4-55)', () => {
  it('« Fiche validée le… par… » on a validated sheet saved, not while a change waits', async () => {
    await renderSheet(sheetViewOf({ status: 'validated', validated }), { 'PUT /api/steps/s2/sheet': { pending: true } });
    expect(screen.getByTestId('sheet-foot-text')).toHaveTextContent('Fiche validée le 20/10/2026 par Camille Bernard.');
    fireEvent.change(screen.getByTestId('sheet-actor'), { target: { value: 'A' } });
    expect(screen.getByTestId('sheet-foot-text').textContent).toBe('');
  });

  it('on a draft over a validated version, the team still sees the version validated on its day; a first draft says how to share it', async () => {
    const first = await renderSheet(sheetViewOf({ status: 'draft', validated }));
    expect(screen.getByTestId('sheet-foot-text')).toHaveTextContent('l’équipe voit la version validée le 20/10/2026');
    first.unmount();
    await renderSheet(sheetViewOf({ status: 'draft' }));
    expect(screen.getByTestId('sheet-foot-text')).toHaveTextContent('La fiche reste en brouillon tant qu’elle n’est pas validée');
  });
});

describe('the clarification the facilitator asks (B-4-56, B-4-57)', () => {
  it('on a validated sheet: a question of 1000 characters, sent with the version, then the sheet is « à revoir »', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ status: 'validated', validated, permissions: facilitator, version: 4 }), {
      'POST /api/steps/s2/sheet/clarification': { json: sheetViewOf({ status: 'needs_review', clarification_request: 'Quel outil ?', permissions: facilitator, version: 5 }) },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Demander une précision…' }));
    const question = screen.getByLabelText('Question à l’auteur de la fiche');
    expect([question.getAttribute('maxlength'), document.activeElement === question]).toEqual(['1000', true]);
    expect(screen.getByTestId('sheet-clarification-send')).toBeDisabled();
    fireEvent.change(question, { target: { value: '  Quel outil ?  ' } });
    await act(async () => {
      fireEvent.click(screen.getByTestId('sheet-clarification-send'));
      await Promise.resolve();
    });
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-state')).toHaveTextContent('à revoir');
    });
    const call = callsTo(sent, 'POST', '/api/steps/s2/sheet/clarification')[0];
    expect([call?.body, call?.headers.get('If-Match'), record.notes]).toEqual([{ text: 'Quel outil ?' }, '"4"', [['Demande envoyée : la fiche passe « à revoir ».', 'success']]]);
    expect(screen.getByRole('note')).toHaveTextContent('Quel outil ?');
  });

  it('none on a draft, nor for who may not ask; « Annuler » puts the question away', async () => {
    const first = await renderSheet(sheetViewOf({ status: 'draft', permissions: facilitator }));
    expect(screen.queryByRole('button', { name: 'Demander une précision…' })).toBeNull();
    first.unmount();
    const second = await renderSheet(sheetViewOf({ status: 'validated', validated }));
    expect(screen.queryByRole('button', { name: 'Demander une précision…' })).toBeNull();
    second.unmount();
    await renderSheet(sheetViewOf({ status: 'validated', validated, permissions: facilitator }));
    fireEvent.click(screen.getByRole('button', { name: 'Demander une précision…' }));
    fireEvent.click(screen.getByRole('button', { name: 'Annuler' }));
    expect(screen.queryByLabelText('Question à l’auteur de la fiche')).toBeNull();
    expect(screen.getByRole('button', { name: 'Demander une précision…' })).toBeInTheDocument();
  });
});
