// status.test.tsx
// The status of a pain point on a line of the panel « Plan d'actions » (ADR 116): the status button for who may fill the sheet where it is chosen (the receiving sheet, or its own step when no use carries it, R10), its list opened by a click or the keyboard, the arrows writing nothing, the choice written once with the version of the line, the plan read again, the message said and its « Undo », the day of the change on a line dealt with, read only in the BPMN view, a conflict said.

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { writesWaiting } from '../../../shared/state/pendingWrites.ts';
import { memoryStorage, renderMapScreen, showEditor, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi, heldReply, type FakeReply } from '../../../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';
import { sheetViewOf } from '../../../test/sheets.ts';
import { OFFERED_STATUSES } from '../../sheet/model/painPointStatus.ts';
import { bpmnView } from '../bpmn/bpmnState.ts';
import { createMapEditor } from '../state/editor.ts';
import { EMPTY_PLAN, planStore } from './planStore.ts';
import { closePlan } from './usePlan.ts';

type Line = Schema<'ActionPlanLine'>;

const PLAN = `GET /api/maps/${MAP_ID}/action-plan`;
const PLAN_PATH = `/api/maps/${MAP_ID}/action-plan`;
const STATUS_PATH = '/api/steps/s3/pain-points/p-VIS-I001/status';
const STATUS = `PUT ${STATUS_PATH}`;
const LIST = 'État de l’irritant VIS-I001';
const FIVE = [
  'Signalé — problème repéré, action à prévoir', 'En cours — action en cours de mise en œuvre', 'Réalisé — action faite, résultat à vérifier',
  'Adopté — vérifié, c’est la nouvelle façon de faire', '|', 'Abandonné — on décide de ne pas le traiter',
];

const named = (text: string): Schema<'NamedText'> => ({ text, source_language: 'fr', translations: {} });
const lineOf = (code: string, overrides: Partial<Line> = {}): Line => ({
  pain_point_id: `p-${code}`, code, description: `Irritant ${code}`, status: 'reported', lost_hours_per_month: 2, action: null, owner: null, due_date: null,
  map_id: MAP_ID, map: named('Visites'), step_id: 's2', step_code: 'VIS-E002', step: named('Vérifier la demande'), lane: named('Commercial'), open: true,
  receiving_step_id: 's3', receiving_map_id: MAP_ID, document_id: 'o1', can_fill: true, status_step_id: 's3', version: 4, status_changed_at: null, ...overrides,
});
const planOf = (lines: readonly Line[], total: number): Schema<'ActionPlanView'> => ({ lines: [...lines], open_lost_hours_per_month: total });
/** VIS-I001, open, which the person may fill; then dealt with in the application on the 21st, local time of Paris. */
const here = lineOf('VIS-I001');
const done = { ...here, status: 'implemented', open: false, version: 5, status_changed_at: '2026-11-21T09:15:00Z' } as const;
const written: FakeReply = { json: sheetViewOf({ step_id: 's3' }) };

const original = process.env.TZ;
beforeEach(async () => {
  await i18next.changeLanguage('fr');
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 10, 21, 12));
});

afterEach(() => {
  act(() => {
    closePlan();
    planStore.setState({ ...EMPTY_PLAN });
    bpmnView.setState({ open: false });
  });
  if (original === undefined) delete process.env.TZ;
  else process.env.TZ = original;
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const panel = () => screen.getByTestId('plan-panel');
const rows = () => within(panel()).queryAllByTestId(/^plan-line-/);
/** The status button of VIS-I001, named by its pain point and the status it shows. */
const button = () => within(panel()).getByRole<HTMLButtonElement>('button', { name: (name) => name.startsWith(`${LIST} : `) });
const buttons = () => within(panel()).queryAllByRole('button', { name: (name) => name.startsWith('État de l’irritant ') });
/** The list the button opened, or none. */
const menu = () => screen.queryByRole('menu', { name: LIST });
const items = () => within(menu() ?? document.body).getAllByRole('menuitemradio');
/** The entries of the open list, each by its name (the status and its help), a separator as « | ». */
const entries = () => items().flatMap((item) => [...(item.classList.contains('menu-item--group') ? ['|'] : []), item.getAttribute('aria-label') ?? '']);
/** The entries marked as the status of the moment. */
const marked = () => items().filter((item) => item.getAttribute('aria-checked') === 'true').map((item) => item.getAttribute('aria-label'));
const entry = (name: string | undefined) => within(menu() ?? document.body).getByRole('menuitemradio', { name: name ?? '' });
/** The message of the last change, told apart from the counts of the head (also a status). */
const said = () => within(panel()).queryByTestId('plan-change');

/** Opens the list of VIS-I001 by a click, and chooses an entry by a click too. */
async function choose(name: string | undefined) {
  await userEvent.click(button());
  await userEvent.click(entry(name));
}

/** The map shown, the plan opened by its button of the ribbon, the host answering as given. */
async function planning(plan: FakeReply | FakeReply[], routes: Record<string, FakeReply | FakeReply[]> = {}, map = sampleMap()) {
  const sent = fakeApi({ [PLAN]: plan, ...routes });
  const shown = await showEditor(map);
  fireEvent.click(screen.getByTestId('ribbon-plan'));
  await vi.waitFor(() => {
    expect(rows().length).toBeGreaterThan(0);
  });
  return { ...shown, sent, reads: () => callsTo(sent, 'GET', PLAN_PATH), writes: () => callsTo(sent, 'PUT', STATUS_PATH) };
}

describe('the status button on a line of the plan (ADR 116)', () => {
  it('a line the person may fill carries the status button, which shows the status alone and opens the five choices, each with its help; a reader’s line does not', async () => {
    await planning({ json: planOf([here, lineOf('VIS-I003', { can_fill: false }), lineOf('VIS-I004', { receiving_step_id: null, document_id: null, can_fill: false, status_step_id: 's2' })], 6) });
    // Closed, the button shows the status alone; its name holds the word shown (WCAG 2.5.3).
    expect([button().textContent, button().getAttribute('aria-haspopup'), button().getAttribute('aria-expanded')]).toEqual(['Signalé', 'menu', 'false']);
    expect(button()).toHaveAccessibleName('État de l’irritant VIS-I001 : Signalé');
    expect(menu()).toBeNull();
    await userEvent.click(button());
    expect([entries(), marked(), button().getAttribute('aria-expanded')]).toEqual([FIVE, [FIVE[0]], 'true']);
    expect(items().map((item) => item.dataset.testid)).toEqual(OFFERED_STATUSES.map((status) => `plan-status-of-VIS-I001-menu-${status}`));
    // Each choice shows its help on its own line under its name, both in the name read aloud.
    expect([...entry(FIVE[2]).querySelectorAll('.menu-name, .menu-help')].map((line) => line.textContent)).toEqual(['Réalisé', 'action faite, résultat à vérifier']);
    // The button is out of the button that leads to the step; the lines the person may not fill read the status.
    expect(button().parentElement?.closest('button')).toBeNull();
    expect(buttons().map((shown) => shown.closest('li')?.getAttribute('data-testid'))).toEqual(['plan-line-VIS-I001']);
    expect(rows()[1]).toHaveTextContent('signalé');
  });

  it('a pain point no use carries takes its status under its own step, as the host gives it, with the right to fill its sheet (R10)', async () => {
    const bare = lineOf('VIS-I005', { receiving_step_id: null, receiving_map_id: null, document_id: null, status_step_id: 's2', version: 2 });
    const own = '/api/steps/s2/pain-points/p-VIS-I005/status';
    const { sent } = await planning([{ json: planOf([bare], 2) }, { json: planOf([{ ...bare, status: 'in_progress', version: 3 }], 2) }],
      { [`PUT ${own}`]: { json: sheetViewOf() } });
    // No sheet to write its plan in: « Modifier le plan » is not offered.
    expect(within(panel()).queryByRole('button', { name: (name) => name.startsWith('Modifier le plan') })).toBeNull();
    await userEvent.click(within(panel()).getByRole('button', { name: 'État de l’irritant VIS-I005 : Signalé' }));
    await userEvent.click(within(screen.getByRole('menu', { name: 'État de l’irritant VIS-I005' })).getByRole('menuitemradio', { name: FIVE[1] }));
    await vi.waitFor(() => {
      expect(said()).toHaveTextContent('VIS-I005 : en cours.');
    });
    expect(callsTo(sent, 'PUT', own).map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'in_progress' }, '"2"']]);
    expect(callsTo(sent, 'PUT', STATUS_PATH)).toEqual([]);
  });

  it('a status the list does not offer, from a package, is shown where the seven place it', async () => {
    await planning({ json: planOf([{ ...here, status: 'scored' }], 2) });
    expect(button().textContent).toBe('Pris en compte');
    await userEvent.click(button());
    const scored = 'Pris en compte — irritant évalué, action à décider';
    expect([entries(), marked()]).toEqual([[FIVE[0], scored, ...FIVE.slice(1)], [scored]]);
  });

  it('in the BPMN view the status is read only', async () => {
    fakeApi({ [PLAN]: { json: planOf([here], 2) }, [`GET /api/maps/${MAP_ID}/pmap`]: { refused: 500, code: 'ERREUR_INTERNE' } });
    act(() => {
      bpmnView.setState({ open: true });
    });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), navigate: vi.fn() });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('bpmn-view');
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(1);
    });
    expect(buttons()).toEqual([]);
    expect(rows()[0]).toHaveTextContent('signalé');
  });
});

describe('the status chosen from the keyboard in the plan (ADR 116, review M1)', () => {
  it('Enter, Space or Alt+Down opens the list on the status of the moment; the arrows move and write nothing; Escape closes and gives the focus back to the button', async () => {
    const { writes } = await planning({ json: planOf([here], 2) }, { [STATUS]: written });
    for (const opening of ['{Enter}', ' ', '{Alt>}{ArrowDown}{/Alt}']) {
      act(() => {
        button().focus();
      });
      await userEvent.keyboard(opening);
      expect(document.activeElement).toBe(entry(FIVE[0]));
      await userEvent.keyboard('{ArrowDown}{ArrowDown}');
      expect(document.activeElement).toBe(entry(FIVE[2]));
      await userEvent.keyboard('{Escape}');
      expect([menu(), document.activeElement, button().textContent, button().getAttribute('aria-expanded')]).toEqual([null, button(), 'Signalé', 'false']);
    }
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect([writes(), said(), rows()]).toEqual([[], null, [screen.getByTestId('plan-line-VIS-I001')]]);
  });

  it('the arrows then Enter write the status chosen, once', async () => {
    const { writes } = await planning([{ json: planOf([here], 2) }, { json: planOf([done], 0) }], { [STATUS]: written });
    act(() => {
      button().focus();
    });
    await userEvent.keyboard('{Alt>}{ArrowDown}{/Alt}{ArrowDown}{ArrowDown}{Enter}');
    await vi.waitFor(() => {
      expect(said()).toHaveTextContent('VIS-I001 : réalisé.');
    });
    expect(writes().map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'implemented' }, '"4"']]);
  });

  it('the status of the moment chosen again writes nothing', async () => {
    const { writes } = await planning({ json: planOf([here], 2) }, { [STATUS]: written });
    act(() => {
      button().focus();
    });
    await userEvent.keyboard('{Enter}{Enter}');
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect([menu(), writes(), document.activeElement]).toEqual([null, [], button()]);
  });
});

describe('the status written as it is chosen in the plan (ADR 116)', () => {
  it('choosing implemented writes with the version of the line, the plan is read again, the line leaves Open, the message is said and Undo takes the focus', async () => {
    const { editor, sent, reads, writes } = await planning([{ json: planOf([here], 2) }, { json: planOf([done], 0) }], { [STATUS]: written });
    const reload = vi.spyOn(editor, 'reload');
    // The region that says the change is there before the change, empty: what comes into it is announced (review m7).
    const live = within(panel()).getByTestId('plan-change-live');
    expect([live.getAttribute('aria-live'), live.getAttribute('aria-atomic'), live.textContent]).toEqual(['polite', 'true', '']);
    await choose(FIVE[2]);
    await vi.waitFor(() => {
      expect(said()).toHaveTextContent('VIS-I001 : réalisé.');
    });
    expect(writes().map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'implemented' }, '"4"']]);
    // Read again after the write: the line leaves the open ones and the total, the map read again for its marks.
    expect(sent.filter((call) => call.path === PLAN_PATH || call.path === STATUS_PATH).map((call) => call.method)).toEqual(['GET', 'PUT', 'GET']);
    expect([reads().length, rows().length, reload.mock.calls.length]).toEqual([2, 0, 1]);
    expect(within(panel()).getByTestId('plan-total')).toHaveTextContent('0 h perdues par mois sur les irritants ouverts');
    expect(within(panel()).getByTestId('plan-change-live')).toBe(live);
    expect(live).toContainElement(said());
    // « Undo » is named by the word it shows first, then by what it does (WCAG 2.5.3).
    const undo = within(panel()).getByRole('button', { name: 'Annuler : remettre VIS-I001 à l’état « signalé »' });
    expect([undo.textContent, document.activeElement]).toEqual(['Annuler', undo]);
  });

  it('Undo writes the status before with the new version; the message stays until the next reading of the panel', async () => {
    const { reads, writes } = await planning([{ json: planOf([here], 2) }, { json: planOf([done], 0) }, { json: planOf([{ ...here, version: 6 }], 2) }], { [STATUS]: written });
    await choose(FIVE[2]);
    fireEvent.click(await within(panel()).findByRole('button', { name: 'Annuler : remettre VIS-I001 à l’état « signalé »' }));
    await vi.waitFor(() => {
      expect(said()).toHaveTextContent('VIS-I001 : signalé.');
    });
    expect(writes().map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'implemented' }, '"4"'], [{ status: 'reported' }, '"5"']]);
    expect([reads().length, rows().length, button().textContent]).toEqual([3, 1, 'Signalé']);
    // Undone in its turn, the change is told the same way and may be undone again.
    expect(document.activeElement).toBe(within(panel()).getByRole('button', { name: 'Annuler : remettre VIS-I001 à l’état « réalisé »' }));
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    await vi.waitFor(() => {
      expect(reads()).toHaveLength(4);
    });
    expect(said()).toBeNull();
  });

  it('the button is disabled while its write runs, the choice shown; the write is followed with the others', async () => {
    const held = heldReply();
    await planning([{ json: planOf([here], 2) }, { json: planOf([{ ...here, status: 'in_progress', version: 5 }], 2) }], { [STATUS]: held.reply });
    await choose(FIVE[1]);
    await vi.waitFor(() => {
      expect(writesWaiting()).toBe(true);
    });
    expect([button().disabled, button().textContent]).toEqual([true, 'En cours']);
    await act(async () => {
      held.give(written);
      await Promise.resolve();
    });
    await vi.waitFor(() => {
      expect(button().disabled).toBe(false);
    });
    expect([button().textContent, writesWaiting(), said()?.textContent]).toEqual(['En cours', false, 'VIS-I001 : en cours.Annuler']);
  });

  it('a conflict is said and the plan read again', async () => {
    const changed = { ...here, status: 'in_progress', version: 5 } as const;
    const { notes, reads } = await planning([{ json: planOf([here], 2) }, { json: planOf([changed], 2) }], {
      [STATUS]: { refused: 409, code: 'CONFLIT_VERSION', message: 'Modifié par quelqu’un d’autre.' },
    });
    await choose(FIVE[5]);
    await vi.waitFor(() => {
      expect(button().textContent).toBe('En cours');
    });
    expect([notes, reads().length, said(), button().disabled]).toEqual([[['Modifié par quelqu’un d’autre.', 'error']], 2, null, false]);
  });
});

describe('the day of the change on a line dealt with (ADR 116 point 7)', () => {
  it('a line dealt with says its status and the local day of the change, an open one its status only', async () => {
    // At 10:30 universal time on the 20th it is 0:30 on the 21st at Kiritimati: the day of the person, not of universal time.
    process.env.TZ = 'Pacific/Kiritimati';
    const dealt = { ...done, status_changed_at: '2026-11-20T10:30:00Z' };
    const going = lineOf('VIS-I002', { status: 'in_progress', status_changed_at: '2026-11-20T10:30:00Z', can_fill: false });
    await planning({ json: planOf([dealt, going], 2) });
    fireEvent.click(within(panel()).getByTestId('plan-filter-open'));
    const [first, second] = rows() as [HTMLElement, HTMLElement];
    expect(within(first).getByTestId('plan-status')).toHaveTextContent('réalisé depuis le 21/11/2026');
    expect(within(second).getByTestId('plan-status').textContent).toBe('en cours');
  });
});
