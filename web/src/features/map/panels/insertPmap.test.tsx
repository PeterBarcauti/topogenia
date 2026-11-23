// insertPmap.test.tsx
// A .pmap file inserted as a detailed step of the map, or replacing the detail of a step (3.3, F-208): the entries of the File menu, of the menu of the map and of the menu of a step, the preview read before anything is written, the place and the label sent with the version read, a refusal with the faults of the file.

import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, replyOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { chooseFileEntry } from '../../../test/fileMenu.ts';
import { createMapEditor } from '../state/editor.ts';
import type { MenuTarget } from '../state/floating.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const PREVIEW = `POST /api/maps/${MAP_ID}/insert-pmap/preview`;
const INSERT = `POST /api/maps/${MAP_ID}/insert-pmap`;

const preview = (overrides: Partial<Schema<'PmapInsertionPreview'>> = {}): Schema<'PmapInsertionPreview'> => ({
  project_code: 'ACHA', project_name: 'Achats', language: 'fr', maps: 2, lanes: 3, steps: 4, flows: 2, sheets: 2, depth: 2, room: 11,
  uncertainties: ['Le délai est à confirmer.'], remote_locations: 2, dropped_languages: ['eo'], label: 'Achats', structure_version: 7, ...overrides,
});

/** The map shown by an editor of a facilitator, a menu opened on it; gives the notifications and the editor. */
async function shown(target: MenuTarget, map: Schema<'MapView'> = sampleMap()) {
  const notes: [string, string | undefined][] = [];
  const editor = createMapEditor({ source: sourceOf(map), storage: memoryStorage(), notify: (text, kind) => notes.push([text, kind]) });
  renderMapScreen({ mapId: MAP_ID, editor });
  await screen.findByTestId('map-canvas');
  act(() => {
    editor.floating.getState().open({ kind: 'menu', target, at: { x: 10, y: 40 } });
  });
  return { notes, editor };
}

/** Pastes a text in the field of the dialog, at once (the host reads it a little after). */
async function paste(text: string) {
  const field = screen.getByTestId('insert-pmap-content');
  await userEvent.click(field);
  await userEvent.paste(text);
}

describe('the insertion of a file as a detailed step (F-208)', () => {
  it('shows what the file holds before anything is written, then sends the place, the label and the version read', async () => {
    const inserted = stepOf('s9', 'l1', 4, { label: 'Achats', sub_map_id: 'sub9' });
    const sent = fakeApi({ [PREVIEW]: { json: preview() }, [INSERT]: { json: replyOf(sampleMap({ steps: [...sampleMap().steps, inserted] }), { id: 's9', ids: ['sub9'] }) } });
    const { notes, editor } = await shown({ kind: 'file' });
    await chooseFileEntry('file.insert');
    const dialog = screen.getByRole('dialog', { name: 'Insérer un fichier comme étape détaillée' });
    expect(within(dialog).getByTestId('insert-pmap-confirm')).toBeDisabled();

    await paste('{"map":1}');
    const summary = await within(dialog).findByTestId('insert-pmap-preview', {}, { timeout: 2000 });
    expect(summary).toHaveTextContent('Achats (ACHA) · Étapes : 4 · Couloirs : 3 · Liens : 2 · Cartes : 2 · Fiches : 2');
    expect(summary).toHaveTextContent('Le délai est à confirmer.');
    expect(within(dialog).getByTestId('insert-pmap-dropped')).toHaveTextContent('Traductions écartées (langues non configurées ici) : eo');
    // A network path or a web address the file carries, said before a link is followed (pre-audit 68, P3-02).
    expect(within(dialog).getByTestId('insert-pmap-remote')).toHaveTextContent('Adresses réseau ou web dans les fiches : 2, à vérifier avant d’ouvrir un document.');
    expect(within(dialog).getByTestId('insert-pmap-label')).toHaveValue('Achats');
    // Nothing chosen on the map: before its end, on the one flow that reaches it, never out of the end (independent review
    // of the 3.3); an end is no step to follow; the lane follows the step chosen to follow.
    expect([within(dialog).getByTestId('insert-pmap-after'), within(dialog).getByTestId('insert-pmap-lane')].map((field) => (field as HTMLSelectElement).value)).toEqual(['flow:f3', 'l1']);
    expect([...within(dialog).getByTestId<HTMLSelectElement>('insert-pmap-after').options].map((option) => option.value)).toEqual(['flow:f3', '', 's1', 's2', 's3']);
    await userEvent.selectOptions(within(dialog).getByTestId('insert-pmap-after'), 's2');
    expect(within(dialog).getByTestId('insert-pmap-lane')).toHaveValue('l1');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)).toHaveLength(0);

    // In the flow after the step chosen, as « Insérer une étape après »: the host cuts its flow in two.
    await userEvent.click(within(dialog).getByTestId('insert-pmap-confirm'));
    const call = callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)[0];
    expect(call?.body).toEqual({ content: '{"map":1}', with_sheets: true, label: 'Achats', lane_id: 'l1', column: null, row: null, anchor_step_id: 's2', position: 'after' });
    expect(call?.headers.get('If-Match')).toBe('"7"');
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(editor.store.getState().selection.steps).toEqual(['s9']);
    expect(notes).toContainEqual(['Fichier inséré. Étapes : 4 · Cartes : 2', 'success']);
  });

  it('a file too deep for the map, and a file refused, cannot be inserted; the faults of the file are listed', async () => {
    fakeApi({
      [PREVIEW]: [
        { json: preview({ depth: 12, room: 11, dropped_languages: [], uncertainties: [], remote_locations: 0 }) },
        { refused: 400, code: 'FICHIER_INVALIDE', message: 'Le fichier comporte 1 erreur.', details: { errors: [{ path: 'map.lanes', code: 'PMAP_REQUIS', message: 'Champ obligatoire.' }] } },
      ],
    });
    await shown({ kind: 'file' });
    await chooseFileEntry('file.insert');
    const dialog = screen.getByRole('dialog');
    await paste('{"a":1}');
    expect(await within(dialog).findByTestId('insert-pmap-too-deep', {}, { timeout: 2000 })).toHaveTextContent('Ce fichier a 12 niveaux de cartes ; sous cette carte, il en faut 11 au plus.');
    expect(within(dialog).getByTestId('insert-pmap-confirm')).toBeDisabled();

    await paste(' ');
    const error = await within(dialog).findByTestId('insert-pmap-error', {}, { timeout: 2000 });
    expect(error).toHaveTextContent('map.lanes Champ obligatoire.');
    expect(within(dialog).queryByTestId('insert-pmap-preview')).toBeNull();
  });
});

describe('the file chosen for an insertion (audit of the copy 64, P2-06)', () => {
  it('a file too large is refused before it is read, a package of exchange before it is sent: nothing goes to the host', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: preview() } });
    await shown({ kind: 'file' });
    await chooseFileEntry('file.insert');
    const dialog = screen.getByRole('dialog');
    const large = new File(['{}'], 'grand.pmap');
    Object.defineProperty(large, 'size', { value: 10 * 1024 * 1024 + 1 });
    const text = vi.spyOn(large, 'text');
    await userEvent.upload(within(dialog).getByTestId('insert-pmap-file'), large);
    expect(await within(dialog).findByTestId('insert-pmap-error')).toHaveTextContent('Ce fichier dépasse 10 Mo : il ne peut pas être inséré.');
    expect(text).not.toHaveBeenCalled();

    await userEvent.upload(within(dialog).getByTestId('insert-pmap-file'), new File(['PK\u0003\u0004content.json'], 'projet.pmap'));
    expect(await within(dialog).findByText('C’est un paquet d’échange : il s’importe comme un projet, il ne s’insère pas.')).toBeInTheDocument();
    await new Promise((resolve) => { setTimeout(resolve, 600); });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap/preview`)).toHaveLength(0);
    expect(within(dialog).getByTestId('insert-pmap-content')).toHaveValue('');
  });
});

describe('the other entries of the insertion (F-208)', () => {
  it('the menu of the map inserts at the cell clicked, without asking the place', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: preview() }, [INSERT]: { json: replyOf(sampleMap()) } });
    // The point of the menu tests of « Coller ici »: a cell of the first lane.
    await shown({ kind: 'pane', point: { x: 500, y: 60 } });
    await userEvent.click(screen.getByTestId('menu-pane-pane.insert-pmap'));
    const dialog = screen.getByRole('dialog', { name: 'Insérer un fichier comme étape détaillée' });
    await paste('{"c":3}');
    await within(dialog).findByTestId('insert-pmap-preview', {}, { timeout: 2000 });
    expect([within(dialog).queryByTestId('insert-pmap-lane'), within(dialog).queryByTestId('insert-pmap-after')]).toEqual([null, null]);
    await userEvent.click(within(dialog).getByTestId('insert-pmap-confirm'));
    const body = callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)[0]?.body as { lane_id: string; column: number; row: number; anchor_step_id?: string };
    expect(body.lane_id).toBe('l1');
    // A cell names no neighbour: the step is linked to nothing, as a shape dropped on an empty place.
    expect([Number.isInteger(body.column), body.row, body.anchor_step_id]).toEqual([true, 0, undefined]);
  });

  it('« Au début de la carte » goes after its start, never into it', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: preview() }, [INSERT]: { json: replyOf(sampleMap()) } });
    await shown({ kind: 'file' });
    await chooseFileEntry('file.insert');
    const dialog = screen.getByRole('dialog');
    await paste('{"d":4}');
    await within(dialog).findByTestId('insert-pmap-preview', {}, { timeout: 2000 });
    await userEvent.selectOptions(within(dialog).getByTestId('insert-pmap-after'), '');
    await userEvent.click(within(dialog).getByTestId('insert-pmap-confirm'));
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)[0]?.body).toMatchObject({ lane_id: 'l1', anchor_step_id: 's1', position: 'after' });
  });
});

describe('the insertion on a flow, as a shape dropped on a connector (F-208)', () => {
  it('a flow chosen alone on the map: the step goes on it, and that flow only is cut, even an exit of a decision', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: preview() }, [INSERT]: { json: replyOf(sampleMap(), { id: 's9', ids: ['sub9'] }) } });
    const { editor } = await shown({ kind: 'file' });
    act(() => {
      editor.store.setState({ selection: { steps: [], flows: ['f4'] } });
    });
    await chooseFileEntry('file.insert');
    const dialog = screen.getByRole('dialog');
    await paste('{"e":5}');
    await within(dialog).findByTestId('insert-pmap-preview', {}, { timeout: 2000 });
    const after = within(dialog).getByTestId('insert-pmap-after');
    // The exit « non » of the decision, not the last step of the map; the lane of its origin.
    expect(after.querySelector('option:checked')).toHaveTextContent('Sur la flèche Conforme ? → Vérifier la demande');
    expect(within(dialog).getByTestId('insert-pmap-lane')).toHaveValue('l1');
    await userEvent.click(within(dialog).getByTestId('insert-pmap-confirm'));
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)[0]?.body).toEqual({ content: '{"e":5}', with_sheets: true, label: 'Achats',
      lane_id: 'l1', column: null, row: null, flow_id: 'f4' });
  });

  it('the menu of a flow inserts on it; another place chosen in the list leaves the flow', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: preview() }, [INSERT]: { json: replyOf(sampleMap()) } });
    await shown({ kind: 'flow', flowId: 'f3', at: { x: 1, y: 2 } });
    await userEvent.click(screen.getByTestId('menu-flow-flow.insert-pmap'));
    const dialog = screen.getByRole('dialog', { name: 'Insérer un fichier comme étape détaillée' });
    await paste('{"f":6}');
    await within(dialog).findByTestId('insert-pmap-preview', {}, { timeout: 2000 });
    const after = within(dialog).getByTestId('insert-pmap-after');
    expect(after.querySelector('option:checked')).toHaveTextContent('Sur la flèche Conforme ? → Facture émise');
    await userEvent.selectOptions(after, 's1');
    await userEvent.click(within(dialog).getByTestId('insert-pmap-confirm'));
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)[0]?.body).toMatchObject({ lane_id: 'l1', anchor_step_id: 's1', position: 'after' });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap`)[0]?.body).not.toHaveProperty('flow_id');
  });
});

describe('the replacement of a detail and the rights (F-208)', () => {
  it('« Remplacer le détail par un fichier » names the step and sends the file alone to the step', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: preview({ steps: 1, maps: 1 }) }, [`POST /api/steps/s2/replace-pmap`]: { json: replyOf(sampleMap(), { id: 's2' }) } });
    const detailed = sampleMap({ steps: sampleMap().steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub2' } : step)) });
    await shown({ kind: 'step', stepId: 's2' }, detailed);
    await userEvent.click(screen.getByTestId('menu-step-step.replace-detail'));
    const dialog = screen.getByRole('dialog', { name: 'Remplacer le détail par un fichier' });
    expect(dialog).toHaveTextContent('Le détail de « Vérifier la demande » part à la corbeille et le fichier prend sa place. Ctrl+Z annule le tout.');
    await paste('{"b":2}');
    await within(dialog).findByTestId('insert-pmap-preview', {}, { timeout: 2000 });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/insert-pmap/preview`)[0]?.body).toEqual({ content: '{"b":2}', step_id: 's2' });
    expect(within(dialog).queryByTestId('insert-pmap-label')).toBeNull();
    await userEvent.click(within(dialog).getByTestId('insert-pmap-sheets'));
    await userEvent.click(within(dialog).getByTestId('insert-pmap-confirm'));
    expect(callsTo(sent, 'POST', '/api/steps/s2/replace-pmap')[0]?.body).toEqual({ content: '{"b":2}', with_sheets: false });
  });

  it('a reader is offered neither the insertion nor the replacement', async () => {
    const readerMap = sampleMap({ rights: { structure: false, role: 'reader' } });
    await shown({ kind: 'file' }, readerMap);
    // The group of the project is open, so that the absence is the entry's own: the copy is there, the insertion is not.
    await userEvent.click(screen.getByTestId('menu-file-cascade-project'));
    expect(screen.getByTestId('menu-file-file.download')).toBeInTheDocument();
    expect(screen.queryByTestId('menu-file-file.insert')).toBeNull();
  });
});
