// saving.test.tsx
// The sheet saved as it is typed in its panel: once after the last keystroke, cleaned, one send at a time, what the form sends, a failure and a conflict kept, the closing, the tab left, the command « Quit »; the gauges of the map and of the panel (annex B 4).

import { act, fireEvent, screen } from '@testing-library/react';
import { i18next } from '../../shared/i18n/i18n.ts';
import { sendPendingWrites } from '../../shared/state/pendingWrites.ts';
import { callsTo } from '../../test/fakeApi.ts';
import { renderSheet } from '../../test/sheetPanel.tsx';
import { contentOf, sheetViewOf } from '../../test/sheets.ts';

const saved = (version: number, overrides = {}) => ({ json: sheetViewOf({ status: 'draft', completeness: 20, version, ...overrides }) });
const type = (testId: string, value: string) => {
  fireEvent.change(screen.getByTestId(testId), { target: { value } });
};
const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the saving as typed (B-4-39 to B-4-42, B-4-47, B-4-50)', () => {
  it('leaves once, 1200 ms after the last keystroke, cleaned, with the version 0 of a sheet never saved; the reply gives the map its gauge', async () => {
    const { sent, record } = await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': saved(1, { content: contentOf({ purpose: 'Facturer' }) }) });
    type('sheet-purpose', ' Facturer ');
    await wait(600);
    type('sheet-actor', 'Assistante');
    await wait(1100);
    expect([callsTo(sent, 'PUT', '/api/steps/s2/sheet'), screen.getByTestId('sheet-saving').textContent]).toEqual([[], 'Modifications en attente…']);
    await wait(100);
    const calls = callsTo(sent, 'PUT', '/api/steps/s2/sheet');
    expect(calls.map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[
      { fields: { actor: { value: 'Assistante', number: null }, purpose: { value: 'Facturer', number: null } }, procedure: [], free_text: null }, '"0"',
    ]]);
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-saving')).toHaveTextContent('Enregistré');
    });
    expect([screen.getByTestId('sheet-purpose'), screen.getByTestId('sheet-actor')].map((field) => (field as HTMLInputElement).value)).toEqual([' Facturer ', 'Assistante']);
    expect(record.summaries.at(-1)).toEqual({ status: 'draft', completeness: 20, can_fill: true, frequency: null, duration_minutes: null, lead_time: null });
  });

  it('a keystroke during a send waits for it, then leaves with the version given back, the typing never replaced', async () => {
    const { sent } = await renderSheet(sheetViewOf({ version: 3 }), { 'PUT /api/steps/s2/sheet': [saved(4, { content: contentOf({ purpose: 'F' }) }), saved(5)] });
    type('sheet-purpose', 'F');
    await wait(1200);
    type('sheet-purpose', 'Fa');
    await wait(1200);
    expect(callsTo(sent, 'PUT', '/api/steps/s2/sheet').map((call) => [(call.body as { fields: { purpose: { value: string } } }).fields.purpose.value, call.headers.get('If-Match')]))
      .toEqual([['F', '"3"'], ['Fa', '"4"']]);
    expect(screen.getByTestId('sheet-purpose')).toHaveValue('Fa');
  });

  it('the gauge of the panel takes the completeness of the reply, under the help that no rubric is required (B-4-10, B-4-11)', async () => {
    await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': saved(1) });
    const gauge = () => screen.getByRole('progressbar', { name: 'Complétude de la fiche' });
    expect([gauge().getAttribute('aria-valuenow'), gauge().parentElement?.querySelector('b')?.textContent]).toEqual(['0', '0 %']);
    type('sheet-purpose', 'Facturer');
    await wait(1200);
    await vi.waitFor(() => {
      expect(gauge()).toHaveAttribute('aria-valuenow', '20');
    });
    const head = gauge().parentElement;
    expect([head?.querySelector('b')?.textContent, head?.getAttribute('title')])
      .toEqual(['20 %', 'Part des rubriques renseignées, calculée par le serveur. Aucune n’est obligatoire.']);
  });
});

describe('what the panel sends of its form (B-4-20, B-4-24 to B-4-37, B-4-47)', () => {
  const sentBodies = (sent: Awaited<ReturnType<typeof renderSheet>>['sent']) => callsTo(sent, 'PUT', '/api/steps/s2/sheet').map((call) => call.body);

  it('the procedure as typed: a second line made by Enter, a nature on it, a tool on the first, the second moved up', async () => {
    const { sent } = await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': saved(1) });
    type('procedure-text-1', 'A');
    fireEvent.keyDown(screen.getByTestId('procedure-text-1'), { key: 'Enter' });
    expect(screen.getByTestId('procedure-text-2')).toHaveFocus();
    type('procedure-text-2', 'B');
    type('procedure-nature-2', 'check');
    type('procedure-tool-1', 'ERP');
    fireEvent.click(screen.getByRole('button', { name: 'Monter l’opération 2' }));
    await wait(1200);
    expect(sentBodies(sent)).toEqual([
      { fields: {}, procedure: [{ text: 'B', tool: null, nature: 'check' }, { text: 'A', tool: 'ERP', nature: null }], free_text: null },
    ]);
  });

  it('the tools completed by a pill, whatever their case, the frequency taken from another step and the duration leave together', async () => {
    const view = sheetViewOf({ content: contentOf({ tools: 'Tableur' }), suggestions: { frequency: { frequency: 'monthly', step_code: 'VTE-E001' }, tools: ['tableur', 'ERP'] } });
    const { sent } = await renderSheet(view, { 'PUT /api/steps/s2/sheet': saved(1) });
    expect(screen.queryByRole('button', { name: 'Ajouter tableur' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter ERP' }));
    fireEvent.click(screen.getByRole('button', { name: 'Reprendre « Mensuelle » (étape VTE-E001)' }));
    type('sheet-duration', '45');
    await wait(1200);
    expect(sentBodies(sent)).toEqual([{
      fields: { tools: { value: 'Tableur, ERP', number: null }, frequency: { value: 'monthly', number: null }, duration_minutes: { value: null, number: 45 } },
      procedure: [], free_text: null,
    }]);
  });

  it('the free text and the answers of the form leave in the same send', async () => {
    const { sent } = await renderSheet(sheetViewOf({ content: contentOf({ purpose: 'Facturer' }) }), { 'PUT /api/steps/s2/sheet': saved(1) });
    fireEvent.click(screen.getByRole('radio', { name: 'Écrire librement' }));
    type('sheet-free-text', 'Le fichier arrive le 2 du mois.');
    await wait(1200);
    expect(sentBodies(sent)).toEqual([{ fields: { purpose: { value: 'Facturer', number: null } }, procedure: [], free_text: 'Le fichier arrive le 2 du mois.' }]);
  });
});

describe('a send that fails (B-4-42 to B-4-44)', () => {
  it('« Non enregistré » in red, the refusal said, the typing kept and sent again at the next keystroke', async () => {
    const { sent, record } = await renderSheet(sheetViewOf(), {
      'PUT /api/steps/s2/sheet': [{ refused: 503, code: 'HTTP_503', message: 'Serveur indisponible.' }, saved(1)],
    });
    type('sheet-purpose', 'Facturer');
    await wait(1200);
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-saving')).toHaveClass('sheet-saving--failed');
    });
    expect([screen.getByTestId('sheet-saving').textContent, record.notes]).toEqual(['Non enregistré', [['Serveur indisponible.', 'error']]]);
    expect(screen.getByTestId('sheet-purpose')).toHaveValue('Facturer');
    type('sheet-actor', 'A');
    await wait(1200);
    const bodies = callsTo(sent, 'PUT', '/api/steps/s2/sheet').map((call) => Object.keys((call.body as { fields: object }).fields));
    expect(bodies).toEqual([['purpose'], ['actor', 'purpose']]);
  });

  it('a conflict reads the sheet again for its version, the typing left as it is; the next send carries the version read', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ version: 3 }), {
      'GET /api/steps/s2/sheet': [{ json: sheetViewOf({ version: 3 }) }, { json: sheetViewOf({ version: 5, content: contentOf({ purpose: 'Autre texte' }) }) }],
      'PUT /api/steps/s2/sheet': [{ refused: 409, code: 'CONFLIT_VERSION', message: 'Modifiée par quelqu’un d’autre.' }, saved(6)],
    });
    type('sheet-purpose', 'Mon texte');
    await wait(1200);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'GET', '/api/steps/s2/sheet')).toHaveLength(2);
    });
    expect(record.notes).toEqual([['Modifiée par quelqu’un d’autre.', 'error']]);
    expect(screen.getByTestId('sheet-purpose')).toHaveValue('Mon texte');
    type('sheet-purpose', 'Mon texte.');
    await wait(1200);
    expect(callsTo(sent, 'PUT', '/api/steps/s2/sheet').map((call) => call.headers.get('If-Match'))).toEqual(['"3"', '"5"']);
  });
});

describe('leaving the sheet (B-4-45, B-4-46)', () => {
  it('closing the panel sends at once what is left', async () => {
    const { sent, unmount } = await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': saved(1) });
    type('sheet-purpose', 'Facturer');
    unmount();
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/steps/s2/sheet')).toHaveLength(1);
    });
  });

  it('the browser asks before the tab closes while something typed is not on the host, and sends it', async () => {
    const { sent } = await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': saved(1) });
    const leave = () => {
      const event = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(event);
      return event.defaultPrevented;
    };
    expect(leave()).toBe(false);
    type('sheet-purpose', 'Facturer');
    expect(leave()).toBe(true);
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-saving')).toHaveTextContent('Enregistré');
    });
    expect([callsTo(sent, 'PUT', '/api/steps/s2/sheet').length, leave()]).toEqual([1, false]);
  });

  it('what is typed waits in the queue the command « Quit » sends first, at once, then nothing is left (spec 5.2)', async () => {
    const { sent, unmount } = await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': saved(1) });
    type('sheet-purpose', 'Facturer');
    expect(await act(() => sendPendingWrites())).toBe(true);
    expect(callsTo(sent, 'PUT', '/api/steps/s2/sheet')).toHaveLength(1);
    expect(await sendPendingWrites()).toBe(true);
    unmount();
  });
});
