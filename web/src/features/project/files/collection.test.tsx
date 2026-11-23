// collection.test.tsx
// The collection of templates (.pmaps) opened as projects (3.4, lot 6, ADR 114): detected when chosen, dropped or pasted, its processes listed and ticked after a read-only preview, one project created per ticked line, one by one, a refused line not stopping the others.

import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../../../shared/keyboard/KeyboardContext.tsx';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi, heldReply } from '../../../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';
import { chooseFileEntry } from '../../../test/fileMenu.ts';
import { PMAP_FORMAT } from '../../../shared/pmap.ts';
import { createMapEditor } from '../../map/state/editor.ts';
import { CollectionDialog } from './CollectionDialog.tsx';
import { isCollection } from './collection.ts';
import { OpenPmapDialog } from './OpenPmapDialog.tsx';

const PREVIEW = 'POST /api/projects/import-collection/preview';
const IMPORT = 'POST /api/projects/import-pmap';

const ENVELOPE = `{ "format": "${PMAP_FORMAT}-collection", "version": "1.0", "name": "Achats", "processes": [] }`;
const WITH_COMMENTS = `// a note of the author\n{\n  "format": "${PMAP_FORMAT}-collection", // the envelope\n  "version": "1.0",\n  "name": "Achats",\n  "processes": [ {},\n  ],\n}`;
const PMAP = JSON.stringify({ format: PMAP_FORMAT, version: '1.5', project: { code: 'VIS', name: 'Visites' }, maps: [] });

type Process = Schema<'CollectionProcessPreview'>;

const process = (index: number, overrides: Partial<Process> = {}): Process => ({
  index, name: `Processus ${index}`, language: 'fr', file_code: 'ACH', proposed_code: `ACH${'ABCDEF'[index] ?? 'Z'}`, description: `Description ${index}`,
  counts: { maps: 2, lanes: 3, steps: 9, flows: 8, sheets: 1 }, uncertainties: ['Le délai est à confirmer.'], remote_locations: 0, faults: [], content: `{"process":${index}}`, ...overrides,
});

const collection = (processes: Process[], name = 'Achats'): Schema<'CollectionPreview'> => ({ name, description: null, processes });

const created = (code: string, root = `m-${code}`) => ({ json: { id: `p-${code}`, code, root_map_id: root, maps: 2, lanes: 3, steps: 9, flows: 8, sheets: 1, uncertainties: [], remote_locations: 0 }, status: 201 });

const openCollection = (onClose = vi.fn()) => {
  render(<KeyboardProvider><CollectionDialog content={ENVELOPE} onClose={onClose} /></KeyboardProvider>);
  return onClose;
};

const rowBox = (name: string) => screen.getByRole('checkbox', { name });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the list of a collection (ADR 114)', () => {
  it('detects_a_collection_chosen_dropped_or_pasted_even_with_comments', async () => {
    expect([isCollection(ENVELOPE), isCollection(WITH_COMMENTS), isCollection(`\u{feff}\`\`\`json\n${ENVELOPE}\n\`\`\``), isCollection(PMAP), isCollection('not json'), isCollection('')])
      .toEqual([true, true, true, false, false, false]);
    fakeApi({ [PREVIEW]: { json: collection([process(0)], 'Achats') } });
    const field = () => screen.getByRole('textbox', { name: 'Ou coller son contenu ici, par exemple la réponse d’une IA :' });

    // Pasted, with comments and trailing commas: the preview opens in the place of the dialog of a file.
    const pasted = render(<KeyboardProvider><OpenPmapDialog onClose={vi.fn()} /></KeyboardProvider>);
    fireEvent.change(field(), { target: { value: WITH_COMMENTS } });
    expect(await screen.findByRole('dialog', { name: 'Collection « Achats »' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Ouvrir un fichier' })).toBeNull();
    pasted.unmount();

    // Chosen: the extension is offered by the file dialog.
    const chosen = render(<KeyboardProvider><OpenPmapDialog onClose={vi.fn()} /></KeyboardProvider>);
    expect(screen.getByTestId('open-pmap-file')).toHaveAttribute('accept', expect.stringContaining('.pmaps'));
    await userEvent.upload(screen.getByTestId('open-pmap-file'), new File([ENVELOPE], 'achats.fr.pmaps'));
    expect(await screen.findByRole('dialog', { name: 'Collection « Achats »' })).toBeInTheDocument();
    chosen.unmount();

    // Dropped on the area.
    render(<KeyboardProvider><OpenPmapDialog onClose={vi.fn()} /></KeyboardProvider>);
    fireEvent.drop(field(), { dataTransfer: { files: [new File([ENVELOPE], 'glisse.pmaps')] } });
    expect(await screen.findByRole('dialog', { name: 'Collection « Achats »' })).toBeInTheDocument();
  });

  it('lists_every_process_ticked_with_its_counts_code_and_points_to_check', async () => {
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception', proposed_code: 'RECEP', uncertainties: [] })]) } });
    openCollection();
    const dialog = await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    expect(within(dialog).getByText('Un projet est créé pour chaque processus coché. Rien n’est écrit avant « Créer les projets ».')).toBeInTheDocument();
    expect([rowBox('Achats'), rowBox('Réception')].map((box) => (box as HTMLInputElement).checked)).toEqual([true, true]);
    expect(within(dialog).getByText('Cochés : 2 sur 2')).toBeInTheDocument();
    const first = rowBox('Achats').closest('li');
    expect(first).toHaveTextContent('Étapes : 9 · Couloirs : 3 · Liens : 8 · Cartes : 2');
    expect(first).toHaveTextContent('Points à vérifier : 1');
    expect(first).toHaveTextContent('Description 0');
    expect(first).toHaveTextContent('Le délai est à confirmer.');
    expect(within(dialog).getByRole('textbox', { name: 'Code court (Achats)' })).toHaveValue('ACHA');
    expect(within(dialog).getByRole('textbox', { name: 'Code court (Réception)' })).toHaveValue('RECEP');
    expect(within(dialog).getByRole('button', { name: 'Créer les projets' })).toBeEnabled();
  });

  it('an_invalid_process_is_unticked_greyed_and_shows_its_faults', async () => {
    const faulty = process(1, { name: 'Réception', proposed_code: null, counts: null, language: null, uncertainties: [], faults: [{ path: 'maps[0].steps[2]', code: 'PMAP_OBLIGATOIRE', message: 'Champ obligatoire.' }] });
    const unnamed = process(2, { name: null, proposed_code: null, counts: null, language: null, uncertainties: [], faults: [{ path: '', code: 'PMAP_JSON', message: 'JSON illisible.' }] });
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), faulty, unnamed]) } });
    openCollection();
    const dialog = await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    expect(rowBox('Réception')).not.toBeChecked();
    expect(rowBox('Réception')).toBeDisabled();
    expect(rowBox('Processus 3')).toBeDisabled();
    expect(rowBox('Achats')).toBeChecked();
    const row = rowBox('Réception').closest('li');
    expect(row).toHaveClass('muted');
    expect(row).toHaveTextContent('Ne peut pas être ouvert :');
    expect(row).toHaveTextContent('maps[0].steps[2] Champ obligatoire.');
    expect(within(row as HTMLElement).queryByRole('textbox')).toBeNull();
    expect(within(dialog).getByText('Cochés : 1 sur 1')).toBeInTheDocument();
  });

  it('sends_nothing_before_create', async () => {
    const sent = fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' })]) }, [IMPORT]: created('ACHA') });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(rowBox('Réception'));
    await userEvent.click(rowBox('Réception'));
    await userEvent.type(screen.getByRole('textbox', { name: 'Code court (Achats)' }), 'X');
    await new Promise((resolve) => { setTimeout(resolve, 100); });
    // Only the read-only preview went; nothing is written, and nothing without a line ticked either.
    expect(sent.map((call) => `${call.method} ${call.path}`)).toEqual([PREVIEW]);
    await userEvent.click(rowBox('Achats'));
    await userEvent.click(rowBox('Réception'));
    expect(screen.getByRole('button', { name: 'Créer les projets' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')).toHaveLength(0);
  });

});

describe('the addresses that reach another machine (pre-audit 68, P3-02)', () => {
  it('a_process_whose_documents_reach_another_machine_says_how_many_before_anything_is_created', async () => {
    // A network path or a web address in a file taken from the site, said before a link is followed (pre-audit 68, P3-02).
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats', remote_locations: 3 }), process(1, { name: 'Réception' })]) } });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    expect(rowBox('Achats').closest('li')).toHaveTextContent('Adresses réseau ou web dans les fiches : 3, à vérifier avant d’ouvrir un document.');
    expect(rowBox('Réception').closest('li')).not.toHaveTextContent('Adresses réseau ou web dans les fiches :');
  });
});

describe('the creation of the projects (ADR 114)', () => {
  it('creates_one_project_per_ticked_line_in_order_and_never_an_unticked_one', async () => {
    const first = heldReply();
    const sent = fakeApi({
      [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' }), process(2, { name: 'Paiement' })]) },
      [IMPORT]: [first.reply, created('PAIE')],
    });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(rowBox('Réception'));
    const code = screen.getByRole('textbox', { name: 'Code court (Achats)' });
    await userEvent.clear(code);
    await userEvent.type(code, 'ach');
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    // One by one: the second is not sent before the first is answered.
    expect(await screen.findByText('Création 1 sur 2…')).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')).toHaveLength(1);
    await act(async () => { first.give(created('ACH')); await Promise.resolve(); });
    expect(await screen.findByRole('dialog', { name: 'Projets créés' })).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap').map((call) => call.body)).toEqual([
      { content: '{"process":0}', code: 'ACH', name: 'Achats' },
      { content: '{"process":2}', code: 'ACHC', name: 'Paiement' },
    ]);
    expect(screen.getByText('ACH créé.')).toBeInTheDocument();
    expect(screen.getByText('PAIE créé.')).toBeInTheDocument();
    expect(screen.queryByText('Réception')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir la carte : Paiement' }));
    expect(window.location.pathname).toBe('/maps/m-PAIE');
  });

  it('a_refused_line_does_not_stop_the_others_and_can_be_tried_again_with_another_code', async () => {
    const sent = fakeApi({
      [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' }), process(2, { name: 'Paiement' })]) },
      [IMPORT]: [created('ACHA'), { refused: 409, code: 'PROJET_CODE_EXISTANT', message: 'Un projet a déjà ce code.' }, created('PAIE'), created('RECEB')],
    });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    await screen.findByRole('dialog', { name: 'Projets créés' });
    // The three were tried, in order: the second is refused and told, the third is created.
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap').map((call) => (call.body as { code: string }).code)).toEqual(['ACHA', 'ACHB', 'ACHC']);
    expect(screen.getByText('ACHA créé.')).toBeInTheDocument();
    expect(screen.getByText('PAIE créé.')).toBeInTheDocument();
    expect(screen.getByText('Non créé : ACHB est déjà pris.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: new RegExp('^Ouvrir la carte') })).toHaveLength(2);
    // Tried again with another code: only that line goes.
    const code = screen.getByRole('textbox', { name: 'Code court (Réception)' });
    await userEvent.clear(code);
    await userEvent.type(code, 'receb');
    await userEvent.click(screen.getByRole('button', { name: new RegExp('^Réessayer') }));
    expect(await screen.findByText('RECEB créé.')).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')).toHaveLength(4);
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')[3]?.body).toEqual({ content: '{"process":1}', code: 'RECEB', name: 'Réception' });
    expect(screen.queryByRole('button', { name: new RegExp('^Réessayer') })).toBeNull();
  });

});

describe('the refusals of a line (ADR 114)', () => {
  it('a_refusal_for_a_taken_code_names_the_code_refused_not_the_one_being_typed', async () => {
    fakeApi({
      [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' })]) },
      [IMPORT]: [created('ACHA'), { refused: 409, code: 'PROJET_CODE_EXISTANT', message: 'Un projet a déjà ce code.' }],
    });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    expect(await screen.findByText('Non créé : ACHB est déjà pris.')).toBeInTheDocument();
    // Typing another code changes the field, not the reason of the refusal, until it is tried again.
    const code = screen.getByRole('textbox', { name: 'Code court (Réception)' });
    await userEvent.clear(code);
    await userEvent.type(code, 'recep');
    expect(code).toHaveValue('RECEP');
    expect(screen.getByText('Non créé : ACHB est déjà pris.')).toBeInTheDocument();
    expect(screen.queryByText(new RegExp('RECEP est déjà pris'))).toBeNull();
  });

  it('the_title_says_no_project_was_created_when_every_line_is_refused', async () => {
    fakeApi({
      [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' })]) },
      [IMPORT]: [
        { refused: 409, code: 'PROJET_CODE_EXISTANT', message: 'Un projet a déjà ce code.' },
        { refused: 409, code: 'PROJET_CODE_CORBEILLE', message: 'Un projet de la corbeille a ce code.' },
        created('RECEP'),
      ],
    });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    expect(await screen.findByRole('dialog', { name: 'Aucun projet créé' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Projets créés' })).toBeNull();
    // One line tried again and created: the title says it.
    const code = screen.getByRole('textbox', { name: 'Code court (Réception)' });
    await userEvent.clear(code);
    await userEvent.type(code, 'recep');
    await userEvent.click(screen.getByRole('button', { name: 'Réessayer : Réception' }));
    expect(await screen.findByRole('dialog', { name: 'Projets créés' })).toBeInTheDocument();
  });

  it('two_lines_with_the_same_code_are_told_before_anything_is_sent_for_the_second', async () => {
    const sent = fakeApi({
      [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' }), process(2, { name: 'Paiement' })]) },
      [IMPORT]: [created('ACHA'), created('ACHC'), created('RECEP')],
    });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    const code = screen.getByRole('textbox', { name: 'Code court (Réception)' });
    await userEvent.clear(code);
    await userEvent.type(code, 'acha');
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    await screen.findByRole('dialog', { name: 'Projets créés' });
    // The second line with the code of the first is refused at its place; nothing is sent for it.
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap').map((call) => (call.body as { code: string }).code)).toEqual(['ACHA', 'ACHC']);
    expect(within(screen.getByTestId('collection-result-1')).getByRole('alert')).toHaveTextContent('Non créé : ACHA est aussi donné à une autre ligne.');
    // Tried again with the code of a line already created: refused the same way, nothing sent.
    const again = screen.getByRole('textbox', { name: 'Code court (Réception)' });
    await userEvent.clear(again);
    await userEvent.type(again, 'achc');
    await userEvent.click(screen.getByRole('button', { name: 'Réessayer : Réception' }));
    expect(await screen.findByText('Non créé : ACHC est aussi donné à une autre ligne.')).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')).toHaveLength(2);
    // A free code: the line goes.
    await userEvent.clear(again);
    await userEvent.type(again, 'recep');
    await userEvent.click(screen.getByRole('button', { name: 'Réessayer : Réception' }));
    expect(await screen.findByText('RECEP créé.')).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap').map((call) => (call.body as { code: string }).code)).toEqual(['ACHA', 'ACHC', 'RECEP']);
  });

});

describe('the lines chosen (ADR 114)', () => {
  it('the_header_box_ticks_and_unticks_all', async () => {
    const faulty = process(2, { name: 'Paiement', proposed_code: null, counts: null, uncertainties: [], faults: [{ path: '', code: 'PMAP_JSON', message: 'JSON illisible.' }] });
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' }), process(1, { name: 'Réception' }), faulty]) } });
    openCollection();
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    const header = screen.getByRole<HTMLInputElement>('checkbox', { name: 'Tout cocher' });
    expect([header.checked, header.indeterminate]).toEqual([true, false]);
    await userEvent.click(header);
    expect([rowBox('Achats'), rowBox('Réception'), header].map((box) => (box as HTMLInputElement).checked)).toEqual([false, false, false]);
    expect(screen.getByText('Cochés : 0 sur 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Créer les projets' })).toBeDisabled();
    await userEvent.click(rowBox('Achats'));
    expect([header.checked, header.indeterminate]).toEqual([false, true]);
    await userEvent.click(header);
    expect([rowBox('Achats'), rowBox('Réception'), rowBox('Paiement'), header].map((box) => (box as HTMLInputElement).checked)).toEqual([true, true, false, true]);
    expect(screen.getByText('Cochés : 2 sur 2')).toBeInTheDocument();
  });

});

describe('the files refused and the dialog (ADR 114)', () => {
  it('a_file_over_ten_megabytes_is_refused_before_it_is_read', async () => {
    const sent = fakeApi({});
    render(<KeyboardProvider><OpenPmapDialog onClose={vi.fn()} /></KeyboardProvider>);
    const large = new File([ENVELOPE], 'grand.pmaps');
    Object.defineProperty(large, 'size', { value: 10 * 1024 * 1024 + 1 });
    const text = vi.spyOn(large, 'text');
    await userEvent.upload(screen.getByTestId('open-pmap-file'), large);
    expect(await screen.findByTestId('open-pmap-error')).toHaveTextContent('Ce fichier dépasse 10 Mo : il ne peut pas être ouvert.');
    expect(text).not.toHaveBeenCalled();
    expect(sent).toHaveLength(0);
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier' })).toBeInTheDocument();
  });

  it('a_collection_is_refused_by_the_insertion_with_the_way_to_open_it', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/insert-pmap/preview`]: { refused: 400, code: 'FICHIER_INVALIDE' } });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), notify: vi.fn() });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('map-canvas');
    act(() => {
      editor.floating.getState().open({ kind: 'menu', target: { kind: 'file' }, at: { x: 10, y: 40 } });
    });
    await chooseFileEntry('file.insert');
    const dialog = screen.getByRole('dialog');
    await userEvent.upload(within(dialog).getByTestId('insert-pmap-file'), new File([ENVELOPE], 'achats.pmaps'));
    expect(await within(dialog).findByTestId('insert-pmap-error')).toHaveTextContent('Une collection s’ouvre par « Ouvrir un fichier… ». Insérer un de ses modèles seul.');
    await new Promise((resolve) => { setTimeout(resolve, 600); });
    expect(sent).toHaveLength(0);
    expect(within(dialog).getByTestId('insert-pmap-content')).toHaveValue('');
  });

  it('escape_closes_and_focus_returns', async () => {
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' })]) } });
    function Harness() {
      const [open, setOpen] = useState(false);
      return (
        <KeyboardProvider>
          <button type="button" onClick={() => { setOpen(true); }}>Open the collection</button>
          {open && <CollectionDialog content={ENVELOPE} onClose={() => { setOpen(false); }} />}
        </KeyboardProvider>
      );
    }
    render(<Harness />);
    const opener = screen.getByRole('button', { name: 'Open the collection' });
    await userEvent.click(opener);
    const dialog = await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    await userEvent.keyboard('{Escape}');
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull();
    });
    expect(opener).toHaveFocus();
  });

});

describe('the dialog after the creation, and an envelope refused (ADR 114)', () => {
  it('escape_closes_after_the_creation_and_focus_returns', async () => {
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' })]) }, [IMPORT]: created('ACHA') });
    function Harness() {
      const [open, setOpen] = useState(false);
      return (
        <KeyboardProvider>
          <button type="button" onClick={() => { setOpen(true); }}>Open the collection</button>
          {open && <CollectionDialog content={ENVELOPE} onClose={() => { setOpen(false); }} />}
        </KeyboardProvider>
      );
    }
    render(<Harness />);
    const opener = screen.getByRole('button', { name: 'Open the collection' });
    await userEvent.click(opener);
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    const dialog = await screen.findByRole('dialog', { name: 'Projets créés' });
    // The button pressed is gone with the creation: the focus stays in the dialog, on its way out.
    expect(dialog).toContainElement(document.activeElement as HTMLElement);
    await userEvent.keyboard('{Escape}');
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull();
    });
    expect(opener).toHaveFocus();
  });

  it('the_close_button_of_the_foot_and_the_cross_of_the_header_have_each_their_identifier', async () => {
    fakeApi({ [PREVIEW]: { json: collection([process(0, { name: 'Achats' })]) }, [IMPORT]: created('ACHA') });
    render(<KeyboardProvider><CollectionDialog content={ENVELOPE} onClose={() => undefined} /></KeyboardProvider>);
    await screen.findByRole('dialog', { name: 'Collection « Achats »' });
    await userEvent.click(screen.getByRole('button', { name: 'Créer les projets' }));
    const dialog = await screen.findByRole('dialog', { name: 'Projets créés' });
    expect(within(dialog).getAllByTestId('collection-close')).toHaveLength(1);
    expect(within(dialog).getByTestId('collection-close').closest('.dialog-foot')).toBeNull();
    expect(within(dialog).getByTestId('collection-done').closest('.dialog-foot')).not.toBeNull();
  });

  it('a_pasted_collection_with_a_faulty_envelope_is_corrected_in_place', async () => {
    const faulty = `{ "format": "${PMAP_FORMAT}-collection", "version": "1.0", "processes": [] }`;
    const sent = fakeApi({
      [PREVIEW]: [
        { refused: 400, code: 'FICHIER_INVALIDE', message: 'Le fichier n’est pas valide.', details: { errors: [{ path: 'name', code: 'PMAP_OBLIGATOIRE', message: 'Champ obligatoire.' }] } },
        { json: collection([process(0, { name: 'Achats' })]) },
      ],
    });
    render(<KeyboardProvider><OpenPmapDialog onClose={vi.fn()} /></KeyboardProvider>);
    const field = () => screen.getByRole('textbox', { name: 'Ou coller son contenu ici, par exemple la réponse d’une IA :' });
    fireEvent.change(field(), { target: { value: faulty } });
    expect(await screen.findByRole('alert')).toHaveTextContent('name Champ obligatoire.');
    // Back to the text, kept as pasted, its faults shown: it is corrected there, with no new preview while it is typed.
    await userEvent.click(screen.getByRole('button', { name: 'Corriger le texte' }));
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier' })).toBeInTheDocument();
    expect(field()).toHaveValue(faulty);
    expect(screen.getByTestId('open-pmap-error')).toHaveTextContent('name Champ obligatoire.');
    fireEvent.change(field(), { target: { value: ENVELOPE } });
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier' })).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-collection/preview')).toHaveLength(1);
    // « Ouvrir » reads the text corrected as a collection.
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    expect(await screen.findByRole('dialog', { name: 'Collection « Achats »' })).toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/projects/import-collection/preview').map((call) => call.body)).toEqual([{ content: faulty }, { content: ENVELOPE }]);
  });

  it('a_refused_preview_says_why_and_creates_nothing', async () => {
    const sent = fakeApi({ [PREVIEW]: { refused: 400, code: 'FICHIER_INVALIDE', message: 'Le fichier n’est pas valide.', details: { errors: [{ path: 'processes', code: 'PMAP_OBLIGATOIRE', message: 'Champ obligatoire.' }] } } });
    openCollection();
    const error = await screen.findByRole('alert');
    expect(error).toHaveTextContent('Le fichier n’est pas valide.');
    expect(error).toHaveTextContent('processes Champ obligatoire.');
    expect(screen.getByRole('button', { name: 'Créer les projets' })).toBeDisabled();
    expect(sent).toHaveLength(1);
  });
});
