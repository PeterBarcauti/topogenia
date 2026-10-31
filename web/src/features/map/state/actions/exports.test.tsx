// exports.test.tsx
// The exports of the map shown: the canvas captured in its state of export, the PNG with the classification under the map, the PDF A3 with its header, the workbook, the notifications, the failures, nothing written nor undone (annex B 2.14, B-2.6-45, S-4.1-08).

import { act } from '@testing-library/react';
import ExcelJS from 'exceljs';
import { toCanvas } from 'html-to-image';
import { i18next, PRODUCT_NAME } from '../../../../shared/i18n/i18n.ts';
import { captureDownloads } from '../../../../test/downloads.ts';
import { showEditor } from '../../../../test/editor.tsx';
import { editorOf } from '../../../../test/editorStore.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap } from '../../../../test/maps.ts';
import { pdfTexts } from '../../../../test/pdf.ts';
import { exportMap } from './exports.ts';

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

const project = (classification = 'confidential') => ({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VTE', classification, version: 4 } } });

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
    expect(downloads.files).toEqual([{ name: `VTE_Ventes_${day()}.png`, content: PNG }]);
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
    editor.store.setState({ map: sampleMap({ translations: { en: 'Sales' } }) });
    await act(() => exportMap(editor, 'png'));
    expect([downloads.files[0]?.name, seen[0]?.caption]).toEqual([`VTE_Sales_${day()}.png`, 'Classification: Strictly confidential']);
  });
});

describe('the PDF (B-2.14-13 to B-2.14-18, S-4.1-08)', () => {
  it('places the image captured without the line under the map in an A3 landscape, with its title, breadcrumb, date, classification and footer', async () => {
    const { editor, notes } = await exporting();
    editor.store.setState({ map: sampleMap({ name: 'Traiter la commande', breadcrumb: [{ map_id: 'root', name: 'Ventes', source_language: 'fr', translations: {} }, { map_id: 'here', name: 'Traiter la commande', source_language: 'fr', translations: {} }] }) });
    await act(() => exportMap(editor, 'pdf'));
    expect(seen.map((capture) => [capture.exporting, capture.caption])).toEqual([[true, null]]);
    expect(downloads.files.map((file) => [file.name, file.content.type])).toEqual([[`VTE_Traiter-la-commande_${day()}.pdf`, 'application/pdf']]);
    const pdf = new TextDecoder('latin1').decode(await downloads.files[0]?.content.arrayBuffer());
    const texts = pdfTexts(pdf).map((text) => text.text);
    const date = new Date().toLocaleString('fr', { dateStyle: 'long', timeStyle: 'short' });
    expect(texts).toEqual(['VTE - Traiter la commande', 'Ventes > Traiter la commande', date, 'Classification : Confidentiel', `Généré par ${PRODUCT_NAME}`]);
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
    expect(texts).toEqual(expect.arrayContaining(['Flux de valeur · VTE - Ventes', 'Bilan', 'Chemin principal', 'Contrôle', 'Complet : rien ne manque']));
  });

  it('in English: the map and the breadcrumb translated', async () => {
    const { editor } = await exporting();
    await i18next.changeLanguage('en');
    editor.store.setState({ map: sampleMap({ translations: { en: 'Sales' }, breadcrumb: [{ map_id: 'root', name: 'Ventes', source_language: 'fr', translations: { en: 'Sales' } }, { map_id: 'here', name: 'Commande', source_language: 'fr', translations: { en: 'Order' } }] }) });
    await act(() => exportMap(editor, 'pdf'));
    const pdf = new TextDecoder('latin1').decode(await downloads.files[0]?.content.arrayBuffer());
    expect(pdfTexts(pdf).map((text) => text.text).filter((text) => /^VTE|>|Classification|Generated/.test(text)))
      .toEqual(['VTE - Sales', 'Sales > Order', 'Classification: Confidential', `Generated by ${PRODUCT_NAME}`]);
    await act(() => exportMap(editor, 'xlsx'));
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await (downloads.files[1]?.content as Blob).arrayBuffer());
    expect([book.worksheets[0]?.name, book.worksheets[0]?.getCell('C3').value, book.worksheets[0]?.getCell('E3').value]).toEqual(['Steps', 'Check the request', 'Sales']);
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
    expect(downloads.files.map((file) => file.name)).toEqual([`VTE_Ventes_${day()}.xlsx`]);
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await (downloads.files[0]?.content as Blob).arrayBuffer());
    expect(book.worksheets.map((sheet) => [sheet.name, sheet.headerFooter.oddHeader])).toEqual([['Étapes', '&RClassification : Interne'], ['Liens', '&RClassification : Interne'], ['Matrice RACI', '&RClassification : Interne']]);
    expect(book.title).toBe('Ventes');
    expect(notes.at(-1)?.[1]).toBe('success');
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
    expect(sent.map((call) => `${call.method} ${call.path}`)).toEqual(Array.from({ length: 3 }, () => `GET /api/projects/${PROJECT_ID}`));
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
