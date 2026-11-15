// exports.test.tsx
// The exports of the map shown: the canvas captured in its state of export, the PNG with the classification under the map, the PDF A3 with its header, the workbook, the poster for a wall cut from the image SVG, the notifications, the failures, nothing written nor undone (annex B 2.14, B-2.6-45, S-4.1-08, Makigami M9).

import { act } from '@testing-library/react';
import ExcelJS from 'exceljs';
import { toCanvas } from 'html-to-image';
import { i18next, PRODUCT_NAME } from '../../../../shared/i18n/i18n.ts';
import { captureDownloads } from '../../../../test/downloads.ts';
import { showEditor } from '../../../../test/editor.tsx';
import { editorOf } from '../../../../test/editorStore.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, sampleMap } from '../../../../test/maps.ts';
import { pdfTexts } from '../../../../test/pdf.ts';
import { wallPlan } from '../../export/wallPrint.ts';
import { exportMap, wallSize } from './exports.ts';

vi.mock('html-to-image', () => ({ toCanvas: vi.fn() }));
// The faces of Inter read from the package on the disk: jsdom serves no file of the application (the real reading is a journey of P6-T10).
vi.mock('../../export/pdfFonts.ts', async () => {
  const { interFonts } = await import('../../../../test/pdf.ts');
  return { pdfFonts: () => Promise.resolve(interFonts()) };
});

/** A white pixel, the image of the canvas captured. */
const PIXEL = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
const PNG = new Blob(['png'], { type: 'image/png' });

/** What the canvas looked like when it was captured: its class, the line under the map, the element captured. */
interface Seen {
  readonly exporting: boolean;
  readonly caption: string | null;
  readonly view: Element;
}

let downloads: ReturnType<typeof captureDownloads>;
let seen: Seen[];

beforeEach(async () => {
  // The clock held still: the date of a file name and of the PDF is the one the test reads, whatever minute it runs at.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 29, 10, 15, 20));
  await i18next.changeLanguage('fr');
  downloads = captureDownloads();
  seen = [];
  vi.mocked(toCanvas).mockReset().mockImplementation((view: HTMLElement) => {
    const canvas = view.closest('[data-testid="map-canvas"]');
    seen.push({ exporting: canvas?.classList.contains('is-exporting') ?? false, caption: canvas?.querySelector('[data-testid="export-caption"]')?.textContent ?? null, view });
    const toDataURL = (type: string) => (type === 'image/png' ? PIXEL : 'data:,');
    return Promise.resolve({ toBlob: (done: (blob: Blob) => void) => { done(PNG); }, toDataURL } as unknown as HTMLCanvasElement);
  });
});

afterEach(() => {
  downloads.restore();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

/** A line of the action plan of the map (Makigami M7). */
const planLine = {
  pain_point_id: 'p1', code: 'VIS-I001', description: 'Ressaisie', status: 'reported', lost_hours_per_month: 2, action: 'Lire l’extraction', owner: 'Paie',
  due_date: '2026-12-15', map_id: MAP_ID, map: { text: 'Visites', source_language: 'fr', translations: {} }, step_id: 's1', step_code: 'VIS-E001',
  step: { text: 'Saisir', source_language: 'fr', translations: {} }, lane: null,
};

/** The project readable, and the action plan of the map: empty unless lines are given. */
const project = (classification = 'confidential', lines: readonly object[] = []) => ({
  [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification, version: 4 } },
  [`GET /api/maps/${MAP_ID}/action-plan`]: { json: { lines } },
});

/** The editor of the sample map, its project readable; gives the calls sent and the notifications. */
async function exporting(classification?: string) {
  const sent = fakeApi(project(classification));
  const shown = await showEditor(sampleMap());
  return { ...shown, sent };
}

const day = () => new Date().toISOString().slice(0, 10);

describe('the PNG (B-2.14-12, B-2.14-29, B-2.14-30, S-4.1-08)', () => {
  it('captures the view of the canvas in its state of export, the classification under the map, and saves the image; then the canvas is itself again', async () => {
    const { editor, canvas, notes, sent } = await exporting();
    await act(() => exportMap(editor, 'png'));
    expect(seen).toEqual([{ exporting: true, caption: 'Classification : Confidentiel', view: canvas.querySelector('.react-flow__viewport') }]);
    expect(downloads.files).toEqual([{ name: `VIS_Visites_${day()}.png`, content: PNG }]);
    expect(notes).toEqual([['Export en cours…', undefined], ['Export terminé : le fichier est dans le dossier Téléchargements.', 'success']]);
    expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(1);
    expect([editor.store.getState().exporting, canvas.classList.contains('is-exporting'), canvas.querySelector('[data-testid="export-caption"]')]).toEqual([null, false, null]);
    const options = vi.mocked(toCanvas).mock.lastCall?.[1];
    const bounds = editor.canvas.target()?.bounds;
    expect([options?.width, options?.height]).toEqual([Math.ceil((bounds?.width ?? 0) + 48), Math.ceil((bounds?.height ?? 0) + 48 + 20 + 24)]);
  });

  it('the name of the file and the classification follow the language shown', async () => {
    const { editor } = await exporting('strictly_confidential');
    await i18next.changeLanguage('en');
    editor.store.setState({ map: sampleMap({ translations: { en: 'Visits' } }) });
    await act(() => exportMap(editor, 'png'));
    expect([downloads.files[0]?.name, seen[0]?.caption]).toEqual([`VIS_Visits_${day()}.png`, 'Classification: Strictly confidential']);
  });
});

describe('the PDF (B-2.14-13 to B-2.14-18, S-4.1-08)', () => {
  it('places the image captured without the line under the map in an A3 landscape, with its title, breadcrumb, date, classification and footer', async () => {
    const { editor, notes } = await exporting();
    editor.store.setState({ map: sampleMap({ name: 'Traiter la commande', breadcrumb: [{ map_id: 'root', name: 'Visites', source_language: 'fr', translations: {} }, { map_id: 'here', name: 'Traiter la commande', source_language: 'fr', translations: {} }] }) });
    await act(() => exportMap(editor, 'pdf'));
    expect(seen.map((capture) => [capture.exporting, capture.caption])).toEqual([[true, null]]);
    expect(downloads.files.map((file) => [file.name, file.content.type])).toEqual([[`VIS_Traiter-la-commande_${day()}.pdf`, 'application/pdf']]);
    const pdf = new TextDecoder('latin1').decode(await downloads.files[0]?.content.arrayBuffer());
    const texts = pdfTexts(pdf).map((text) => text.text);
    const date = new Date().toLocaleString('fr', { dateStyle: 'long', timeStyle: 'short' });
    expect(texts).toEqual(['VIS - Traiter la commande', 'Visites > Traiter la commande', date, 'Classification : Confidentiel', `Généré par ${PRODUCT_NAME}`]);
    expect(pdf).toContain('/MediaBox [0 0 1190.5');
    // A map far wider than high fills the width of the page between its margins (396 mm), whatever its pixel ratio.
    const width = Number(/([\d.]+) 0 0 [\d.]+ [\d.]+ [\d.]+ cm/.exec(pdf)?.[1]);
    expect(width / (72 / 25.4)).toBeCloseTo(396, 3);
    expect(notes.at(-1)).toEqual(['Export terminé : le fichier est dans le dossier Téléchargements.', 'success']);
  });

  it('with the value stream layer on, adds the page of its summary, main path and check; off, the map alone (spec 3.0 section 15.4)', { timeout: 15_000 }, async () => {
    const { editor } = await exporting();
    await act(() => exportMap(editor, 'pdf'));
    act(() => {
      editor.preferences.getState().set('showValueStream', true);
    });
    await act(() => exportMap(editor, 'pdf'));
    const [off, on] = await Promise.all(downloads.files.map(async (file) => new TextDecoder('latin1').decode(await file.content.arrayBuffer())));
    expect([off?.match(/\/Type \/Page[^s]/g)?.length, on?.match(/\/Type \/Page[^s]/g)?.length]).toEqual([1, 2]);
    const texts = pdfTexts(on ?? '').map((text) => text.text);
    expect(texts).toEqual(expect.arrayContaining(['Flux de valeur · VIS - Visites', 'Bilan', 'Chemin principal', 'Contrôle', 'Complet : rien ne manque']));
  });

  it('in English: the map and the breadcrumb translated', async () => {
    const { editor } = await exporting();
    await i18next.changeLanguage('en');
    editor.store.setState({ map: sampleMap({ translations: { en: 'Visits' }, breadcrumb: [{ map_id: 'root', name: 'Visites', source_language: 'fr', translations: { en: 'Visits' } }, { map_id: 'here', name: 'Commande', source_language: 'fr', translations: { en: 'Order' } }] }) });
    await act(() => exportMap(editor, 'pdf'));
    const pdf = new TextDecoder('latin1').decode(await downloads.files[0]?.content.arrayBuffer());
    expect(pdfTexts(pdf).map((text) => text.text).filter((text) => /^VIS|>|Classification|Generated/.test(text)))
      .toEqual(['VIS - Visits', 'Visits > Order', 'Classification: Confidential', `Generated by ${PRODUCT_NAME}`]);
    await act(() => exportMap(editor, 'xlsx'));
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await (downloads.files[1]?.content as Blob).arrayBuffer());
    expect([book.worksheets[0]?.name, book.worksheets[0]?.getCell('C3').value, book.worksheets[0]?.getCell('E3').value]).toEqual(['Steps', 'Check the request', 'Visits']);
  }, 15_000);

  it('the image is captured once the canvas has drawn its state of export, 50 ms after at least', async () => {
    const { editor } = await exporting();
    let since = Number.NaN;
    const unsubscribe = editor.store.subscribe((state) => {
      if (state.exporting?.caption !== undefined && state.exporting.caption !== null && Number.isNaN(since)) since = performance.now();
    });
    let waited = 0;
    vi.mocked(toCanvas).mockImplementationOnce(() => {
      waited = performance.now() - since;
      return Promise.resolve({ toBlob: (done: (blob: Blob) => void) => { done(PNG); } } as unknown as HTMLCanvasElement);
    });
    await act(() => exportMap(editor, 'png'));
    unsubscribe();
    // A pause of 50 ms on the clock of the timers; the clock of the page may count a fraction less.
    expect(waited).toBeGreaterThanOrEqual(45);
  });

  it('the fonts of the page go with the image, read from its style sheets', async () => {
    const { editor } = await exporting();
    const style = document.createElement('style');
    style.textContent = '@font-face { font-family: "Inter Variable"; }';
    document.head.append(style);
    try {
      await act(() => exportMap(editor, 'png'));
      expect(vi.mocked(toCanvas).mock.lastCall?.[1]?.fontEmbedCSS).toContain('Inter Variable');
    } finally {
      style.remove();
    }
  });
});

describe('the workbook (B-2.14-20, S-4.1-08)', () => {
  it('builds the three sheets without any capture, the classification printed on each', { timeout: 15_000 }, async () => {
    const { editor, notes } = await exporting('internal');
    await act(() => exportMap(editor, 'xlsx'));
    expect(seen).toEqual([]);
    expect(downloads.files.map((file) => file.name)).toEqual([`VIS_Visites_${day()}.xlsx`]);
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await (downloads.files[0]?.content as Blob).arrayBuffer());
    expect(book.worksheets.map((sheet) => [sheet.name, sheet.headerFooter.oddHeader])).toEqual([['Étapes', '&RClassification : Interne'], ['Liens', '&RClassification : Interne'], ['Matrice RACI', '&RClassification : Interne']]);
    expect(book.title).toBe('Visites');
    expect(notes.at(-1)?.[1]).toBe('success');
  });

  it('adds the action plan of the map after its sheets when it holds a pain point (Makigami M7)', { timeout: 15_000 }, async () => {
    fakeApi(project('internal', [planLine]));
    const { editor } = await showEditor(sampleMap());
    await act(() => exportMap(editor, 'xlsx'));
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await (downloads.files[0]?.content as Blob).arrayBuffer());
    expect(book.worksheets.map((sheet) => sheet.name)).toEqual(['Étapes', 'Liens', 'Matrice RACI', 'Plan d’actions']);
    const plan = book.getWorksheet('Plan d’actions');
    expect([plan?.getCell('B2').value, plan?.getCell('G2').value, plan?.getCell('I2').value, plan?.getCell('J2').value])
      .toEqual(['VIS-E001', 'Lire l’extraction', '2026-12-15', 'signalé']);
  });
});

describe('the Markdown (plan 3.1, L3)', () => {
  /** The Markdown saved for the sample map, its project and its action plan as given. */
  async function markdownOf(lines: readonly object[]): Promise<string> {
    fakeApi(project('internal', lines));
    const { editor } = await showEditor(sampleMap());
    await act(() => exportMap(editor, 'md'));
    return (downloads.files[0]?.content as Blob).text();
  }

  it('gives the action plan of the map after the tables of the workbook when it holds a pain point, as the workbook does (Makigami M7; audit 3.1 V3, U9)', async () => {
    const text = await markdownOf([planLine]);
    expect([...text.matchAll(/^## (.+)$/gm)].map((match) => match[1])).toEqual(['Diagramme', 'Étapes', 'Liens', 'Matrice RACI', 'Plan d’actions']);
    const table = text.slice(text.indexOf('## Plan d’actions')).split('\n');
    expect(table.slice(2, 5)).toEqual([
      '| Carte | Code | Étape | Couloir | Irritant | Description | Action | Responsable | Échéance | État | Heures perdues par mois |',
      '| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |',
      '| Visites | VIS-E001 | Saisir |  | VIS-I001 | Ressaisie | Lire l’extraction | Paie | 2026-12-15 | signalé | 2 |',
    ]);
  });

  it('gives no action plan when the map holds no pain point', async () => {
    expect(await markdownOf([])).not.toContain('Plan d’actions');
  });
});

describe('the failures and what an export leaves (B-2.14-30, B-2.6-45)', () => {
  const FAILED = ['L’export a échoué. Réessayer dans un instant.', 'error'];

  it('a project that cannot be read: the failure is said, nothing is captured nor saved, the canvas is itself again', async () => {
    fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { refused: 403, code: 'ACCES_REFUSE' } });
    const { editor, canvas, notes } = await showEditor(sampleMap());
    await act(() => exportMap(editor, 'pdf'));
    expect([notes, seen, downloads.files]).toEqual([[['Export en cours…', undefined], FAILED], [], []]);
    expect([editor.store.getState().exporting, canvas.classList.contains('is-exporting')]).toEqual([null, false]);
  });

  it('a capture that fails is said, and the canvas leaves its state of export', async () => {
    vi.mocked(toCanvas).mockRejectedValue(new Error('tainted'));
    const { editor, canvas, notes } = await exporting();
    await act(() => exportMap(editor, 'png'));
    expect([notes.at(-1), downloads.files, canvas.classList.contains('is-exporting'), canvas.querySelector('[data-testid="export-caption"]')]).toEqual([FAILED, [], false, null]);
  });

  it('nothing is written nor undone: the project is read, the history of the map stays as it was', async () => {
    const { editor, sent } = await exporting();
    const history = editor.store.getState().history;
    for (const format of ['png', 'pdf', 'xlsx'] as const) await act(() => exportMap(editor, format));
    // The workbook reads the action plan of the map besides the project (Makigami M7).
    expect(sent.map((call) => `${call.method} ${call.path}`)).toEqual([...Array.from({ length: 3 }, () => `GET /api/projects/${PROJECT_ID}`),
      `GET /api/maps/${MAP_ID}/action-plan`]);
    expect(editor.store.getState().history).toBe(history);
    expect(downloads.files).toHaveLength(3);
  });

  it('one export at a time: an export asked while another runs is left alone', async () => {
    const sent = fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { pending: true } });
    const { editor, notes } = await showEditor(sampleMap());
    act(() => {
      void exportMap(editor, 'pdf');
    });
    await act(() => exportMap(editor, 'xlsx'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(1);
    });
    expect([notes, editor.store.getState().exporting]).toEqual([[['Export en cours…', undefined]], { format: 'pdf', caption: null }]);
  });

  it('an image needs the canvas shown: without it the failure is said; the workbook needs none', { timeout: 15_000 }, async () => {
    fakeApi(project());
    const { editor, notes } = await editorOf(sampleMap());
    await act(() => exportMap(editor, 'png'));
    await act(() => exportMap(editor, 'xlsx'));
    expect([notes[1], notes[3]?.[1], downloads.files.map((file) => file.name.slice(-5)), editor.store.getState().exporting]).toEqual([FAILED, 'success', ['.xlsx'], null]);
  });
});

describe('the colours read from the charter when the export is made (B-2.14-04, B-2.14-25, B-2.14-26)', () => {
  const TOKENS: Record<string, string> = { '--surface': ' #FFFFFF', '--table-header-bg': ' #F5F5F7', '--raci-r-bg': '#D5F0E3', '--raci-a-bg': 'rgb(217, 233, 250)' };

  beforeEach(() => {
    const original = window.getComputedStyle.bind(window);
    vi.spyOn(window, 'getComputedStyle').mockImplementation((element, pseudo) =>
      (element === document.documentElement ? ({ getPropertyValue: (name: string) => TOKENS[name] ?? '' } as CSSStyleDeclaration) : original(element, pseudo)));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('the image on --surface; the headers of the workbook on --table-header-bg, each letter on its --raci-*-bg', { timeout: 15_000 }, async () => {
    const { editor } = await exporting();
    await act(() => exportMap(editor, 'png'));
    expect(vi.mocked(toCanvas).mock.lastCall?.[1]?.backgroundColor).toBe('#FFFFFF');
    await act(() => exportMap(editor, 'xlsx'));
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await (downloads.files[1]?.content as Blob).arrayBuffer());
    const colour = (sheet: string, cell: string) => (book.getWorksheet(sheet)?.getCell(cell).fill as { fgColor?: { argb?: string } } | undefined)?.fgColor?.argb;
    expect([colour('Étapes', 'A1'), colour('Matrice RACI', 'C1'), colour('Matrice RACI', 'C2'), colour('Matrice RACI', 'D2')]).toEqual(['FFF5F5F7', 'FFF5F5F7', 'FFD5F0E3', undefined]);
  });
});

describe('the poster for a wall (Makigami M9)', () => {
  /**
   * The browser drawing each page: an image decoded at once, a canvas whose context keeps the size of what it draws, a
   * pixel as its JPEG; its measure of the texts, 7 a letter, serves the layout of the canvas and of the image alike.
   */
  function drawing(): { width: number; height: number }[] {
    const drawn: { width: number; height: number }[] = [];
    vi.stubGlobal('Image', class { src = ''; decode = () => Promise.resolve(); });
    const context = {
      font: '', fillStyle: '', fillRect: vi.fn(), measureText: (text: string) => ({ width: 7 * text.length }),
      drawImage: (_image: unknown, _x: number, _y: number, width: number, height: number) => { drawn.push({ width, height }); },
    };
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(context as unknown as CanvasRenderingContext2D);
    vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue(PIXEL);
    return drawn;
  }

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('cuts the image SVG of the map into the pages of its plan, each drawn at its size, and saves the poster named after the map, without any capture', { timeout: 15_000 }, async () => {
    const drawn = drawing();
    const { editor, notes } = await exporting('internal');
    await act(() => exportMap(editor, 'wall', { wall: { paper: 'a4', across: 2, analysis: false } }));
    expect(notes.at(-1)).toEqual(['Export terminé : le fichier est dans le dossier Téléchargements.', 'success']);
    expect(downloads.files.map((file) => [file.name, file.content.type])).toEqual([[`VIS_Visites-affiche_${day()}.pdf`, 'application/pdf']]);
    const map = editor.store.getState().map ?? sampleMap();
    const plan = wallPlan(wallSize(editor, map), 'a4', 2);
    expect(drawn).toEqual(plan.pages.map((page) => page.pixels));
    const pdf = new TextDecoder('latin1').decode(await downloads.files[0]?.content.arrayBuffer());
    expect(pdf.match(/\/Type \/Page\b/g)).toHaveLength(plan.pages.length);
    expect(pdfTexts(pdf, 210).find((text) => text.face === 'Inter-Regular')?.text).toBe('VIS - Visites · Classification : Interne');
    expect(seen).toEqual([]);
    // Each page drawn from its own image SVG, its address given back once drawn (the file saved gives its own back 10 s
    // later, as the download of an earlier test may give back its own meanwhile: the addresses of the pages are looked for).
    expect(plan.pages.length).toBeGreaterThan(1);
    expect(downloads.revoked).toEqual(expect.arrayContaining(plan.pages.map((page) => `blob:fake/${String(page.number)}`)));
    expect(editor.store.getState().exporting).toBeNull();
  });

  it('a page the browser cannot draw is said as a failure, nothing saved', { timeout: 15_000 }, async () => {
    vi.stubGlobal('Image', class { src = ''; decode = () => Promise.reject(new Error('broken')); });
    const { editor, notes } = await exporting('internal');
    await act(() => exportMap(editor, 'wall', { wall: { paper: 'a3', across: 1, analysis: true } }));
    expect([notes.at(-1), downloads.files]).toEqual([['L’export a échoué. Réessayer dans un instant.', 'error'], []]);
  });
});
