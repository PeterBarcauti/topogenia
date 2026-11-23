// ActionPlanPanel.test.tsx
// The panel « Plan d'actions » (ADR 112 points 5 and 6): the total of the open hours at its head, the lines and their marks, the filters and the sorts, a line that leads to its step, the plan read from the root map at each showing (on the map shown next after a sheet covered it, once the last field typed is written), one panel at a time, « Edit the plan » through the sheet of the step that receives, on its map.

import { act, cleanup, fireEvent, screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { chooseProcessLanguage, processLanguage } from '../../../shared/state/processLanguage.ts';
import { memoryStorage, openSheetOf, renderMapScreen, showEditor, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi, heldReply, type FakeReply } from '../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { documentOf, documentUseOf, inputOf, painPointOf, sheetViewOf } from '../../../test/sheets.ts';
import { bpmnView } from '../bpmn/bpmnState.ts';
import { EMPTY_SEARCH, searchStore } from '../search/searchStore.ts';
import { closeSearch } from '../search/useSearch.ts';
import { createMapEditor } from '../state/editor.ts';
import { EMPTY_PLAN, planStore } from './planStore.ts';
import { closePlan } from './usePlan.ts';

type Line = Schema<'ActionPlanLine'>;

const OTHER_MAP = '0190a1b2-0000-7000-8000-00000000a002';
const OTHER_PROJECT = '0190a1b2-0000-7000-8000-00000000b002';
const PLAN = `GET /api/maps/${MAP_ID}/action-plan`;
const PLAN_PATH = `/api/maps/${MAP_ID}/action-plan`;

const named = (text: string, translations: Record<string, string> = {}): Schema<'NamedText'> => ({ text, source_language: 'fr', translations });
const lineOf = (code: string, overrides: Partial<Line> = {}): Line => ({
  pain_point_id: `p-${code}`, code, description: `Irritant ${code}`, status: 'reported', lost_hours_per_month: null, action: null, owner: null, due_date: null,
  map_id: MAP_ID, map: named('Visites'), step_id: 's2', step_code: 'VIS-E002', step: named('Vérifier la demande', { en: 'Check the request' }),
  lane: named('Commercial'), open: true, receiving_step_id: 's3', receiving_map_id: MAP_ID, document_id: 'd1', can_fill: false, status_step_id: 's3', version: 1, status_changed_at: null, ...overrides,
});

/**
 * Three pain points in the order of the project: one open with its plan, due on the 15th (late on the 20th); one open on
 * a detailed map, without action nor due date, the most hours; one adopted, due on the 10th (dealt with, never late).
 */
const LINES: readonly Line[] = [
  lineOf('VIS-I001', { status: 'scored', lost_hours_per_month: 3.5, action: 'Rappel automatique au bout de 48 h', owner: 'Service RH', due_date: '2026-12-15' }),
  lineOf('VIS-I002', { lost_hours_per_month: 6, map_id: OTHER_MAP, map: named('Saisir la demande'), step_id: 's3', step_code: 'VIS-E003', step: named('Remplir'), lane: null, receiving_map_id: OTHER_MAP }),
  lineOf('VIS-I003', { status: 'adopted', open: false, action: 'Formulaire unique', due_date: '2026-12-10' }),
];
const planOf = (lines: readonly Line[] = LINES, total = 9.5): Schema<'ActionPlanView'> => ({ lines: [...lines], open_lost_hours_per_month: total });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  // Only the date is fixed, the 20 December 2026 at noon, local time: the timers stay those of the browser.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 11, 20, 12));
});

afterEach(() => {
  act(() => {
    closePlan();
    planStore.setState({ ...EMPTY_PLAN });
    closeSearch();
    searchStore.setState({ ...EMPTY_SEARCH });
    bpmnView.setState({ open: false });
    processLanguage.setState({ chosen: {} });
  });
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const panel = () => screen.getByTestId('plan-panel');
const rows = () => within(panel()).queryAllByTestId(/^plan-line-/);
const codes = () => rows().map((row) => row.getAttribute('data-testid')?.replace('plan-line-', ''));
const filter = (name: 'open' | 'late' | 'no-action') => within(panel()).getByTestId(`plan-filter-${name}`);
const status = () => within(panel()).getByRole('status');

/** The map shown (the root map unless told), the plan opened by its button of the ribbon, the host answering as given. */
async function planning(reply: FakeReply | FakeReply[] = { json: planOf() }, map = sampleMap(), routes: Record<string, FakeReply> = {}) {
  const sent = fakeApi({ [PLAN]: reply, ...routes });
  const shown = await showEditor(map);
  fireEvent.click(screen.getByTestId('ribbon-plan'));
  return { ...shown, sent, reads: () => callsTo(sent, 'GET', PLAN_PATH) };
}

/** The plan shown with its three lines, every filter off. */
async function allLines() {
  const shown = await planning();
  await vi.waitFor(() => {
    expect(rows()).toHaveLength(2);
  });
  fireEvent.click(filter('open'));
  expect(rows()).toHaveLength(3);
  return shown;
}

describe('the head and the lines of the plan (ADR 112 point 5)', () => {
  it('the total of the hours lost by the open pain points heads the panel, whatever the filters', async () => {
    await planning();
    const total = await within(await screen.findByTestId('plan-panel')).findByTestId('plan-total');
    expect(total).toHaveTextContent('9,5 h perdues par mois sur les irritants ouverts');
    fireEvent.click(filter('late'));
    fireEvent.click(filter('open'));
    expect(rows()).toHaveLength(1);
    expect(within(panel()).getByTestId('plan-total')).toHaveTextContent('9,5 h perdues par mois sur les irritants ouverts');
  });

  it('a line shows the pain point, its step, map and lane, its action, owner, due date, status and hours', async () => {
    await allLines();
    const [first, second] = rows() as [HTMLElement, HTMLElement];
    for (const text of ['VIS-I001', 'Irritant VIS-I001', 'VIS-E002 Vérifier la demande', 'Carte : Visites', 'Couloir : Commercial', 'Action : Rappel automatique au bout de 48 h',
      'Responsable : Service RH', 'Échéance : 15/12/2026', 'pris en compte', '3,5 h perdues par mois']) expect(first).toHaveTextContent(text);
    // No lane, no action, no owner, no due date: the lane and the dates are left out, the action said missing.
    expect(second).toHaveTextContent('Carte : Saisir la demande');
    expect(second).toHaveTextContent('Aucune action décidée');
    for (const word of ['Couloir', 'Responsable', 'Échéance']) expect(second).not.toHaveTextContent(word);
    expect(within(first).getByRole('button', { name: 'Aller à VIS-E002' })).toBeInTheDocument();
  });

  it('the names are those of the language the map is shown in, the words of the interface its own; the button closes the panel', async () => {
    chooseProcessLanguage(PROJECT_ID, 'en');
    await planning();
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    expect(rows()[0]).toHaveTextContent('VIS-E002 Check the request');
    expect(rows()[0]).toHaveTextContent('pris en compte');
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    expect(screen.queryByTestId('plan-panel')).toBeNull();
  });

  it('a late line says so and the count of late lines is given', async () => {
    await allLines();
    const [first, second, third] = rows() as [HTMLElement, HTMLElement, HTMLElement];
    expect(within(first).getByTestId('plan-late')).toHaveTextContent('En retard');
    expect([within(second).queryByTestId('plan-late'), within(third).queryByTestId('plan-late')]).toEqual([null, null]);
    expect(status()).toHaveTextContent('En retard : 1');
    // The due date of the day is not late: on the 15th, nothing is.
    act(() => {
      vi.setSystemTime(new Date(2026, 11, 15, 23, 30));
    });
    fireEvent.click(filter('late'));
    fireEvent.click(filter('late'));
    expect(within(panel()).queryAllByTestId('plan-late')).toHaveLength(0);
    expect(status()).toHaveTextContent('En retard : 0');
  });
});

describe('the filters and the sorts (ADR 112 point 5)', () => {
  it('open is pressed at first; late and without action narrow the list and 2 of 3 is said', async () => {
    await planning();
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    expect([filter('open'), filter('late'), filter('no-action')].map((button) => button.getAttribute('aria-pressed'))).toEqual(['true', 'false', 'false']);
    expect(codes()).toEqual(['VIS-I001', 'VIS-I002']);
    expect(status()).toHaveTextContent('En retard : 1 · Irritants montrés : 2 sur 3');
    fireEvent.click(filter('late'));
    expect(codes()).toEqual(['VIS-I001']);
    expect(status()).toHaveTextContent('Irritants montrés : 1 sur 3');
    fireEvent.click(filter('late'));
    fireEvent.click(filter('no-action'));
    expect(codes()).toEqual(['VIS-I002']);
    expect(filter('no-action')).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(filter('late'));
    expect(rows()).toHaveLength(0);
    expect(panel()).toHaveTextContent('Aucun irritant ne correspond aux filtres.');
    expect(status()).toHaveTextContent('Irritants montrés : 0 sur 3');
  });

  it('the three sorts, and back to the order of the project', async () => {
    await allLines();
    const sort = within(panel()).getByRole('combobox', { name: 'Trier par' });
    expect([...sort.querySelectorAll('option')].map((option) => option.textContent)).toEqual(['Ordre du projet', 'Heures perdues', 'Échéance']);
    expect(codes()).toEqual(['VIS-I001', 'VIS-I002', 'VIS-I003']);
    fireEvent.change(sort, { target: { value: 'hours' } });
    expect(codes()).toEqual(['VIS-I002', 'VIS-I001', 'VIS-I003']);
    fireEvent.change(sort, { target: { value: 'due' } });
    expect(codes()).toEqual(['VIS-I003', 'VIS-I001', 'VIS-I002']);
    fireEvent.change(sort, { target: { value: 'project' } });
    expect(codes()).toEqual(['VIS-I001', 'VIS-I002', 'VIS-I003']);
  });

  it('a project without pain point says so; no line left by the filters says so; a refusal of the host is said', async () => {
    await planning([{ json: planOf([], 0) }, { refused: 404, code: 'INTROUVABLE', message: 'La carte est introuvable.' }]);
    expect(await within(await screen.findByTestId('plan-panel')).findByText('Aucun irritant dans ce projet.')).toBeInTheDocument();
    expect(within(panel()).queryByTestId('plan-total')).toBeNull();
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    expect(await within(panel()).findByRole('alert')).toHaveTextContent('La carte est introuvable.');
    expect(rows()).toHaveLength(0);
  });
});

describe('a line leads to its step (ADR 112 point 5)', () => {
  it('a line of this map chooses its step and puts it in view; a line of another map opens it, the step chosen on arrival, the panel open', async () => {
    const { editor, opened, reads } = await planning();
    const reveal = vi.spyOn(editor.canvas, 'reveal');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    fireEvent.click(within(rows()[0] as HTMLElement).getByRole('button', { name: 'Aller à VIS-E002' }));
    expect(editor.store.getState().selection.steps).toEqual(['s2']);
    expect(reveal).toHaveBeenLastCalledWith(['s2']);
    fireEvent.click(within(rows()[1] as HTMLElement).getByRole('button', { name: 'Aller à VIS-E003' }));
    expect(opened).toEqual([`/maps/${OTHER_MAP}`]);
    cleanup();
    const root = sampleMap().breadcrumb[0] as Schema<'MapView'>['breadcrumb'][number];
    const detail = await showEditor(sampleMap({ id: OTHER_MAP, depth: 2, breadcrumb: [root, { ...root, map_id: OTHER_MAP, name: 'Saisir la demande' }] }));
    expect(detail.editor.store.getState().selection.steps).toEqual(['s3']);
    expect(await screen.findByTestId('plan-panel')).toBeInTheDocument();
    expect(codes()).toEqual(['VIS-I001', 'VIS-I002']);
    // Changing the map reads nothing again.
    expect(reads()).toHaveLength(1);
  });

  it('in the BPMN view, a line goes back to the map', async () => {
    fakeApi({ [PLAN]: { json: planOf() }, [`GET /api/maps/${MAP_ID}/pmap`]: { refused: 500, code: 'ERREUR_INTERNE' } });
    act(() => {
      bpmnView.setState({ open: true });
    });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), navigate: vi.fn() });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('bpmn-view');
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    fireEvent.click(within(rows()[0] as HTMLElement).getByRole('button', { name: 'Aller à VIS-E002' }));
    expect(bpmnView.getState().open).toBe(false);
    expect(await screen.findByTestId('map-canvas')).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(editor.store.getState().selection.steps).toEqual(['s2']);
    });
    expect(screen.getByTestId('plan-panel')).toBeInTheDocument();
  });

  it('Escape closes the panel and gives the focus back to the map', async () => {
    const { canvas } = await planning();
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    const go = within(rows()[0] as HTMLElement).getByRole('button', { name: 'Aller à VIS-E002' });
    act(() => {
      go.focus();
    });
    fireEvent.keyDown(go, { key: 'Escape' });
    expect(screen.queryByTestId('plan-panel')).toBeNull();
    expect(canvas.contains(document.activeElement)).toBe(true);
  });
});

describe('the reading of the plan (ADR 112 point 5)', () => {
  it('the plan is read from the root map each time it is shown, a new read cancels the one before', async () => {
    const first = heldReply();
    const root = sampleMap().breadcrumb[0] as Schema<'MapView'>['breadcrumb'][number];
    const detailed = sampleMap({ id: OTHER_MAP, depth: 2, breadcrumb: [root, { ...root, map_id: OTHER_MAP, name: 'Saisir la demande' }] });
    const { editor, reads } = await planning([first.reply, { json: planOf([LINES[1] as Line], 6) }, { json: planOf() }], detailed, {
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } },
    });
    expect(reads()).toHaveLength(1);
    expect(await within(await screen.findByTestId('plan-panel')).findByText('Chargement du plan…')).toBeInTheDocument();
    // A sheet covers the plan; the plan shown again as it closes is read again, the read before it cancelled.
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    act(() => {
      editor.store.setState({ openSheet: null });
    });
    expect(reads()).toHaveLength(2);
    await vi.waitFor(() => {
      expect(codes()).toEqual(['VIS-I002']);
    });
    await act(async () => {
      first.give({ json: planOf() });
      await Promise.resolve();
    });
    expect(codes()).toEqual(['VIS-I002']);
    // Closed and opened again: read again.
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    await vi.waitFor(() => {
      expect(codes()).toEqual(['VIS-I001', 'VIS-I002']);
    });
    expect(reads()).toHaveLength(3);
  });

  it('the plan of a project is not shown in another one', async () => {
    await planning();
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    cleanup();
    const elsewhere = await showEditor(sampleMap({ id: OTHER_MAP, project_id: OTHER_PROJECT }));
    expect(elsewhere.canvas).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(planStore.getState()).toMatchObject({ projectId: null, open: false, reply: null });
    });
    expect(screen.queryByTestId('plan-panel')).toBeNull();
  });
});

describe('one panel at a time (ADR 112 point 5)', () => {
  it('a sheet opened covers the plan, which comes back read again when it closes; the assistant, a layer turned on or the search opened closes it; opening the plan closes the search', async () => {
    const { editor, reads } = await planning({ json: planOf() }, sampleMap(), {
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } },
    });
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    const dock = () => screen.getByTestId('side-dock').getAttribute('data-panel');
    expect([dock(), screen.getByTestId('ribbon-plan').getAttribute('aria-pressed')]).toEqual(['plan', 'true']);
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    expect(dock()).toBe('sheet');
    expect(screen.queryByTestId('plan-panel')).toBeNull();
    act(() => {
      editor.store.setState({ openSheet: null });
    });
    expect(dock()).toBe('plan');
    expect(reads()).toHaveLength(2);
    act(() => {
      editor.assistant.getState().setOpen(true);
    });
    expect([dock(), planStore.getState().open, screen.getByTestId('ribbon-plan').getAttribute('aria-pressed')]).toEqual(['assistant', false, 'false']);
    // Opening the plan closes the assistant and the sheet.
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    expect([editor.store.getState().openSheet, editor.assistant.getState().open, dock()]).toEqual([null, false, 'plan']);
    // Read once: the sheet the opening closes uncovers nothing.
    expect(reads()).toHaveLength(3);
    act(() => {
      editor.preferences.getState().set('showAnalysis', true);
    });
    expect([planStore.getState().open, dock()]).toEqual([false, 'analysis']);
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    expect(dock()).toBe('plan');
    // The search and the plan exclude each other.
    fireEvent.click(screen.getByTestId('ribbon-find'));
    expect([planStore.getState().open, searchStore.getState().open, dock()]).toEqual([false, true, 'search']);
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    expect([planStore.getState().open, searchStore.getState().open, dock()]).toEqual([true, false, 'plan']);
  });
});

const THIRD_MAP = '0190a1b2-0000-7000-8000-00000000a003';
const invoice = documentOf('o1', { step_id: 's2', name: 'Facture' });
/** The sheet of s3, which receives « Facture » from s2 with the pain point VIS-I001 on it: where the pain point and its plan are written. */
const pain = painPointOf('p-VIS-I001', { code: 'VIS-I001', step_id: 's2', description: 'Irritant VIS-I001' });
const sheets = {
  'GET /api/steps/s3/sheet': {
    json: sheetViewOf({ step_id: 's3', inputs: [inputOf(invoice, { used: true, use: documentUseOf('o1', 's3', { pain_point_id: pain.id }) })], pain_points: [pain] }),
  },
  [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } },
};
const here = lineOf('VIS-I001', { document_id: 'o1', can_fill: true });
const away = lineOf('VIS-I002', { map_id: OTHER_MAP, map: named('Saisir la demande'), receiving_map_id: OTHER_MAP, document_id: 'o1', can_fill: true });
const mapUnder = (id: string, name: string) => {
  const root = sampleMap().breadcrumb[0] as Schema<'MapView'>['breadcrumb'][number];
  return sampleMap({ id, depth: 2, breadcrumb: [root, { ...root, map_id: id, name }] });
};
const edits = () => within(panel()).queryAllByRole('button', { name: (name) => name.startsWith('Modifier le plan de ') });
/** Waits for the effects of what was just drawn before the caret and the sheet are read: a late one would be seen. */
const settled = () => act(() => new Promise<void>((resolve) => { setTimeout(resolve, 20); }));
/** The sheet asked is open on s3, the inputs unfolded and the others folded, the caret in the pain point of « Facture ». */
async function sheetAsked(editor: ReturnType<typeof createMapEditor>) {
  const field = await screen.findByLabelText('Irritant sur « Facture »');
  expect(editor.store.getState().openSheet).toBe('s3');
  expect(within(screen.getByTestId('sheet-panel')).getByTestId('sheet-title')).toHaveTextContent('VIS-E003');
  expect(['inputs', 'procedure'].map((section) => screen.getByTestId(`sheet-fold-${section}`).getAttribute('aria-expanded'))).toEqual(['true', 'false']);
  expect([screen.getByLabelText('Action décidée sur « Facture »'), screen.getByLabelText('Échéance de l’action sur « Facture »')]).toHaveLength(2);
  await vi.waitFor(() => {
    expect(document.activeElement).toBe(field);
  });
}

describe('Edit the plan, on the map of the line (ADR 112 point 6)', () => {
  it('Edit the plan is offered on the lines whose receiving sheet the person may fill, never to a reader', async () => {
    await planning({ json: planOf([here, lineOf('VIS-I003', { can_fill: false })], 0) });
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    expect(edits().map((button) => [button.textContent, button.closest('li')?.getAttribute('data-testid')])).toEqual([['Modifier le plan', 'plan-line-VIS-I001']]);
    cleanup();
    act(() => {
      closePlan();
      planStore.setState({ ...EMPTY_PLAN });
    });
    // A reader: the host gives no line he may fill; every line is read and leads to its step all the same.
    await planning({ json: planOf([lineOf('VIS-I001'), lineOf('VIS-I003')], 0) }, sampleMap({ rights: { structure: false, role: 'reader' } }));
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    expect([edits(), within(panel()).getAllByRole('button', { name: (name) => name.startsWith('Aller à ') })].map((buttons) => buttons.length)).toEqual([0, 2]);
  });

  it('on this map it opens the receiving sheet, its inputs unfolded, the field of the document focused', async () => {
    const { editor } = await planning({ json: planOf([here], 0) }, sampleMap(), sheets);
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I001' }));
    await sheetAsked(editor);
    // The sheet covers the plan, kept open under it.
    expect([screen.getByTestId('side-dock').getAttribute('data-panel'), planStore.getState().open]).toEqual(['sheet', true]);
  });

  it('in the BPMN view, Edit the plan goes back to the map, where the sheet can be filled', async () => {
    fakeApi({ [PLAN]: { json: planOf([here], 0) }, [`GET /api/maps/${MAP_ID}/pmap`]: { refused: 500, code: 'ERREUR_INTERNE' }, ...sheets });
    act(() => {
      bpmnView.setState({ open: true });
    });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), navigate: vi.fn() });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('bpmn-view');
    fireEvent.click(screen.getByTestId('ribbon-plan'));
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I001' }));
    expect(bpmnView.getState().open).toBe(false);
    await sheetAsked(editor);
    expect(screen.getByLabelText('Irritant sur « Facture »')).not.toHaveAttribute('readonly');
  });
});

describe('Edit the plan, on another map (ADR 112 point 6)', () => {
  it('on another map it opens that map then that sheet, the field focused, once', async () => {
    const { opened } = await planning({ json: planOf([away], 0) }, sampleMap(), sheets);
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I002' }));
    expect(opened).toEqual([`/maps/${OTHER_MAP}`]);
    expect(screen.queryByTestId('sheet-panel')).toBeNull();
    cleanup();
    const detail = await showEditor(mapUnder(OTHER_MAP, 'Saisir la demande'));
    await sheetAsked(detail.editor);
    // Asked once: the sheet opened again from the map is folded as any other, the map shown again opens no sheet.
    fireEvent.click(screen.getByTestId('sheet-close'));
    expect(await screen.findByTestId('plan-panel')).toBeInTheDocument();
    openSheetOf(detail.canvas, 's3');
    await screen.findByTestId('sheet-panel');
    await settled();
    expect([screen.getByTestId('sheet-fold-inputs').getAttribute('aria-expanded'), screen.queryByLabelText('Irritant sur « Facture »')]).toEqual(['false', null]);
    cleanup();
    const later = await showEditor(mapUnder(OTHER_MAP, 'Saisir la demande'));
    await settled();
    expect([later.editor.store.getState().openSheet, screen.queryByTestId('sheet-panel')]).toEqual([null, null]);
  });

  it('another map opened in between forgets the request', async () => {
    const { opened } = await planning({ json: planOf([away], 0) }, sampleMap(), sheets);
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I002' }));
    expect(opened).toEqual([`/maps/${OTHER_MAP}`]);
    cleanup();
    const between = await showEditor(mapUnder(THIRD_MAP, 'Archiver'));
    await settled();
    expect(between.editor.store.getState().openSheet).toBeNull();
    cleanup();
    const detail = await showEditor(mapUnder(OTHER_MAP, 'Saisir la demande'));
    await settled();
    expect([detail.editor.store.getState().openSheet, screen.queryByTestId('sheet-panel')]).toEqual([null, null]);
    expect(await screen.findByTestId('plan-panel')).toBeInTheDocument();
  });

  it('the sheet closed, the plan comes back read again with the line changed', async () => {
    const planned = { ...here, action: 'Formulaire unique', due_date: '2026-12-31' };
    const { editor, reads } = await planning([{ json: planOf([here], 0) }, { json: planOf([planned], 0) }], sampleMap(), sheets);
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I001' }));
    await sheetAsked(editor);
    expect(reads()).toHaveLength(1);
    fireEvent.click(screen.getByTestId('sheet-close'));
    expect([screen.getByTestId('side-dock').getAttribute('data-panel'), reads().length]).toEqual(['plan', 2]);
    await vi.waitFor(() => {
      expect(rows()[0]).toHaveTextContent('Action : Formulaire unique');
    });
    expect(rows()[0]).toHaveTextContent('Échéance : 31/12/2026');
  });
});

const USE_PATH = '/api/steps/s3/uses/o1';
/** The calls of the plan and of the use of « Facture » in the order they left: `GET` a reading of the plan, `PUT` a write of the use. */
const planAndUse = (sent: ReturnType<typeof fakeApi>) => sent.filter((call) => call.path === PLAN_PATH || call.path === USE_PATH).map((call) => call.method);

describe('the plan read again as it comes back on another map (relecture M2)', () => {
  it('covered by a sheet, the plan is read again on the map shown next, up a level, the sheet left open', async () => {
    const { editor, reads } = await planning([{ json: planOf() }, { json: planOf([LINES[1] as Line], 6) }], mapUnder(OTHER_MAP, 'Saisir la demande'), {
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } },
    });
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(2);
    });
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    expect(screen.queryByTestId('plan-panel')).toBeNull();
    cleanup();
    await showEditor(sampleMap());
    await vi.waitFor(() => {
      expect(codes()).toEqual(['VIS-I002']);
    });
    expect(reads()).toHaveLength(2);
  });

  it('the sheet asked on another map left for the map above with a field just written: read once the write is answered', async () => {
    const written = heldReply();
    const planned = { ...away, action: 'Formulaire unique' };
    const { sent, reads } = await planning([{ json: planOf([away], 0) }, { json: planOf([planned], 0) }], sampleMap(), { ...sheets, [`PUT ${USE_PATH}`]: written.reply });
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I002' }));
    cleanup();
    const detail = await showEditor(mapUnder(OTHER_MAP, 'Saisir la demande'));
    await sheetAsked(detail.editor);
    // The map shown reads nothing: the sheet asked covers the plan at once.
    expect(reads()).toHaveLength(1);
    const action = screen.getByLabelText('Action décidée sur « Facture »');
    fireEvent.change(action, { target: { value: 'Formulaire unique' } });
    // The caret leaves the field for the way up (the breadcrumb), which sends the use; the map above is shown before its reply.
    fireEvent.blur(action);
    cleanup();
    await showEditor(sampleMap());
    expect(await screen.findByTestId('plan-panel')).toBeInTheDocument();
    await settled();
    expect(planAndUse(sent)).toEqual(['GET', 'PUT']);
    await act(async () => {
      written.give(sheets['GET /api/steps/s3/sheet']);
      await Promise.resolve();
    });
    await vi.waitFor(() => {
      expect(rows()[0]).toHaveTextContent('Action : Formulaire unique');
    });
    expect(planAndUse(sent)).toEqual(['GET', 'PUT', 'GET']);
  });
});

describe('the plan read again after the last field written (relecture M3)', () => {
  it('the action typed then Escape, without Tab: the plan is read once the use is written, the new action shown', async () => {
    const written = heldReply();
    const planned = { ...here, action: 'Formulaire unique' };
    const { editor, sent } = await planning([{ json: planOf([here], 0) }, { json: planOf([planned], 0) }], sampleMap(), { ...sheets, [`PUT ${USE_PATH}`]: written.reply });
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I001' }));
    await sheetAsked(editor);
    const action = screen.getByLabelText('Action décidée sur « Facture »');
    act(() => {
      action.focus();
    });
    fireEvent.change(action, { target: { value: 'Formulaire unique' } });
    fireEvent.keyDown(action, { key: 'Escape' });
    expect(screen.getByTestId('side-dock').getAttribute('data-panel')).toBe('plan');
    await settled();
    // The use left as the field was left; the plan waits for its reply.
    expect(planAndUse(sent)).toEqual(['GET', 'PUT']);
    await act(async () => {
      written.give(sheets['GET /api/steps/s3/sheet']);
      await Promise.resolve();
    });
    await vi.waitFor(() => {
      expect(rows()[0]).toHaveTextContent('Action : Formulaire unique');
    });
    expect(planAndUse(sent)).toEqual(['GET', 'PUT', 'GET']);
  });
});

describe('Edit the plan, the step that receives on another map than the pain point (relecture M1)', () => {
  // The step that produces moved to a detailed map, the one that receives (where the plan is written) left on the root map.
  const split = lineOf('VIS-I004', { map_id: OTHER_MAP, map: named('Saisir la demande'), receiving_map_id: MAP_ID, document_id: 'o1', can_fill: true });

  it('shown on the map of the step that receives, its sheet opens there, no other map opened', async () => {
    const { editor, opened } = await planning({ json: planOf([split], 0) }, sampleMap(), sheets);
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I004' }));
    await sheetAsked(editor);
    expect(opened).toEqual([]);
  });

  it('shown on the map of the pain point, the map of the step that receives opens, then its sheet', async () => {
    const { opened } = await planning({ json: planOf([split], 0) }, mapUnder(OTHER_MAP, 'Saisir la demande'), sheets);
    fireEvent.click(await within(await screen.findByTestId('plan-panel')).findByRole('button', { name: 'Modifier le plan de VIS-I004' }));
    expect([opened, screen.queryByTestId('sheet-panel')]).toEqual([[`/maps/${MAP_ID}`], null]);
    cleanup();
    const root = await showEditor(sampleMap());
    await sheetAsked(root.editor);
  });

  it('the step that receives on no known map: no button', async () => {
    await planning({ json: planOf([{ ...split, receiving_map_id: null }], 0) });
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(1);
    });
    expect(edits()).toHaveLength(0);
  });
});
