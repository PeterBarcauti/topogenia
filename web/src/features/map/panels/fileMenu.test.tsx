// fileMenu.test.tsx
// The entries of the File menu of the map: the dialogs of P6-T03 opened on the project of the map, the import of a package, the copy downloaded, the project to replace read first, the exports of the map (annex B 2.4, ADR 058, ADR 066, AR-154).

import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { toCanvas } from 'html-to-image';
import type { Schema } from '../../../api/client.ts';
import { i18next, PACKAGE_EXTENSION } from '../../../shared/i18n/i18n.ts';
import { captureDownloads } from '../../../test/downloads.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { chooseFileEntry } from '../../../test/fileMenu.ts';
import { pmapOfSvg } from '../../project/files/svgCopy.ts';
import { FORMAT } from '../../project/files/tableImport.ts';
import { closeProjectProperties, projectProperties } from '../../project/properties/projectProperties.ts';
import { ROW_HEIGHT } from '../model/geometry/constants.ts';
import { exportMap } from '../state/actions/exports.ts';
import { createMapEditor } from '../state/editor.ts';

vi.mock('html-to-image', () => ({ toCanvas: vi.fn() }));
// The faces of Inter read from the package on the disk: jsdom serves no file of the application (the real reading is a journey, AR-52).
vi.mock('../export/pdfFonts.ts', async () => {
  const { interFonts } = await import('../../../test/pdf.ts');
  return { pdfFonts: () => Promise.resolve(interFonts()) };
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** A sub-map of the project VIS, whose root map « Visites à l’export » names the project. */
const subMap = () => sampleMap({ name: 'Traiter la commande', breadcrumb: [{ map_id: 'root', name: 'Visites à l’export', source_language: 'fr', translations: {} }, { map_id: MAP_ID, name: 'Traiter la commande', source_language: 'fr', translations: {} }] });

/** The map shown by an editor of an administrator, its File menu open; gives the notifications. */
async function fileMenu(map: Schema<'MapView'> = subMap()) {
  const notes: [string, string | undefined][] = [];
  const editor = createMapEditor({ source: sourceOf(map), storage: memoryStorage(), admin: true, notify: (text, kind) => notes.push([text, kind]) });
  renderMapScreen({ mapId: MAP_ID, editor });
  await screen.findByTestId('map-canvas');
  const open = () => {
    act(() => {
      editor.floating.getState().open({ kind: 'menu', target: { kind: 'file' }, at: { x: 10, y: 40 } });
    });
  };
  open();
  return { notes, open, choose: (id: string) => chooseFileEntry(`file.${id}`) };
}

describe('the dialogs of the File menu (B-2.4-53, B-2.4-55, ADR 058)', () => {
  it('« Ouvrir un fichier… » opens the dialog of the home page', async () => {
    const { choose } = await fileMenu();
    await choose('open');
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier' })).toBeInTheDocument();
  });

  it('« Importer un paquet » opens the import of a package as a new project, as on the home page (AR-154)', async () => {
    const { choose } = await fileMenu();
    await choose('import');
    const dialog = screen.getByRole('dialog', { name: `Importer un paquet ${PACKAGE_EXTENSION}` });
    expect(dialog).toHaveTextContent('Le paquet devient un nouveau projet.');
  });

  it('« Dupliquer le projet… » proposes the name of the project, its root map, followed by « (copie) »', async () => {
    const { choose } = await fileMenu();
    await choose('duplicate');
    const dialog = screen.getByRole('dialog', { name: 'Dupliquer le projet' });
    expect(within(dialog).getByTestId('duplicate-name')).toHaveValue('Visites à l’export (copie)');
  });

  it('« Propriétés du projet… », last in a group of its own, opens the properties of the project, whose dialog the header shows', async () => {
    closeProjectProperties();
    const { choose } = await fileMenu();
    const entries = screen.getAllByRole('menuitem');
    expect([entries.at(-1)?.textContent, entries.at(-1)?.classList.contains('menu-item--group')]).toEqual(['Propriétés du projet…', true]);
    await choose('properties');
    expect([projectProperties.getState().open, screen.queryByRole('menu')]).toEqual([true, null]);
    closeProjectProperties();
  });

  it('« Exporter le projet » opens the export of the package of the project', async () => {
    const { choose } = await fileMenu();
    await choose('export');
    expect(screen.getByRole('dialog', { name: 'Exporter le projet VIS' })).toBeInTheDocument();
  });

  it('« Remplacer par un paquet » reads the version of the project and the days of the trash, then opens the replacement', async () => {
    const sent = fakeApi({
      [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', version: 9 } },
      'GET /api/trash': [{ json: { retention_days: 45, projects: [] } }, { refused: 403, code: 'ACCES_REFUSE' }],
    });
    const { choose, open } = await fileMenu();
    await choose('replace');
    const dialog = await screen.findByRole('dialog', { name: 'Remplacer le projet VIS' });
    expect(dialog).toHaveTextContent('restaurable pendant 45 jours');
    expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(1);
    await userEvent.click(within(dialog).getByTestId('import-package-close'));
    open();
    await choose('replace');
    expect(await screen.findByRole('dialog', { name: 'Remplacer le projet VIS' })).toHaveTextContent('restaurable pendant 30 jours');
  });

  it('a project that cannot be read is said, and no dialog opens', async () => {
    fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { refused: 404, code: 'INTROUVABLE', message: 'Projet introuvable.' }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } } });
    const { choose, notes } = await fileMenu();
    await choose('replace');
    await vi.waitFor(() => {
      expect(notes).toEqual([['Projet introuvable.', 'error']]);
    });
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});

describe('« Télécharger une copie (.pmap) » (B-2.4-54)', () => {
  it('saves the maps without the sheets under CODE_name_date.pmap, then says where', async () => {
    const downloads = captureDownloads();
    try {
      const sent = fakeApi({ [`GET /api/projects/${PROJECT_ID}/pmap`]: { text: '{"format":"pmap"}' } });
      const { choose, notes } = await fileMenu();
      await choose('download');
      await vi.waitFor(() => {
        expect(notes).toEqual([['Copie des cartes téléchargée (sans les fiches) : le fichier .pmap est dans le dossier Téléchargements.', 'success']]);
      });
      expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}/pmap`)[0]?.query.get('with_sheets')).toBe('false');
      expect(downloads.files.map((file) => file.name)).toEqual([expect.stringMatching(/^VIS_Visites-a-lexport_\d{4}-\d{2}-\d{2}\.pmap$/) as unknown]);
    } finally {
      downloads.restore();
    }
  });

  it('a failure is said', async () => {
    fakeApi({ [`GET /api/projects/${PROJECT_ID}/pmap`]: { refused: 403, code: 'ACCES_REFUSE', message: 'Droits insuffisants.' } });
    const { choose, notes } = await fileMenu();
    await choose('download');
    await vi.waitFor(() => {
      expect(notes).toEqual([['Droits insuffisants.', 'error']]);
    });
  });
});

describe('« Enregistrer la carte ouverte (.pmap) » (3.3, lot 4, F-208)', () => {
  it('saves the map shown with its detailed maps, read as a copy, under the name of the map, then says what it holds', async () => {
    const downloads = captureDownloads();
    try {
      const sent = fakeApi({ [`GET /api/maps/${MAP_ID}/pmap`]: { text: '{"format":"pmap"}' } });
      const { choose, notes } = await fileMenu();
      await choose('map_pmap');
      await vi.waitFor(() => {
        expect(notes).toEqual([['Carte enregistrée avec ses cartes détaillées, ses fiches et ses commentaires : le fichier .pmap est dans le dossier Téléchargements.', 'success']]);
      });
      expect(callsTo(sent, 'GET', `/api/maps/${MAP_ID}/pmap`).map((call) => call.query.get('purpose'))).toEqual(['copy']);
      expect(downloads.files.map((file) => file.name)).toEqual([expect.stringMatching(/^VIS_Traiter-la-commande_\d{4}-\d{2}-\d{2}\.pmap$/) as unknown]);
      expect(await downloads.files[0]?.content.text()).toBe('{"format":"pmap"}');
    } finally {
      downloads.restore();
    }
  });

  it('a failure is said, and nothing is saved', async () => {
    const downloads = captureDownloads();
    try {
      fakeApi({ [`GET /api/maps/${MAP_ID}/pmap`]: { refused: 404, code: 'INTROUVABLE', message: 'Carte introuvable.' } });
      const { choose, notes } = await fileMenu();
      await choose('map_pmap');
      await vi.waitFor(() => {
        expect(notes).toEqual([['Carte introuvable.', 'error']]);
      });
      expect(downloads.files).toEqual([]);
    } finally {
      downloads.restore();
    }
  });
});

describe('the exports of the map (B-2.4-56 to B-2.4-58, B-2.14-01)', () => {
  it('the group « Exporter la carte » shows short labels, each entry named by its whole label, the print for a wall set apart', async () => {
    await fileMenu();
    await userEvent.click(screen.getByTestId('menu-file-cascade-export'));
    const entries = within(screen.getByRole('menu', { name: 'Exporter la carte' })).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual(['PDF (A3 paysage)', 'Image PNG', 'Image SVG…', 'Excel', 'Markdown (Mermaid)', 'BPMN 2.0 (.bpmn)…', 'Imprimer en grand…']);
    expect(entries.map((entry) => entry.getAttribute('aria-label'))).toEqual(['Exporter en PDF (A3 paysage)', 'Exporter en image PNG', 'Exporter en image SVG…', 'Exporter en Excel', 'Exporter en Markdown (Mermaid)', 'Exporter en BPMN 2.0 (.bpmn)…', null]);
    expect(entries.map((entry) => entry.classList.contains('menu-item--group'))).toEqual([false, false, false, false, false, false, true]);
  });

  it('« Exporter en PDF (A3 paysage) », « Exporter en image PNG », « Exporter en Excel » and « Exporter en Markdown (Mermaid) » each save their file at once', { timeout: 20_000 }, async () => {
    const pixel = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
    vi.mocked(toCanvas).mockResolvedValue({ toBlob: (done: (blob: Blob) => void) => { done(new Blob(['png'], { type: 'image/png' })); }, toDataURL: () => pixel } as unknown as HTMLCanvasElement);
    const downloads = captureDownloads();
    try {
      fakeApi({
        [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'internal', version: 4 } },
        // The action plan of the map, read for the workbook (Makigami M7): without a pain point, no sheet more.
        [`GET /api/maps/${MAP_ID}/action-plan`]: { json: { lines: [] } },
      });
      const { choose, open, notes } = await fileMenu();
      for (const format of ['pdf', 'png', 'xlsx', 'md']) {
        await choose(format);
        // The PDF and the workbook take seconds in jsdom under load (exceljs, jsPDF): the wait of the other exports (5 s).
        await vi.waitFor(() => {
          expect(notes.at(-1)?.[1]).toBe('success');
        }, { timeout: 5_000 });
        notes.length = 0;
        open();
      }
      expect(downloads.files.map((file) => file.name.replace(/_\d{4}-\d{2}-\d{2}/, ''))).toEqual(['VIS_Traiter-la-commande.pdf', 'VIS_Traiter-la-commande.png', 'VIS_Traiter-la-commande.xlsx', 'VIS_Traiter-la-commande.md']);
    } finally {
      downloads.restore();
    }
  });
});

describe('the image SVG of the map (plan 3.1, L7)', () => {
  it('opens its dialog, its copy unticked; saved without it, then with it, read from the host and read back by the opening', { timeout: 20_000 }, async () => {
    const downloads = captureDownloads();
    try {
      const sent = fakeApi({
        [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'internal', version: 4 } },
        [`GET /api/maps/${MAP_ID}/pmap`]: { text: `{"format":"${FORMAT}","version":"1.3"}` },
      });
      const { choose, open, notes } = await fileMenu();
      for (const tick of [false, true]) {
        await choose('svg');
        const dialog = screen.getByRole('dialog', { name: 'Exporter en image SVG' });
        const box = within(dialog).getByRole('checkbox', { name: 'Inclure une copie modifiable' });
        await vi.waitFor(() => {
          expect(box).toBeEnabled();
        });
        expect(box).not.toBeChecked();
        if (tick) await userEvent.click(box);
        await userEvent.click(within(dialog).getByRole('button', { name: 'Exporter' }));
        await vi.waitFor(() => {
          expect(notes.at(-1)?.[1]).toBe('success');
        }, { timeout: 5_000 });
        notes.length = 0;
        open();
      }
      expect(downloads.files.map((file) => file.name.replace(/_\d{4}-\d{2}-\d{2}/, ''))).toEqual(['VIS_Traiter-la-commande.svg', 'VIS_Traiter-la-commande.svg']);
      const [plain, editable] = await Promise.all(downloads.files.map((file) => file.content.text()));
      expect([plain?.includes('<metadata'), pmapOfSvg(editable ?? '')]).toEqual([false, `{"format":"${FORMAT}","version":"1.3"}`]);
      expect(callsTo(sent, 'GET', `/api/maps/${MAP_ID}/pmap`)).toHaveLength(1);
    } finally {
      downloads.restore();
    }
  });

  it('draws the empty rows a lane shows, as the canvas and the images PNG and PDF (lot G of the plan 3.1)', { timeout: 20_000 }, async () => {
    const downloads = captureDownloads();
    try {
      fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'internal', version: 4 } } });
      const editor = createMapEditor({ source: sourceOf(subMap()), storage: memoryStorage(), notify: () => undefined });
      await editor.store.getState().load(MAP_ID);
      const shown: readonly Readonly<Record<string, number>>[] = [{}, { l1: 3 }];
      for (const rows of shown) {
        editor.store.setState({ rowsAtLeast: rows });
        await exportMap(editor, 'svg');
      }
      const heights = (await Promise.all(downloads.files.map((file) => file.content.text()))).map((svg) => Number(/<svg[^>]*\sheight="([\d.]+)"/.exec(svg)?.[1]));
      expect((heights[1] ?? 0) - (heights[0] ?? 0)).toBe(2 * ROW_HEIGHT);
    } finally {
      downloads.restore();
    }
  });

  it('draws the marks of the layer « Analyse » when it is on, as the images PNG and PDF capture them (Makigami M9)', { timeout: 20_000 }, async () => {
    const downloads = captureDownloads();
    try {
      fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'internal', version: 4 } } });
      const map = subMap();
      const classified = { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, value_class: 'waste' as const } : step)) };
      const editor = createMapEditor({ source: sourceOf(classified), storage: memoryStorage(), notify: () => undefined });
      await editor.store.getState().load(MAP_ID);
      await exportMap(editor, 'svg');
      editor.preferences.getState().set('showAnalysis', true);
      await exportMap(editor, 'svg');
      const [off, on] = await Promise.all(downloads.files.map((file) => file.content.text()));
      expect([off?.includes('class="value"'), on?.includes('class="value"')]).toEqual([false, true]);
    } finally {
      downloads.restore();
    }
  });

  it('offers no copy for a project « Strictement confidentiel », and says why', async () => {
    const sent = fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'strictly_confidential', version: 4 } } });
    const { choose } = await fileMenu();
    await choose('svg');
    const dialog = screen.getByRole('dialog', { name: 'Exporter en image SVG' });
    expect(await within(dialog).findByText('Indisponible pour un projet « Strictement confidentiel ».')).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: 'Inclure une copie modifiable' })).toBeDisabled();
    expect(callsTo(sent, 'GET', `/api/maps/${MAP_ID}/pmap`)).toHaveLength(0);
  });
});
