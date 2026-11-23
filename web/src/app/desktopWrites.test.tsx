// desktopWrites.test.tsx
// The tab of the desktop mode tells the host what still waits to be written, so that « Quit » by the icon near the clock waits for it: a typing of a sheet, a document, one declaration at a time, the page left; a form of the sheet changed and not validated, declared apart (R11); nothing in server mode (ADR 115).

import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { App } from './App.tsx';
import { declareWrites } from './desktopWrites.ts';
import { i18next } from '../shared/i18n/i18n.ts';
import { formsUnsaved, subscribeWrites, trackUnsavedForm, trackWrites, writesWaiting } from '../shared/state/pendingWrites.ts';
import { callsTo, fakeApi, heldReply, type SentCall } from '../test/fakeApi.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { renderSheet } from '../test/sheetPanel.tsx';
import { AUTHOR_ID, documentOf, inputOf, sheetViewOf } from '../test/sheets.ts';

const DECLARED = { json: null, status: 204 } as const;
const declarations = (sent: readonly SentCall[]) => callsTo(sent, 'POST', '/api/desktop/writes').map((call) => call.body);
const type = (testId: string, value: string) => {
  fireEvent.change(screen.getByTestId(testId), { target: { value } });
};
const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

/** The options given to fetch for each declaration, in order. */
const declarationInits = () => vi.mocked(fetch).mock.calls
  .filter(([input]) => input === '/api/desktop/writes')
  .map(([, init]) => init);

/** What each test started and ends, a test that fails included: the declarations, the queues followed. */
const ends: (() => void)[] = [];

/** Starts the declarations of the tab given, ended after the test. */
function declare(tab = 'tab-1'): () => void {
  const stop = declareWrites(tab);
  ends.push(stop);
  return stop;
}

/** A queue the test sets waiting or not, followed as a sheet follows its own, left after the test. */
function queueOf() {
  let waiting = false;
  const listeners = new Set<() => void>();
  const untrack = trackWrites({
    unsaved: () => waiting,
    flush: () => Promise.resolve(),
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  });
  ends.push(() => {
    waiting = false;
    untrack();
  });
  return {
    set(value: boolean) {
      waiting = value;
      for (const listener of listeners) listener();
    },
  };
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

// Each test leaves no queue followed: a sheet closed with a typing refused sends it once more, then leaves.
afterEach(async () => {
  for (const end of ends.splice(0)) end();
  cleanup();
  await vi.waitFor(() => {
    expect([writesWaiting(), formsUnsaved()]).toEqual([false, false]);
  });
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the writes of a sheet declared to the host of the desktop mode (ADR 115)', () => {
  it('a typing in a sheet declares « waiting » once, before its saving leaves, then « nothing waits » once the saving is answered', async () => {
    const reply = heldReply();
    const { sent } = await renderSheet(sheetViewOf(), { 'PUT /api/steps/s2/sheet': reply.reply, 'POST /api/desktop/writes': DECLARED });
    declare();
    type('sheet-purpose', 'F');
    type('sheet-purpose', 'Fa');
    await wait(1200);
    expect([declarations(sent), callsTo(sent, 'PUT', '/api/steps/s2/sheet').length]).toEqual([[{ tab: 'tab-1', waiting: true }], 1]);
    reply.give({ json: sheetViewOf({ status: 'draft', version: 1 }) });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }, { tab: 'tab-1', waiting: false }]);
    });
    // In order on the network: declared, saved, then the declaration lifted; each with the anti-CSRF header.
    expect(sent.filter((call) => call.path !== '/api/steps/s2/sheet' || call.method === 'PUT').map((call) => `${call.method} ${call.path}`))
      .toEqual(['POST /api/desktop/writes', 'PUT /api/steps/s2/sheet', 'POST /api/desktop/writes']);
    expect(callsTo(sent, 'POST', '/api/desktop/writes').map((call) => call.headers.get('X-Requested-With'))).toEqual(['XMLHttpRequest', 'XMLHttpRequest']);
  });

  it('a saving refused leaves the declaration « waiting »: the typing is not on the host', async () => {
    const { sent } = await renderSheet(sheetViewOf(), {
      'PUT /api/steps/s2/sheet': { refused: 422, code: 'DONNEES_INVALIDES' }, 'POST /api/desktop/writes': DECLARED,
    });
    declare();
    type('sheet-purpose', 'F');
    await wait(1200);
    await vi.waitFor(() => {
      expect(screen.getByTestId('sheet-saving')).toHaveTextContent('Non enregistré');
    });
    await wait(50);
    expect([declarations(sent), writesWaiting()]).toEqual([[{ tab: 'tab-1', waiting: true }], true]);
  });

  // A document ticked, a write without a form (R11: a form typed is declared apart, below).
  it('the write of a document declares « waiting » while it is asked, then « nothing waits » once answered', async () => {
    const reply = heldReply();
    const offered = inputOf(documentOf('o1', { name: 'Facture' }));
    const { sent } = await renderSheet(sheetViewOf({ inputs: [offered] }), { 'PUT /api/steps/s2/uses/o1': reply.reply, 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByTestId('input-tick-o1'));
    await vi.waitFor(() => {
      expect([declarations(sent), callsTo(sent, 'PUT', '/api/steps/s2/uses/o1').length]).toEqual([[{ tab: 'tab-1', waiting: true }], 1]);
    });
    reply.give({ json: sheetViewOf() });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }, { tab: 'tab-1', waiting: false }]);
    });
  });

});

const UNSAVED = { tab: 'tab-1', waiting: false, unsaved: true } as const;
const NOTHING = { tab: 'tab-1', waiting: false } as const;
const VALIDATED = { number: 1, validated_at: '2026-10-20T08:00:00Z', validated_by: AUTHOR_ID };
const FACILITATOR = { fill: true, validate: true, request_clarification: true, assign: true };

describe('a new document typed in the sheet and not validated, declared « unsaved » (R11, ADR 115)', () => {
  it('opened without typing declares nothing; a name typed in a new input from outside is declared at once; Annuler lifts it', async () => {
    const { sent } = await renderSheet(sheetViewOf(), { 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByRole('button', { name: 'Entrée extérieure' }));
    const form = screen.getByRole('group', { name: 'Nouvelle entrée extérieure' });
    await wait(50);
    expect([declarations(sent), formsUnsaved()]).toEqual([[], false]);
    fireEvent.change(within(form).getByLabelText('Nom du document'), { target: { value: 'Relevé' } });
    // Never sent by itself: declared at once, nothing to wait for.
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    expect(formsUnsaved()).toBe(true);
    fireEvent.click(within(form).getByRole('button', { name: 'Annuler' }));
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING]);
    });
    expect(formsUnsaved()).toBe(false);
  });

  it('a name typed then erased is the form as it opened: the declaration is lifted', async () => {
    const { sent } = await renderSheet(sheetViewOf(), { 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une sortie' }));
    const name = within(screen.getByRole('group', { name: 'Nouvelle sortie' })).getByLabelText('Nom du document');
    fireEvent.change(name, { target: { value: 'F' } });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    fireEvent.change(name, { target: { value: '' } });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING]);
    });
  });

  it('a new output validated: its write is waited for, then the form closed lifts everything', async () => {
    const reply = heldReply();
    const { sent } = await renderSheet(sheetViewOf(), { 'POST /api/steps/s2/documents': reply.reply, 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une sortie' }));
    const form = screen.getByRole('group', { name: 'Nouvelle sortie' });
    fireEvent.change(within(form).getByLabelText('Nom du document'), { target: { value: 'Facture' } });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    fireEvent.click(within(form).getByRole('button', { name: 'Enregistrer le document' }));
    await vi.waitFor(() => {
      expect(declarations(sent).at(-1)).toEqual({ tab: 'tab-1', waiting: true, unsaved: true });
    });
    reply.give({ json: sheetViewOf() });
    await vi.waitFor(() => {
      expect(screen.queryByTestId('document-form-output')).toBeNull();
    });
    await vi.waitFor(() => {
      expect(declarations(sent).at(-1)).toEqual(NOTHING);
    });
    expect(formsUnsaved()).toBe(false);
  });
});

describe('a document changed, a question of the facilitator, not validated, declared « unsaved » (R11, ADR 115)', () => {
  it('a document changed in its form is declared; the form opened on another document starts again from it; Escape lifts it', async () => {
    const output = (id: string, name: string) => ({ document: documentOf(id, { step_id: 's2', name }), used_by: [] });
    const { sent } = await renderSheet(sheetViewOf({ outputs: [output('o1', 'Facture'), output('o2', 'Avoir')] }), { 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByRole('button', { name: 'Modifier « Facture »' }));
    await wait(50);
    expect(declarations(sent)).toEqual([]);
    fireEvent.change(within(screen.getByTestId('document-form-output')).getByLabelText('Nom du document'), { target: { value: 'Facture v2' } });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    fireEvent.click(screen.getByRole('button', { name: 'Modifier « Avoir »' }));
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING]);
    });
    const other = within(screen.getByTestId('document-form-output')).getByLabelText('Nom du document');
    expect(other).toHaveValue('Avoir');
    fireEvent.change(other, { target: { value: 'Avoir v2' } });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING, UNSAVED]);
    });
    fireEvent.keyDown(other, { key: 'Escape' });
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING, UNSAVED, NOTHING]);
    });
  });

  it('the question of the facilitator typed and not sent is declared; Annuler lifts it', async () => {
    const { sent } = await renderSheet(sheetViewOf({ status: 'validated', validated: VALIDATED, permissions: FACILITATOR }), { 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByTestId('sheet-ask-clarification'));
    await wait(50);
    expect(declarations(sent)).toEqual([]);
    type('sheet-clarification-text', 'Quel canal ?');
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    fireEvent.click(screen.getByTestId('sheet-clarification-cancel'));
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING]);
    });
  });

  it('the question rewritten in place is declared once it differs from the question asked', async () => {
    const view = sheetViewOf({ status: 'needs_review', validated: VALIDATED, clarification_request: 'Quel canal ?', permissions: FACILITATOR, version: 6 });
    const { sent } = await renderSheet(view, { 'POST /api/desktop/writes': DECLARED });
    declare();
    fireEvent.click(screen.getByTestId('sheet-clarification-edit'));
    await wait(50);
    expect(declarations(sent)).toEqual([]);
    type('sheet-clarification-field', 'Quel canal, et quand ?');
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    fireEvent.click(screen.getByTestId('sheet-clarification-edit-cancel'));
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, NOTHING]);
    });
  });

});

describe('a form unsaved and the writes waiting, said together (R11, ADR 115)', () => {
  it('a form unsaved while a write waits: both in one declaration, then the form alone once the write is answered; the page left lifts both', async () => {
    const sent = fakeApi({ 'POST /api/desktop/writes': DECLARED });
    const queue = queueOf();
    declare();
    const release = trackUnsavedForm();
    ends.push(release);
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED]);
    });
    queue.set(true);
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([UNSAVED, { tab: 'tab-1', waiting: true, unsaved: true }]);
    });
    queue.set(false);
    await vi.waitFor(() => {
      expect(declarations(sent)).toHaveLength(3);
    });
    expect(declarations(sent)[2]).toEqual(UNSAVED);
    window.dispatchEvent(new Event('pagehide'));
    expect([declarations(sent).at(-1), declarationInits().at(-1)?.keepalive]).toEqual([NOTHING, true]);
    release();
    release();
    expect(formsUnsaved()).toBe(false);
  });
});

describe('the declarations of the tab: one at a time, the end, the page left (ADR 115)', () => {
  it('one declaration at a time: the next leaves once the one before is answered, and only if it still says something new', async () => {
    const first = heldReply();
    const sent = fakeApi({ 'POST /api/desktop/writes': [first.reply, DECLARED] });
    const queue = queueOf();
    declare();
    queue.set(true);
    queue.set(false);
    queue.set(true);
    queue.set(false);
    expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }]);
    first.give(DECLARED);
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }, { tab: 'tab-1', waiting: false }]);
    });
    queue.set(true);
    queue.set(true);
    await vi.waitFor(() => {
      expect(declarations(sent)).toHaveLength(3);
    });
    expect(declarations(sent)[2]).toEqual({ tab: 'tab-1', waiting: true });
    queue.set(false);
    await vi.waitFor(() => {
      expect(declarations(sent)).toHaveLength(4);
    });
  });

  it('nothing is declared while nothing waits, a refusal of the host is kept silent, and the end lifts what was declared', async () => {
    const sent = fakeApi({ 'POST /api/desktop/writes': { refused: 404, code: 'INTROUVABLE' } });
    const queue = queueOf();
    const stop = declare();
    queue.set(false);
    expect(declarations(sent)).toEqual([]);
    queue.set(true);
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }]);
    });
    stop();
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }, { tab: 'tab-1', waiting: false }]);
    });
    queue.set(false);
    queue.set(true);
    await wait(50);
    expect(declarations(sent)).toHaveLength(2);
  });

  it('the page left sends « nothing waits » with keepalive, then nothing more until the page is shown again', async () => {
    const sent = fakeApi({ 'POST /api/desktop/writes': DECLARED });
    const queue = queueOf();
    declare();
    queue.set(true);
    await vi.waitFor(() => {
      expect(declarations(sent)).toHaveLength(1);
    });
    window.dispatchEvent(new Event('pagehide'));
    expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }, { tab: 'tab-1', waiting: false }]);
    expect(declarationInits().map((init) => init?.keepalive)).toEqual([false, true]);
    queue.set(false);
    queue.set(true);
    await wait(50);
    expect(declarations(sent)).toHaveLength(2);
    // Back from the cache of the browser, the tab declares again what waits.
    window.dispatchEvent(new Event('pageshow'));
    await vi.waitFor(() => {
      expect(declarations(sent)).toEqual([{ tab: 'tab-1', waiting: true }, { tab: 'tab-1', waiting: false }, { tab: 'tab-1', waiting: true }]);
    });
  });

  it('the page left while nothing is declared sends nothing', () => {
    const sent = fakeApi({ 'POST /api/desktop/writes': DECLARED });
    declare();
    window.dispatchEvent(new Event('pagehide'));
    expect(declarations(sent)).toEqual([]);
  });
});

describe('the shell declares in desktop mode only (ADR 115)', () => {
  const routes = { 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, 'POST /api/desktop/writes': DECLARED };

  it('in desktop mode, the tab is named once by letters, digits and dashes, and declares what waits', async () => {
    const sent = fakeApi(routes);
    render(<App source={fakeSource([profileOf({ desktop: true })]).source} />);
    await screen.findByTestId('header-user');
    const queue = queueOf();
    queue.set(true);
    queue.set(false);
    await vi.waitFor(() => {
      expect(declarations(sent)).toHaveLength(2);
    });
    const [declared, lifted] = declarations(sent) as { tab: string; waiting: boolean }[];
    expect(declared?.tab).toMatch(/^[A-Za-z0-9-]{1,64}$/);
    expect([declared?.waiting, lifted?.tab, lifted?.waiting]).toEqual([true, declared?.tab, false]);
  });

  it('in server mode, nothing is declared, whatever waits', async () => {
    const sent = fakeApi(routes);
    render(<App source={fakeSource([profileOf({})]).source} />);
    await screen.findByTestId('header-user');
    const notices: boolean[] = [];
    const unsubscribe = subscribeWrites(() => notices.push(writesWaiting()));
    const queue = queueOf();
    queue.set(true);
    queue.set(false);
    await wait(50);
    expect([notices.length > 0, declarations(sent)]).toEqual([true, []]);
    unsubscribe();
  });
});
