// SearchPanel.test.tsx
// The panel « Rechercher » (ADR 110 point 5): the host asked after a pause and on Enter, a new text cancelling the search before it, the results by map with the word marked, Enter and Shift+Enter walking them on this map and on another one, Escape, the BPMN view left, one panel at a time.

import { act, cleanup, fireEvent, screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { nameIn } from '../../../shared/i18n/languageNames.ts';
import { chooseProcessLanguage, processLanguage } from '../../../shared/state/processLanguage.ts';
import { memoryStorage, renderMapScreen, showEditor, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi, heldReply, type FakeReply } from '../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { sheetViewOf } from '../../../test/sheets.ts';
import { bpmnView } from '../bpmn/bpmnState.ts';
import { createMapEditor } from '../state/editor.ts';
import { EMPTY_SEARCH, searchStore } from './searchStore.ts';
import { closeSearch } from './useSearch.ts';

type Hit = Schema<'SearchHitView'>;
type MapHits = Schema<'SearchMapView'>;

const OTHER_MAP = '0190a1b2-0000-7000-8000-00000000a002';
const OTHER_PROJECT = '0190a1b2-0000-7000-8000-00000000b002';
const SEARCH = `POST /api/projects/${PROJECT_ID}/search`;
const SEARCH_PATH = `/api/projects/${PROJECT_ID}/search`;

const named = (text: string): Schema<'NamedText'> => ({ text, source_language: 'fr', translations: {} });
const hit = (kind: Hit['kind'], text: string, start: number, length: number, extra: Partial<Hit> = {}): Hit => ({
  kind, step_id: null, lane_id: null, text, language: 'fr', start, length, marks: [{ start, length }], ...extra,
});
const mapHits = (mapId: string, depth: number, name: string, hits: Hit[]): MapHits => ({ map_id: mapId, depth, name: named(name), hits });
const replyOf = (...maps: MapHits[]): Schema<'ProjectSearchView'> => ({ total: maps.reduce((count, map) => count + map.hits.length, 0), maps });

/** « demande » on this map (a step, a document of a step) and on a detailed map below (a tool of its step s3). */
const found = replyOf(
  mapHits(MAP_ID, 1, 'Visites', [
    hit('step', 'Vérifier la demande', 12, 7, { step_id: 's2' }),
    hit('document', 'Bon de demande', 7, 7, { step_id: 's3', step_code: 'VIS-E003', step: named('Conforme ?') }),
  ]),
  mapHits(OTHER_MAP, 2, 'Saisir la demande', [hit('tool', 'Formulaire de demande', 14, 7, { step_id: 's3', step_code: 'VIS-E003', step: named('Remplir') })]),
);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    closeSearch();
    searchStore.setState({ ...EMPTY_SEARCH });
    bpmnView.setState({ open: false });
    processLanguage.setState({ chosen: {} });
  });
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const panel = () => screen.getByTestId('search-panel');
const field = () => screen.getByTestId<HTMLInputElement>('search-field');
const rows = () => within(panel()).queryAllByTestId(/^search-hit-/);
const wait = (milliseconds: number) => act(() => new Promise((resolve) => { setTimeout(resolve, milliseconds); }));

/** The map shown, the search opened by Ctrl+F on its canvas, the host answering as given. */
async function searching(reply: FakeReply | FakeReply[] = { json: found }, map = sampleMap(), routes: Record<string, FakeReply> = {}) {
  const sent = fakeApi({ [SEARCH]: reply, ...routes });
  const shown = await showEditor(map);
  fireEvent.keyDown(shown.canvas, { key: 'f', ctrlKey: true });
  return { ...shown, sent, searches: () => callsTo(sent, 'POST', SEARCH_PATH) };
}

const type = (text: string) => {
  fireEvent.change(field(), { target: { value: text } });
};

describe('the search sent to the host (ADR 110 point 4)', () => {
  it('two letters ask the host once after a pause, with the language shown and the map open', async () => {
    const { searches } = await searching();
    expect(document.activeElement).toBe(field());
    type('d');
    type('de');
    expect(searches()).toHaveLength(0);
    await vi.waitFor(() => {
      expect(searches().map((call) => call.body)).toEqual([{ text: 'de', language: 'fr', map_id: MAP_ID }]);
    });
    await wait(400);
    expect(searches()).toHaveLength(1);
  });

  it('one letter asks only on Enter', async () => {
    const { searches } = await searching();
    type('d');
    await wait(400);
    expect(searches()).toHaveLength(0);
    expect(panel()).toHaveTextContent('Au moins deux caractères, ou Entrée.');
    fireEvent.keyDown(field(), { key: 'Enter' });
    await vi.waitFor(() => {
      expect(searches().map((call) => call.body)).toEqual([{ text: 'd', language: 'fr', map_id: MAP_ID }]);
    });
  });

  it('a new text cancels the search before it', async () => {
    const first = heldReply();
    const { searches } = await searching([first.reply, { json: replyOf(mapHits(MAP_ID, 1, 'Visites', [hit('step', 'Demande reçue', 0, 7, { step_id: 's1' })])) }]);
    type('dem');
    await vi.waitFor(() => {
      expect(searches()).toHaveLength(1);
    });
    expect(panel()).toHaveTextContent('Recherche…');
    type('dema');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(1);
    });
    await act(async () => {
      first.give({ json: found });
      await Promise.resolve();
    });
    await wait(10);
    expect(rows().map((row) => row.textContent)).toEqual([expect.stringContaining('Demande reçue')]);
  });

  it('200 first of 1 234 is said; nothing found is said; a refusal of the host is said', async () => {
    const many = mapHits(MAP_ID, 1, 'Visites', Array.from({ length: 200 }, (_, index) => hit('step', `Étape ${String(index)}`, 0, 5, { step_id: 's2' })));
    await searching([
      { json: { total: 1234, maps: [many] } }, { json: { total: 0, maps: [] } },
      { refused: 400, code: 'VALEUR_INVALIDE', message: 'Le texte est trop long.' },
    ]);
    type('étape');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(200);
    });
    expect(panel().textContent).toMatch(/Les 200 premiers résultats sur 1\s234 : préciser la recherche\./);
    type('zzz');
    await vi.waitFor(() => {
      expect(panel()).toHaveTextContent('Aucun résultat.');
    });
    type('zzzz');
    await vi.waitFor(() => {
      expect(within(panel()).getByRole('alert')).toHaveTextContent('Le texte est trop long.');
    });
    expect(rows()).toHaveLength(0);
  });
});

describe('the results (ADR 110 point 5)', () => {
  it('the results are grouped by map, this map first, the word marked, a text of the language of origin says its language', async () => {
    chooseProcessLanguage(PROJECT_ID, 'en');
    const { searches } = await searching();
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    expect(searches().map((call) => call.body)).toEqual([{ text: 'demande', language: 'en', map_id: MAP_ID }]);
    const groups = within(panel()).getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);
    expect(groups).toEqual(['Cette carte', expect.stringContaining('Saisir la demande')]);
    expect(groups[1]).toContain('Niveau 2');
    expect([...panel().querySelectorAll('mark')].map((mark) => mark.textContent)).toEqual(['demande', 'demande', 'demande']);
    const [step, document, tool] = rows();
    expect(within(step as HTMLElement).getByRole('img', { name: 'Étape' })).toBeInTheDocument();
    expect(document).toHaveTextContent('VIS-E003 Conforme ?');
    expect(within(tool as HTMLElement).getByRole('img', { name: 'Outil' })).toBeInTheDocument();
    // Shown in English, a text found in French says so.
    expect(within(step as HTMLElement).getByTitle(`Langue d’origine : ${nameIn('fr', 'fr')}`)).toHaveTextContent('FR');
    // No result chosen yet: the count of results, not « 0 sur 3 ».
    expect(within(panel()).getByRole('status')).toHaveTextContent(/^3 résultats$/);
  });

  it('each word found is marked at the places the host gives, in the order of the text (R9b)', async () => {
    const marks = [{ start: 5, length: 3 }, { start: 9, length: 5 }];
    await searching({ json: replyOf(mapHits(MAP_ID, 1, 'Visites', [hit('step', 'Site web Livres à jour', 5, 3, { step_id: 's2', marks })])) });
    type('livre web');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(1);
    });
    expect([...panel().querySelectorAll('mark')].map((mark) => mark.textContent)).toEqual(['web', 'Livre']);
    expect(rows()[0]).toHaveTextContent('Site web Livres à jour');
  });

  it('Enter on this map chooses the step and puts it in view', async () => {
    const { editor } = await searching();
    const reveal = vi.spyOn(editor.canvas, 'reveal');
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    fireEvent.keyDown(field(), { key: 'Enter' });
    expect(editor.store.getState().selection.steps).toEqual(['s2']);
    expect(reveal).toHaveBeenLastCalledWith(['s2']);
    expect(rows()[0]).toHaveAttribute('aria-current', 'true');
    expect(within(panel()).getByRole('status')).toHaveTextContent('1 sur 3');
    expect(document.activeElement).toBe(field());
    fireEvent.click(rows()[1] as HTMLElement);
    expect(editor.store.getState().selection.steps).toEqual(['s3']);
    expect(rows()[1]).toHaveAttribute('aria-current', 'true');
    expect(rows()[0]).not.toHaveAttribute('aria-current');
  });

  it('Enter on another map opens it, which chooses the step on arrival, the search open and the field focused', async () => {
    const { opened, searches } = await searching();
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    fireEvent.keyDown(field(), { key: 'Enter' });
    fireEvent.keyDown(field(), { key: 'Enter' });
    fireEvent.keyDown(field(), { key: 'Enter' });
    expect(opened).toEqual([`/maps/${OTHER_MAP}`]);
    cleanup();
    const detail = await showEditor(sampleMap({ id: OTHER_MAP }));
    expect(detail.editor.store.getState().selection.steps).toEqual(['s3']);
    expect(await screen.findByTestId('search-panel')).toBeInTheDocument();
    expect(document.activeElement).toBe(field());
    expect(field().value).toBe('demande');
    expect(within(panel()).getByRole('status')).toHaveTextContent('3 sur 3');
    expect(rows()[2]).toHaveAttribute('aria-current', 'true');
    // Changing the map reads nothing again.
    expect(searches()).toHaveLength(1);
  });

});

describe('the keys of the field and the view in BPMN 2.0 (ADR 110 points 5 and 6)', () => {
  it('Shift+Enter goes back; after the last, the first', async () => {
    const { editor } = await searching({ json: replyOf(mapHits(MAP_ID, 1, 'Visites', ['s1', 's2', 's3'].map((id) => hit('step', `Demande ${id}`, 0, 7, { step_id: id })))) });
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    const chosen = () => editor.store.getState().selection.steps;
    fireEvent.keyDown(field(), { key: 'Enter', shiftKey: true });
    expect(chosen()).toEqual(['s3']);
    fireEvent.keyDown(field(), { key: 'Enter', shiftKey: true });
    expect(chosen()).toEqual(['s2']);
    fireEvent.keyDown(field(), { key: 'Enter' });
    expect(chosen()).toEqual(['s3']);
    fireEvent.keyDown(field(), { key: 'Enter' });
    expect(chosen()).toEqual(['s1']);
    fireEvent.click(within(panel()).getByTestId('search-previous'));
    expect(chosen()).toEqual(['s3']);
    fireEvent.click(within(panel()).getByTestId('search-next'));
    expect(chosen()).toEqual(['s1']);
  });

  it('Escape closes the search and gives the focus back to the map', async () => {
    const { canvas } = await searching();
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    fireEvent.keyDown(field(), { key: 'Enter' });
    fireEvent.keyDown(field(), { key: 'Escape' });
    expect(screen.queryByTestId('search-panel')).toBeNull();
    expect(document.activeElement).toBe(canvas.querySelector('.react-flow__node[data-id="s2"]'));
    // From a result, by the layer of the panels: the same.
    fireEvent.keyDown(canvas, { key: 'f', ctrlKey: true });
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    act(() => {
      (rows()[1] as HTMLElement).focus();
    });
    fireEvent.keyDown(rows()[1] as HTMLElement, { key: 'Escape' });
    expect(screen.queryByTestId('search-panel')).toBeNull();
    expect(canvas.contains(document.activeElement)).toBe(true);
  });

  it('in the BPMN view, a result goes back to the map and chooses the step', async () => {
    fakeApi({ [SEARCH]: { json: found }, [`GET /api/maps/${MAP_ID}/pmap`]: { refused: 500, code: 'ERREUR_INTERNE' } });
    act(() => {
      bpmnView.setState({ open: true });
    });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), navigate: vi.fn() });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('bpmn-view');
    fireEvent.keyDown(document.body, { key: 'f', ctrlKey: true });
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    fireEvent.keyDown(field(), { key: 'Enter' });
    expect(bpmnView.getState().open).toBe(false);
    expect(await screen.findByTestId('map-canvas')).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(editor.store.getState().selection.steps).toEqual(['s2']);
    });
    expect(screen.getByTestId('search-panel')).toBeInTheDocument();
  });
});

describe('one panel at a time (ADR 110 point 5)', () => {
  it('a sheet opened covers the search, which comes back when it closes; the assistant opened closes it', async () => {
    const { editor } = await searching({ json: found }, sampleMap(), {
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } },
    });
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'sheet');
    expect(screen.queryByTestId('search-panel')).toBeNull();
    act(() => {
      editor.store.setState({ openSheet: null });
    });
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'search');
    expect(field().value).toBe('demande');
    expect(rows()).toHaveLength(3);
    act(() => {
      editor.assistant.getState().setOpen(true);
    });
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'assistant');
    expect(searchStore.getState().open).toBe(false);
    // The search opened again closes the assistant and the sheet.
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    fireEvent.click(screen.getByTestId('ribbon-find'));
    expect([editor.store.getState().openSheet, editor.assistant.getState().open]).toEqual([null, false]);
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'search');
  });
});

describe('the count of the panel (ADR 110 point 5)', () => {
  it('says the number of results until one is chosen, then its place; a single result is singular', async () => {
    await searching({ json: replyOf(mapHits(MAP_ID, 1, 'Visites', [hit('step', 'Demande reçue', 0, 7, { step_id: 's1' })])) });
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(1);
    });
    expect(within(panel()).getByRole('status')).toHaveTextContent(/^1 résultat$/);
    fireEvent.keyDown(field(), { key: 'Enter' });
    expect(within(panel()).getByRole('status')).toHaveTextContent(/^1 sur 1$/);
  });
});

describe('another project (ADR 110 point 5)', () => {
  it('the search made in a project is not shown in another one', async () => {
    await searching();
    type('demande');
    await vi.waitFor(() => {
      expect(rows()).toHaveLength(3);
    });
    expect(searchStore.getState().projectId).toBe(PROJECT_ID);
    cleanup();
    const elsewhere = await showEditor(sampleMap({ id: OTHER_MAP, project_id: OTHER_PROJECT }));
    expect(elsewhere.canvas).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(searchStore.getState()).toMatchObject({ projectId: null, open: false, text: '', reply: null });
    });
    expect(screen.queryByTestId('search-panel')).toBeNull();
    // Opened again there, the field is empty and nothing of the first project is listed.
    fireEvent.keyDown(elsewhere.canvas, { key: 'f', ctrlKey: true });
    expect(await screen.findByTestId('search-panel')).toBeInTheDocument();
    expect(field().value).toBe('');
    expect(rows()).toHaveLength(0);
  });
});
