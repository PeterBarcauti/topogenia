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
import { closeProjectProperties, projectProperties } from '../../project/properties/projectProperties.ts';
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

/** A sub-map of the project VTE, whose root map « Ventes à l’export » names the project. */
const subMap = () => sampleMap({ name: 'Traiter la commande', breadcrumb: [{ map_id: 'root', name: 'Ventes à l’export', source_language: 'fr', translations: {} }, { map_id: MAP_ID, name: 'Traiter la commande', source_language: 'fr', translations: {} }] });

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
  return { notes, open, choose: (id: string) => userEvent.click(screen.getByTestId(`menu-file-file.${id}`)) };
}

describe('the dialogs of the File menu (B-2.4-53, B-2.4-55, ADR 058)', () => {
  it('« Ouvrir un fichier .pmap… » opens the dialog of the home page', async () => {
    const { choose } = await fileMenu();
    await choose('open');
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier .pmap' })).toBeInTheDocument();
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
    expect(within(dialog).getByTestId('duplicate-name')).toHaveValue('Ventes à l’export (copie)');
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
    expect(screen.getByRole('dialog', { name: 'Exporter le projet VTE' })).toBeInTheDocument();
  });

  it('« Remplacer par un paquet » reads the version of the project and the days of the trash, then opens the replacement', async () => {
    const sent = fakeApi({
      [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VTE', version: 9 } },
      'GET /api/trash': [{ json: { retention_days: 45, projects: [] } }, { refused: 403, code: 'ACCES_REFUSE' }],
    });
    const { choose, open } = await fileMenu();
    await choose('replace');
    const dialog = await screen.findByRole('dialog', { name: 'Remplacer le projet VTE' });
    expect(dialog).toHaveTextContent('restaurable pendant 45 jours');
    expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(1);
    await userEvent.click(within(dialog).getByTestId('import-package-close'));
    open();
    await choose('replace');
    expect(await screen.findByRole('dialog', { name: 'Remplacer le projet VTE' })).toHaveTextContent('restaurable pendant 30 jours');
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
      expect(downloads.files.map((file) => file.name)).toEqual([expect.stringMatching(/^VTE_Ventes-a-lexport_\d{4}-\d{2}-\d{2}\.pmap$/) as unknown]);
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

describe('the exports of the map (B-2.4-56 to B-2.4-58, B-2.14-01)', () => {
  it('« Exporter en PDF (A3 paysage) », « Exporter en image PNG » and « Exporter en Excel » each save their file, and none other (no SVG)', { timeout: 15_000 }, async () => {
    const pixel = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
    vi.mocked(toCanvas).mockResolvedValue({ toBlob: (done: (blob: Blob) => void) => { done(new Blob(['png'], { type: 'image/png' })); }, toDataURL: () => pixel } as unknown as HTMLCanvasElement);
    const downloads = captureDownloads();
    try {
      fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VTE', classification: 'internal', version: 4 } } });
      const { choose, open, notes } = await fileMenu();
      expect(screen.getAllByRole('menuitem').map((item) => item.textContent).filter((text) => text.startsWith('Exporter en'))).toEqual(['Exporter en PDF (A3 paysage)', 'Exporter en image PNG', 'Exporter en Excel']);
      for (const format of ['pdf', 'png', 'xlsx']) {
        await choose(format);
        // The PDF and the workbook take seconds in jsdom under load (exceljs, jsPDF): the wait of the other exports (5 s).
        await vi.waitFor(() => {
          expect(notes.at(-1)?.[1]).toBe('success');
        }, { timeout: 5_000 });
        notes.length = 0;
        open();
      }
      expect(downloads.files.map((file) => file.name.replace(/_\d{4}-\d{2}-\d{2}/, ''))).toEqual(['VTE_Traiter-la-commande.pdf', 'VTE_Traiter-la-commande.png', 'VTE_Traiter-la-commande.xlsx']);
    } finally {
      downloads.restore();
    }
  });
});
