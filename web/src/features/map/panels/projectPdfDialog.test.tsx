// projectPdfDialog.test.tsx
// « Exporter le projet en PDF… » of the File menu (lot 5 of the 3.4, ADR 113): its entry for every role and in the BPMN view, the scope, the four boxes and their starting values, the warning on the sheets, the progress and Cancel, the abandon by Escape, the refusals said in the dialog.

import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { menuItemsOf } from '../../../shared/commands/commands.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../test/editor.tsx';
import { editorOf } from '../../../test/editorStore.ts';
import { fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';
import { chooseFileEntry } from '../../../test/fileMenu.ts';
import { contextOf, type MapContext } from '../state/commands/context.ts';
import { FILE_MENU, MAP_COMMANDS } from '../state/commands/mapCommands.ts';
import { createMapEditor, type MapEditor } from '../state/editor.ts';
import { exportProjectPdf, type ProjectPdfAsk, type ProjectPdfOutcome, type ProjectPdfRun } from '../state/actions/projectPdf.ts';

vi.mock('../state/actions/projectPdf.ts', () => ({ exportProjectPdf: vi.fn() }));

/** What a call of the task was given, and how to end it. */
interface Pending {
  readonly ask: ProjectPdfAsk;
  readonly run: ProjectPdfRun;
  readonly end: (outcome: ProjectPdfOutcome) => void;
}

let pending: Pending | null = null;

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  pending = null;
  vi.mocked(exportProjectPdf).mockReset();
  vi.mocked(exportProjectPdf).mockImplementation((_editor, ask, run) => new Promise<ProjectPdfOutcome>((resolve) => {
    pending = { ask, run, end: resolve };
  }));
  fakeApi({});
});

const detailed = () => sampleMap({
  breadcrumb: [{ map_id: 'root', name: 'Visites', source_language: 'fr', translations: {} }, { map_id: MAP_ID, name: 'Traiter', source_language: 'fr', translations: {} }],
});

/** The dialog opened from the File menu of the map given. */
async function projectDialog(map: Schema<'MapView'> = sampleMap(), setup: (editor: MapEditor) => void = () => undefined): Promise<{ editor: MapEditor; dialog: HTMLElement }> {
  const editor = createMapEditor({ source: sourceOf(map), storage: memoryStorage(), admin: true });
  setup(editor);
  renderMapScreen({ mapId: MAP_ID, editor });
  await screen.findByTestId('map-canvas');
  act(() => {
    editor.floating.getState().open({ kind: 'menu', target: { kind: 'file' }, at: { x: 10, y: 40 } });
  });
  await chooseFileEntry('file.project-pdf');
  return { editor, dialog: screen.getByRole('dialog', { name: 'Exporter le projet en PDF' }) };
}

const box = (dialog: HTMLElement, name: string) => within(dialog).getByRole('checkbox', { name });
const BOXES = ['Avec les fiches', 'Avec la matrice RACI', 'Avec le flux de valeur', 'Avec le plan d’actions'];
const isOff = (input: HTMLElement) => (input as HTMLInputElement).disabled;

describe('the entry of the File menu', () => {
  it('Export the project as PDF opens the group of the project in the File menu, for every role, in the BPMN view too', async () => {
    const roles = (['facilitator', 'contributor', 'reader'] as const).map((role) => sampleMap({ rights: { structure: role === 'facilitator', role } }));
    for (const map of roles) {
      for (const shown of [true, false]) {
        const { editor } = await editorOf(map);
        // The canvas is shown on the map, and not in the view in BPMN 2.0.
        if (shown) editor.canvas.show({ element: document.createElement('div'), bounds: null });
        const context = contextOf(editor, null) as MapContext;
        const items = menuItemsOf(MAP_COMMANDS, FILE_MENU, context);
        const ids = items.map((item) => item.id);
        // First of the group « Projet » (ADR 117), after the commands of the map.
        expect(ids.indexOf('file.project-pdf') - ids.indexOf('file.wall')).toBe(1);
        const entry = items.find((item) => item.id === 'file.project-pdf');
        expect([i18next.t(entry?.label ?? ''), entry?.disabled ?? false]).toEqual(['Exporter le projet en PDF…', false]);
      }
    }
  });

  it('opens the dialog, with its help', async () => {
    const { dialog } = await projectDialog();
    expect(within(dialog).getByText(new RegExp('Toutes les cartes dans un seul fichier'))).toBeInTheDocument();
  });
});

describe('the scope', () => {
  it('This map and its levels is offered on a detailed map only', async () => {
    const { dialog } = await projectDialog(sampleMap());
    expect(within(dialog).getByRole('radio', { name: 'Tout le projet' })).toBeChecked();
    expect(within(dialog).queryByRole('radio', { name: 'Cette carte et ses niveaux' })).toBeNull();
  });

  it('offers both on a detailed map, the whole project first, and asks the task with the one chosen', async () => {
    const { editor, dialog } = await projectDialog(detailed());
    expect(within(dialog).getByRole('radio', { name: 'Tout le projet' })).toBeChecked();
    await userEvent.click(within(dialog).getByRole('radio', { name: 'Cette carte et ses niveaux' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    expect(vi.mocked(exportProjectPdf).mock.calls[0]?.[0]).toBe(editor);
    expect(pending?.ask.scope).toBe('branch');
  });
});

describe('the four boxes', () => {
  it('start as the arbitration says: sheets and action plan ticked, RACI and value stream as the map shows them', async () => {
    const { dialog } = await projectDialog();
    expect(BOXES.map((name) => (box(dialog, name) as HTMLInputElement).checked)).toEqual([true, false, false, true]);
  });

  it.each([
    ['showRaci', [true, true, false, true]],
    ['showValueStream', [true, false, true, true]],
  ] as const)('follow the display: with the layer %s on, the RACI and the value stream are ticked as it shows them', async (layer, expected) => {
    const { dialog } = await projectDialog(sampleMap(), (editor) => {
      editor.preferences.getState().set(layer, true);
    });
    expect(BOXES.map((name) => (box(dialog, name) as HTMLInputElement).checked)).toEqual(expected);
  });

  it('keep no memory from one export to the next: reopened, the dialog starts again as the display says', async () => {
    const { editor, dialog } = await projectDialog();
    await userEvent.click(box(dialog, 'Avec les fiches'));
    await userEvent.click(box(dialog, 'Avec le plan d’actions'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Annuler' }));
    expect(screen.queryByRole('dialog', { name: 'Exporter le projet en PDF' })).toBeNull();
    act(() => {
      editor.floating.getState().open({ kind: 'project-pdf' });
    });
    const again = screen.getByRole('dialog', { name: 'Exporter le projet en PDF' });
    expect(BOXES.map((name) => (box(again, name) as HTMLInputElement).checked)).toEqual([true, false, false, true]);
  });

  it('are given to the task as they are ticked', async () => {
    const { dialog } = await projectDialog();
    await userEvent.click(box(dialog, 'Avec les fiches'));
    await userEvent.click(box(dialog, 'Avec la matrice RACI'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    expect(pending?.ask).toEqual({ scope: 'project', choice: { sheets: false, raci: true, stream: false, plan: true } });
  });

  it('show the warning on the sheets under their box, tied to it for a screen reader', async () => {
    const { dialog } = await projectDialog();
    const text = 'Les fiches sont celles que vous voyez : n’envoyez le document qu’à qui peut les lire.';
    const warning = within(dialog).getByText(text);
    expect(box(dialog, 'Avec les fiches')).toHaveAccessibleDescription(text);
    expect(box(dialog, 'Avec les fiches').closest('label')?.nextElementSibling).toBe(warning);
  });
});

describe('the task', () => {
  it('Create the PDF greys the boxes and shows the progress and Cancel, then closes when the file is made', async () => {
    const { dialog } = await projectDialog(detailed());
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    expect(within(dialog).queryByRole('button', { name: 'Créer le PDF' })).toBeNull();
    expect(within(dialog).getAllByRole('checkbox').every(isOff)).toBe(true);
    expect(within(dialog).getAllByRole('radio').every(isOff)).toBe(true);
    expect(within(dialog).getByRole('progressbar', { name: 'Avancement' })).not.toHaveAttribute('value');
    act(() => {
      pending?.run.progress({ text: 'Carte 3 sur 12', value: 3, max: 12 });
    });
    const bar = within(dialog).getByRole('progressbar', { name: 'Avancement' });
    expect([bar.getAttribute('value'), bar.getAttribute('max')]).toEqual(['3', '12']);
    expect(within(dialog).getByText('Carte 3 sur 12')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Annuler' })).toBeEnabled();
    await act(async () => {
      pending?.end({ kind: 'done' });
      await Promise.resolve();
    });
    expect(screen.queryByRole('dialog', { name: 'Exporter le projet en PDF' })).toBeNull();
  });

  it('Cancel stops the task by its signal and closes', async () => {
    const { editor, dialog } = await projectDialog();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    expect(pending?.run.signal.aborted).toBe(false);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Annuler' }));
    expect(pending?.run.signal.aborted).toBe(true);
    expect(screen.queryByRole('dialog', { name: 'Exporter le projet en PDF' })).toBeNull();
    expect(editor.floating.getState().floating).toBeNull();
  });

  it('Escape cancels the task then closes', async () => {
    const { dialog } = await projectDialog();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    await userEvent.keyboard('{Escape}');
    expect(pending?.run.signal.aborted).toBe(true);
    expect(screen.queryByRole('dialog', { name: 'Exporter le projet en PDF' })).toBeNull();
  });

  it('the closing button of the dialog also cancels the task', async () => {
    const { dialog } = await projectDialog();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    await userEvent.click(screen.getByTestId('project-pdf-close'));
    expect(pending?.run.signal.aborted).toBe(true);
  });

  it('Escape with nothing running closes without a task', async () => {
    await projectDialog();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog', { name: 'Exporter le projet en PDF' })).toBeNull();
    expect(exportProjectPdf).not.toHaveBeenCalled();
  });
});

describe('the refusals', () => {
  it.each([
    ['maps', 'Plus de 100 cartes : choisissez « Cette carte et ses niveaux ».'],
    ['pages', 'Plus de 1 000 pages : décochez les fiches ou exportez une carte détaillée et ses niveaux.'],
    ['busy', 'Export en cours…'],
  ] as const)('past the bound of the %s the dialog says it, stays open and gives the boxes back', async (reason, message) => {
    const { dialog } = await projectDialog();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    await act(async () => {
      pending?.end({ kind: 'refused', reason, message });
      await Promise.resolve();
    });
    expect(within(dialog).getByRole('alert')).toHaveTextContent(message);
    expect(within(dialog).queryByRole('progressbar')).toBeNull();
    expect(within(dialog).getAllByRole('checkbox').some(isOff)).toBe(false);
    expect(within(dialog).getByRole('button', { name: 'Créer le PDF' })).toBeEnabled();
    await userEvent.click(box(dialog, 'Avec les fiches'));
    expect(within(dialog).queryByRole('alert')).toBeNull();
  });
});
