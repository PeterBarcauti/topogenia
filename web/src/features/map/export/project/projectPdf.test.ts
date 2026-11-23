// projectPdf.test.ts
// The PDF of the project read back page by page: the title page and the contents A4 portrait, each map A3 landscape level by level under its path, the pages of each option and only them, an empty map said, the classification and the number on every page by one last pass, the sheets of the copy and the measure of their texts, the bounds.

import type { Schema } from '../../../../api/client.ts';
import { i18next, PRODUCT_NAME } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT, type PmapDocument, type PmapSheet } from '../../../../shared/pmap.ts';
import { laneOf, mapOf, stepOf } from '../../../../test/maps.ts';
import { interFonts, pdfInfo, pdfOutline, pdfPages, type PdfPage } from '../../../../test/pdf.ts';
import { imagePlacement, PDF_FACES, PDF_MARGIN, type MapImage } from '../pdf.ts';
import {
  buildProjectPdf, copySheets, drawnWidth, PROJECT_PDF_LIMITS, ProjectPdfRefusal,
  type ProjectPdfChoice, type ProjectPdfContent, type ProjectPdfMap, type ProjectPdfProgress, type ProjectPdfTools,
} from './projectPdf.ts';

type MapView = Schema<'MapView'>;

const t = i18next.getFixedT('fr');

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

/** A white pixel, as a PNG and as a JPEG. */
const PNG = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
const JPEG = 'data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=';
const IMAGE: MapImage = { dataUrl: PNG, width: 2000, height: 500 };

const CLASSIFICATION = 'Classification : Confidentiel';
const NONE: ProjectPdfChoice = { sheets: false, raci: false, stream: false, plan: false };
const ALL: ProjectPdfChoice = { sheets: true, raci: true, stream: true, plan: true };
const A4 = { width: 210, height: 297 };
const A3 = { width: 420, height: 297 };

const lanes = [laneOf('l1', 0, { name: 'Accueil' }), laneOf('l2', 1, { name: 'Comptabilité' })];

/** Three levels: Visites, with Accueillir (detailed by Contrôler) and Facturer below it. */
const MAIN = mapOf({
  id: 'm0', name: 'Visites', lanes,
  steps: [
    stepOf('a', 'l1', 0, { code: 'VIS-E001', label: 'Accueillir', sub_map_id: 'm1', sub_map_empty: false }),
    stepOf('b', 'l2', 1, { code: 'VIS-E002', label: 'Facturer', sub_map_id: 'm2', sub_map_empty: false }),
  ],
  raci: [{ step_id: 'a', lane_id: 'l2', role: 'C' }],
});
const ACCUEIL = mapOf({ id: 'm1', name: 'Accueillir', parent_step_id: 'a', depth: 2, lanes, steps: [stepOf('c', 'l1', 0, { code: 'VIS-E003', label: 'Contrôler', sub_map_id: 'm3', sub_map_empty: false })] });
const FACTURE = mapOf({ id: 'm2', name: 'Facturer', parent_step_id: 'b', depth: 2, lanes, steps: [stepOf('e', 'l2', 0, { code: 'VIS-E005', label: 'Émettre' })] });
const CONTROLE = mapOf({ id: 'm3', name: 'Contrôler', parent_step_id: 'c', depth: 3, lanes, steps: [stepOf('d', 'l1', 0, { code: 'VIS-E004', label: 'Vérifier la pièce' })] });

/** A map of the walk, its image asked of the painter. */
const entry = (view: MapView, path: string, image: () => Promise<MapImage> = () => Promise.resolve(IMAGE)): ProjectPdfMap => ({ view, name: view.name, title: `VIS - ${view.name}`, path, image });
const WALK = [entry(MAIN, 'Visites'), entry(ACCUEIL, 'Visites > Accueillir'), entry(FACTURE, 'Visites > Facturer'), entry(CONTROLE, 'Visites > Accueillir > Contrôler')];

const named = (text: string): Schema<'NamedText'> => ({ text, source_language: 'fr', translations: {} });
const LINE: Schema<'ActionPlanView'>['lines'][number] = {
  pain_point_id: 'p1', code: 'IRR-1', description: 'Saisie en double', status: 'reported', lost_hours_per_month: 6, action: 'Lire l’extraction', owner: 'Service paie',
  due_date: '2026-12-15', map_id: 'm0', map: named('Visites'), step_id: 'a', step_code: 'VIS-E001', step: named('Accueillir'), lane: named('Accueil'), open: true,
  receiving_step_id: 'a', receiving_map_id: 'm0', document_id: null, can_fill: false, status_step_id: 'a', version: 1, status_changed_at: null,
};
const PLAN: Schema<'ActionPlanView'> = { open_lost_hours_per_month: 6, lines: [LINE] };

const sheetOf = (overrides: Partial<PmapSheet> = {}): PmapSheet => ({ actor: 'Gestionnaire', purpose: 'Accueillir le visiteur', ...overrides });
const SHEETS = copySheets({
  format: PMAP_FORMAT, version: '1.5', project: { name: 'Visites' },
  map: { lanes: [], steps: [{ id: 'VIS-E001', lane: 'c1', label: 'Accueillir', sheet: sheetOf(), sub_map: { lanes: [], steps: [{ id: 'VIS-E003', lane: 'c1', label: 'Contrôler', sheet: sheetOf({ purpose: 'Contrôler la pièce' }) }] } }] },
});

const contentOf = (overrides: Partial<ProjectPdfContent> = {}): ProjectPdfContent => ({
  facts: { name: 'Visites clients', code: 'VIS', classification: CLASSIFICATION, date: '21 novembre 2026', description: 'Le parcours d’une visite, de la demande à la facture.', summary: null, scope: null },
  choice: NONE, maps: [entry(FACTURE, 'Visites > Facturer')], sheets: new Map(), plan: null, depthCut: false, ...overrides,
});

/** The file as jsPDF wrote it, one character a byte. */
async function pdfOf(content: ProjectPdfContent, tools: Partial<ProjectPdfTools> = {}): Promise<string> {
  const blob = await buildProjectPdf(content, { fonts: interFonts(), t, language: 'fr', shown: 'fr', ...tools });
  expect(blob.type).toBe('application/pdf');
  return new TextDecoder('latin1').decode(await blob.arrayBuffer());
}

/** An image of a page, its box rounded to a thousandth of a millimetre, to compare with the room of the image. */
const boxOf = (image: { readonly filter?: string; readonly x: number; readonly y: number; readonly width: number; readonly height: number } | undefined) =>
  image === undefined ? undefined : [image.filter ?? '', ...[image.x, image.y, image.width, image.height].map((value) => Math.round(value * 1000) / 1000)];
const textsOf = (page: PdfPage | undefined): string[] => (page?.texts ?? []).map((text) => text.text);
const sizeOf = (page: PdfPage): string => (Math.abs(page.width - A3.width) < 0.1 && Math.abs(page.height - A3.height) < 0.1 ? 'A3' : Math.abs(page.width - A4.width) < 0.1 && Math.abs(page.height - A4.height) < 0.1 ? 'A4' : `${String(page.width)}x${String(page.height)}`);
/** The title of a page: its first text in bold 16 at the top left. */
const titleOf = (page: PdfPage | undefined): string => page?.texts.find((text) => text.size === 16 && text.face === PDF_FACES.bold && Math.abs(text.y - (PDF_MARGIN + 4)) < 0.01)?.text ?? '';

/** The width of a text written by jsPDF in a face of Inter and a size, in millimetres. */
async function widthIn(face: 'regular' | 'bold', size: number, text: string): Promise<number> {
  const { jsPDF } = await import('jspdf');
  const ruler = new jsPDF({ unit: 'mm' });
  ruler.addFileToVFS(`${face}.ttf`, interFonts()[face]);
  ruler.addFont(`${face}.ttf`, face, 'normal');
  ruler.setFont(face, 'normal');
  ruler.setFontSize(size);
  return ruler.getTextWidth(text);
}

describe('the pages of the document', () => {
  it('a project of one level: a title page and a contents page A4 portrait, then its map A3 landscape', async () => {
    const pages = pdfPages(await pdfOf(contentOf()));
    expect(pages.map(sizeOf)).toEqual(['A4', 'A4', 'A3']);
    const map = pages[2];
    expect(titleOf(map)).toBe('VIS - Facturer');
    const path = map?.texts.find((text) => text.text === 'Visites > Facturer');
    expect([path?.face, path?.size]).toEqual([PDF_FACES.regular, 10]);
    expect(path?.y).toBeCloseTo(PDF_MARGIN + 11, 3);
    expect(map?.images).toHaveLength(1);
    const room = imagePlacement(A3, IMAGE);
    expect(boxOf(map?.images[0])).toEqual(boxOf({ filter: 'FlateDecode', ...room }));
  });

  it('the image of a map drawn as a JPEG is written as a JPEG in the room of the image', async () => {
    const jpeg: MapImage = { dataUrl: JPEG, width: 800, height: 900 };
    const pages = pdfPages(await pdfOf(contentOf({ maps: [entry(FACTURE, 'Visites > Facturer', () => Promise.resolve(jpeg))] })));
    const room = imagePlacement(A3, jpeg);
    expect(pages[2]?.images.map(boxOf)).toEqual([boxOf({ filter: 'DCTDecode', ...room })]);
  });

  it('a project of three levels: the maps level by level, each titled with its path, the contents listing each with its page and a link to it, a bookmark per map nested by level', async () => {
    const pdf = await pdfOf(contentOf({ maps: WALK }));
    const pages = pdfPages(pdf);
    expect(pages.map(sizeOf)).toEqual(['A4', 'A4', 'A3', 'A3', 'A3', 'A3']);
    expect(pages.slice(2).map(titleOf)).toEqual(['VIS - Visites', 'VIS - Accueillir', 'VIS - Facturer', 'VIS - Contrôler']);
    expect(pages.slice(2).map((page) => page.texts.find((text) => text.size === 10 && text.x < 100 && Math.abs(text.y - (PDF_MARGIN + 11)) < 0.01)?.text)).toEqual(WALK.map((map) => map.path));
    const contents = pages[1];
    expect(titleOf(contents)).toBe('Sommaire');
    expect(textsOf(contents)).toEqual(expect.arrayContaining(['Visites', 'VIS-E001', 'Accueillir', 'VIS-E002', 'Facturer', 'VIS-E003', 'Contrôler', '3', '4', '5', '6']));
    expect(contents?.links).toEqual([3, 4, 5, 6]);
    // Each map set in by its level, the code of its parent step in grey before its name, its page on the right.
    const x = (text: string) => contents?.texts.find((found) => found.text === text)?.x ?? NaN;
    expect(x('Visites')).toBeCloseTo(PDF_MARGIN, 3);
    expect(x('VIS-E001')).toBeGreaterThan(x('Visites'));
    expect(x('VIS-E003')).toBeGreaterThan(x('VIS-E001'));
    expect(x('VIS-E002')).toBeCloseTo(x('VIS-E001'), 3);
    expect(contents?.texts.find((text) => text.text === 'VIS-E001')?.grey).toBeGreaterThan(0);
    expect(x('6')).toBeGreaterThan(A4.width - PDF_MARGIN - 10);
    expect(pdfOutline(pdf)).toEqual([
      { title: 'Visites', level: 0, page: 3 }, { title: 'VIS-E001 Accueillir', level: 1, page: 4 }, { title: 'VIS-E003 Contrôler', level: 2, page: 6 }, { title: 'VIS-E002 Facturer', level: 1, page: 5 },
    ]);
    expect([pdfInfo(pdf, 'Title'), pdfInfo(pdf, 'Creator')]).toEqual(['VIS - projet', PRODUCT_NAME]);
  });

});

describe('the pages chosen and the pages of every document', () => {
  it('each option adds its pages and only them: value stream, RACI when the map has a letter, sheets A4 portrait after their map, the action plan last when it has a line', async () => {
    const maps = [entry(MAIN, 'Visites'), entry(ACCUEIL, 'Visites > Accueillir')];
    const kinds = async (choice: ProjectPdfChoice, plan: Schema<'ActionPlanView'> | null = PLAN) => pdfPages(await pdfOf(contentOf({ maps, choice, plan, sheets: SHEETS }))).slice(2)
      .map((page) => `${sizeOf(page)} ${titleOf(page).replace(/ · .*$/, '')}`);
    expect(await kinds(NONE)).toEqual(['A3 VIS - Visites', 'A3 VIS - Accueillir']);
    expect(await kinds({ ...NONE, stream: true })).toEqual(['A3 VIS - Visites', 'A3 Flux de valeur', 'A3 VIS - Accueillir', 'A3 Flux de valeur']);
    // Only the main map carries a letter.
    expect(await kinds({ ...NONE, raci: true })).toEqual(['A3 VIS - Visites', 'A3 Matrice RACI', 'A3 VIS - Accueillir']);
    expect(await kinds({ ...NONE, sheets: true })).toEqual(['A3 VIS - Visites', 'A4 Fiches', 'A3 VIS - Accueillir', 'A4 Fiches']);
    expect(await kinds({ ...NONE, plan: true })).toEqual(['A3 VIS - Visites', 'A3 VIS - Accueillir', 'A3 Plan d’actions']);
    expect(await kinds({ ...NONE, plan: true }, { lines: [], open_lost_hours_per_month: 0 })).toEqual(['A3 VIS - Visites', 'A3 VIS - Accueillir']);
    expect(await kinds(ALL)).toEqual(['A3 VIS - Visites', 'A3 Flux de valeur', 'A3 Matrice RACI', 'A4 Fiches', 'A3 VIS - Accueillir', 'A3 Flux de valeur', 'A4 Fiches', 'A3 Plan d’actions']);
    // The sheet of the copy under its code and label, its state as its map shows it, its facts; the plan with its line; the plan in the contents.
    const pages = pdfPages(await pdfOf(contentOf({ maps, choice: ALL, plan: PLAN, sheets: SHEETS })));
    const sheets = pages.find((page) => titleOf(page).startsWith('Fiches'));
    expect(titleOf(sheets)).toBe('Fiches · VIS - Visites');
    expect(textsOf(sheets)).toEqual(expect.arrayContaining(['VIS-E001 Accueillir', 'Acteur', 'Gestionnaire', 'Accueillir le visiteur']));
    expect(textsOf(pages.at(-1))).toEqual(expect.arrayContaining(['IRR-1', 'Saisie en double', 'Lire l’extraction']));
    expect(textsOf(pages[1])).toContain('Plan d’actions');
    expect(pages[1]?.links.at(-1)).toBe(pages.length);
  });

  it('the maps left out below twelve levels are said on the last line of the contents, without a link', async () => {
    const [, contents] = pdfPages(await pdfOf(contentOf({ depthCut: true })));
    const lines = (contents?.texts ?? []).filter((text) => text.y > PDF_MARGIN + 12 && text.y < A4.height - PDF_MARGIN - 6).map((text) => text.text);
    expect(lines).toEqual(['Facturer', '3', 'Les cartes au-delà de 12 niveaux ne sont pas dans ce document.']);
    expect(contents?.links).toEqual([3]);
  });

});

describe('the action plan, the empty map, the title page', () => {
  it('the action plan carries only the lines of the maps in the document; with none of them, it has no page', async () => {
    const away = { ...LINE, pain_point_id: 'p2', code: 'IRR-2', description: 'Relance oubliée', map_id: 'm-loin', map: named('Archiver') };
    const maps = [entry(MAIN, 'Visites')];
    const pages = pdfPages(await pdfOf(contentOf({ maps, choice: { ...NONE, plan: true }, plan: { ...PLAN, lines: [LINE, away] } })));
    const plan = pages.at(-1);
    expect(titleOf(plan)).toBe('Plan d’actions');
    expect(textsOf(plan)).toEqual(expect.arrayContaining(['IRR-1', 'Saisie en double']));
    expect(textsOf(plan).filter((text) => ['IRR-2', 'Relance oubliée', 'Archiver'].includes(text))).toEqual([]);
    const none = pdfPages(await pdfOf(contentOf({ maps, choice: { ...NONE, plan: true }, plan: { ...PLAN, lines: [away] } })));
    expect(none.slice(2).map(titleOf)).toEqual(['VIS - Visites']);
    expect(textsOf(none[1])).not.toContain('Plan d’actions');
  });

  it('an empty map says so instead of its image', async () => {
    const empty = mapOf({ id: 'm9', name: 'Archiver', parent_step_id: 'b', lanes, steps: [] });
    const image = vi.fn(() => Promise.resolve(IMAGE));
    const pages = pdfPages(await pdfOf(contentOf({ maps: [entry(empty, 'Visites > Archiver', image)] })));
    expect(titleOf(pages[2])).toBe('VIS - Archiver');
    expect(textsOf(pages[2])).toContain('Carte sans étape.');
    expect(pages[2]?.images).toEqual([]);
    expect(image).not.toHaveBeenCalled();
  });

  it('the title page carries the name, code, classification, date and the description, or the summary of the main map without description', async () => {
    const [title] = pdfPages(await pdfOf(contentOf({ facts: { ...contentOf().facts, scope: 'Accueillir' } })));
    const body = (title?.texts ?? []).filter((text) => text.y > PDF_MARGIN + 12 && text.y < A4.height - PDF_MARGIN - 6);
    expect(body.map((text) => text.text)).toEqual(['VIS', 'Visites clients', CLASSIFICATION, '21 novembre 2026', 'Carte « Accueillir » et ses niveaux', 'Le parcours d’une visite, de la demande à la facture.']);
    expect(body.find((text) => text.text === 'Visites clients')?.face).toBe(PDF_FACES.bold);
    expect(body.find((text) => text.text === CLASSIFICATION)?.face).toBe(PDF_FACES.bold);
    for (const description of [null, '  ']) {
      const [page] = pdfPages(await pdfOf(contentOf({ facts: { ...contentOf().facts, description, summary: 'Ce que montre la carte principale.' } })));
      expect(textsOf(page)).toContain('Ce que montre la carte principale.');
      expect(textsOf(page)).not.toContain('Carte « Accueillir » et ses niveaux');
    }
  });

  it('a description longer than the title page ends on an ellipsis, its last line within the width', async () => {
    const description = 'Le parcours d’une visite, de la demande à la facture, étape par étape. '.repeat(60).trim();
    const [title] = pdfPages(await pdfOf(contentOf({ facts: { ...contentOf().facts, description } })));
    const body = (title?.texts ?? []).filter((text) => text.y > PDF_MARGIN + 12 && text.y < A4.height - PDF_MARGIN - 6).map((text) => text.text);
    const last = body.at(-1) ?? '';
    expect(last.endsWith('…')).toBe(true);
    expect(await widthIn('regular', 11, last)).toBeLessThanOrEqual(A4.width - 2 * PDF_MARGIN);
    expect(body.slice(0, -1).some((text) => text.endsWith('…'))).toBe(false);
    // What is written is the start of the description, word for word.
    const written = body.slice(4).join(' ').replace(/…$/, '').trim();
    expect(description.startsWith(written)).toBe(true);
  });

  it('every page, of either size, carries the classification at the top right and its number of the total at the foot', async () => {
    const pages = pdfPages(await pdfOf(contentOf({ maps: WALK, choice: ALL, plan: PLAN, sheets: SHEETS })));
    expect(new Set(pages.map(sizeOf))).toEqual(new Set(['A3', 'A4']));
    const { jsPDF } = await import('jspdf');
    const ruler = new jsPDF({ unit: 'mm' });
    ruler.addFileToVFS('bold.ttf', interFonts().bold);
    ruler.addFont('bold.ttf', 'bold', 'normal');
    ruler.setFont('bold', 'normal');
    ruler.setFontSize(10);
    const width = ruler.getTextWidth(CLASSIFICATION);
    pages.forEach((page, index) => {
      const top = page.texts.filter((text) => text.text === CLASSIFICATION && text.y < PDF_MARGIN + 12);
      expect(top, `page ${String(index + 1)}`).toHaveLength(1);
      expect([top[0]?.face, top[0]?.size]).toEqual([PDF_FACES.bold, 10]);
      expect(top[0]?.y).toBeCloseTo(PDF_MARGIN + 11, 3);
      expect(top[0]?.x).toBeCloseTo(page.width - PDF_MARGIN - width, 2);
      expect(top[0]?.grey).toBeGreaterThan(0);
      const foot = page.texts.filter((text) => Math.abs(text.y - (page.height - PDF_MARGIN + 2)) < 0.01).map((text) => text.text);
      expect(foot, `page ${String(index + 1)}`).toEqual([`Généré par ${PRODUCT_NAME} · VIS`, `Page ${String(index + 1)} sur ${String(pages.length)}`]);
    });
  });
});

describe('the sheets of the copy', () => {
  const copy: PmapDocument = {
    format: PMAP_FORMAT, version: '1.5', project: { name: 'Visites' },
    map: {
      lanes: [],
      steps: [
        { id: 'VIS-E001', lane: 'c1', label: 'Extraire', sheet: { outputs: [{ id: 'o1', name: 'Extraction des livres' }, { name: 'Note' }] } },
        { id: 'VIS-E002', lane: 'c1', label: 'Facturer', sheet: { uses: [{ document: 'o1' }, { document: 'o9' }] }, sub_map: {
          lanes: [], steps: [{ id: 'VIS-E003', lane: 'c1', label: 'Saisir', sheet: { outputs: [{ id: 'o9', name: 'Bordereau' }], uses: [{ document: 'o1' }] } }],
        } },
        { id: 'VIS-E004', lane: 'c1', label: 'Sans fiche' },
      ],
    },
  };

  it('are found by the code of their step at every level, each naming the documents of its own map; a document its map does not name is left out', () => {
    const sheets = copySheets(copy);
    expect([...sheets.keys()]).toEqual(['VIS-E001', 'VIS-E002', 'VIS-E003']);
    const facturer = sheets.get('VIS-E002');
    expect([facturer?.documentName('o1'), facturer?.documentName('o9')]).toEqual(['Extraction des livres', undefined]);
    const saisir = sheets.get('VIS-E003');
    expect([saisir?.documentName('o9'), saisir?.documentName('o1')]).toEqual(['Bordereau', undefined]);
    expect(sheets.get('VIS-E002')?.sheet).toBe(copy.map.steps?.[1]?.sheet);
  });
});

describe('the measure of the texts', () => {
  it('a line Inter cannot write is measured as the browser draws it: four pixels a point, rounded up, a pixel each side', () => {
    const asked: string[] = [];
    const wide = drawnWidth((text, font) => {
      asked.push(`${text}|${String(font.size)}|${String(font.weight)}`);
      return 99.4;
    });
    expect(wide('销售', 10, true)).toBeCloseTo(((100 + 2) * 25.4) / 72 / 4, 6);
    expect(asked).toEqual(['销售|40|700']);
    expect(wide('販売', 9, false)).toBeCloseTo(((100 + 2) * 25.4) / 72 / 4, 6);
    expect(asked.at(-1)).toBe('販売|36|400');
  });

  it('a Chinese sheet is cut at a character within its width, measured by the canvas measure, the Latin texts by jsPDF', async () => {
    const chinese = '销售流程的每一步都需要负责人确认之后才能继续进行下一个环节并且记录在系统里面'.repeat(6);
    const sheets = copySheets({ format: PMAP_FORMAT, version: '1.5', project: { name: 'x' }, map: { lanes: [], steps: [{ id: 'VIS-E005', lane: 'c1', label: 'x', sheet: { free_text: chinese } }] } });
    const measured: string[] = [];
    const wide = (text: string, size: number) => {
      measured.push(text);
      return Array.from(text).length * size * 0.36;
    };
    const drawn: { text: string; width: number }[] = [];
    const draw = (text: string, size: number) => {
      drawn.push({ text, width: wide(text, size) });
      return { dataUrl: PNG, width: wide(text, size), height: 5, baseline: 4 };
    };
    const pages = pdfPages(await pdfOf(contentOf({ choice: { ...NONE, sheets: true }, sheets }), { wide, draw }));
    expect(pages.map(sizeOf)).toEqual(['A4', 'A4', 'A3', 'A4']);
    const lines = drawn.filter((line) => chinese.includes(line.text));
    expect(lines.length).toBeGreaterThan(2);
    expect(lines.map((line) => line.text).join('')).toBe(chinese);
    expect(lines.every((line) => line.width <= A4.width - 2 * PDF_MARGIN)).toBe(true);
    expect(measured.every((text) => /[一-鿿]/u.test(text))).toBe(true);
    expect(textsOf(pages[3])).toContain('VIS-E005 Émettre');
  });
});

describe('the heads and the contents in Chinese and in Japanese', () => {
  const chinese = '销售流程的每一步都需要负责人确认之后才能继续进行下一个环节'.repeat(4);
  const japanese = '販売プロセスの各ステップは責任者の確認を経てから次の段階へ進みます'.repeat(4);
  const wide = (text: string, size: number) => Array.from(text).length * size * 0.36;

  it('are cut by the measure of the canvas, an ellipsis at their end, never on the page number nor under the classification', async () => {
    const drawn: string[] = [];
    const draw = (text: string, size: number) => {
      drawn.push(text);
      return { dataUrl: PNG, width: wide(text, size), height: 5, baseline: 4 };
    };
    const zh = entry(mapOf({ ...FACTURE, name: chinese }), `${chinese} > ${chinese}`);
    const ja = entry(mapOf({ id: 'm8', name: japanese, lanes, steps: [stepOf('j', 'l1', 0, { code: 'VIS-E008', label: 'x' })] }), japanese);
    const sheets = copySheets({ format: PMAP_FORMAT, version: '1.5', project: { name: 'x' }, map: { lanes: [], steps: [{ id: 'VIS-E005', lane: 'c1', label: 'x', sheet: { purpose: 'Émettre la facture' } }] } });
    const pages = pdfPages(await pdfOf(contentOf({ maps: [zh, ja], choice: { ...NONE, sheets: true }, sheets }), { wide, draw }));
    expect(pages.map(sizeOf)).toEqual(['A4', 'A4', 'A3', 'A4', 'A3']);
    // The contents: each line drawn ends before the number of its page.
    const contents = pages[1];
    const numbers = (contents?.texts ?? []).filter((text) => /^\d+$/.test(text.text));
    expect(numbers.map((text) => text.text)).toEqual(['3', '5']);
    const lines = contents?.images ?? [];
    expect(lines).toHaveLength(2);
    lines.forEach((line, rank) => {
      expect(line.x + line.width).toBeLessThanOrEqual((numbers[rank]?.x ?? 0) + 0.001);
    });
    // The heads of the maps and of the sheets: the title and the path end before the classification.
    const classification = await widthIn('bold', 10, CLASSIFICATION);
    for (const page of [pages[2], pages[3], pages[4]]) {
      const heads = (page?.images ?? []).filter((image) => image.y < PDF_MARGIN + 12);
      expect(heads).toHaveLength(2);
      for (const head of heads) expect(head.x + head.width).toBeLessThanOrEqual((page?.width ?? 0) - PDF_MARGIN - classification + 0.001);
    }
    for (const start of [chinese.slice(0, 6), `Fiches · VIS - ${chinese.slice(0, 6)}`, `VIS - ${chinese.slice(0, 6)}`, japanese.slice(0, 6), `VIS - ${japanese.slice(0, 6)}`]) {
      expect(drawn.some((text) => text.startsWith(start) && text.endsWith('…')), start).toBe(true);
    }
  });
});

describe('the bounds, the progress and the abandon', () => {
  it('past 1000 pages the document is refused and no file is made', async () => {
    const long = Array.from({ length: 60_000 }, (_, index) => `ligne ${String(index)}`).join('\n');
    const sheets = copySheets({ format: PMAP_FORMAT, version: '1.5', project: { name: 'x' }, map: { lanes: [], steps: [{ id: 'VIS-E005', lane: 'c1', label: 'x', sheet: { free_text: long } }] } });
    const tools: ProjectPdfTools = { fonts: interFonts(), t, language: 'fr', shown: 'fr' };
    const refused = buildProjectPdf(contentOf({ choice: { ...NONE, sheets: true }, sheets }), tools);
    await expect(refused).rejects.toBeInstanceOf(ProjectPdfRefusal);
    await expect(refused).rejects.toMatchObject({ reason: 'pages' });
    expect(PROJECT_PDF_LIMITS).toEqual({ maps: 100, levels: 12, pages: 1000 });
    // Without the sheets the same project is written.
    await expect(buildProjectPdf(contentOf({ sheets }), tools)).resolves.toBeInstanceOf(Blob);
  });

  it('past 100 maps nothing is drawn', async () => {
    const image = vi.fn(() => Promise.resolve(IMAGE));
    const many = Array.from({ length: 101 }, (_, index) => entry(mapOf({ id: `x${String(index)}`, name: `Carte ${String(index)}`, lanes, steps: [stepOf(`s${String(index)}`, 'l1', 0)] }), 'x', image));
    const tools: ProjectPdfTools = { fonts: interFonts(), t, language: 'fr', shown: 'fr' };
    await expect(buildProjectPdf(contentOf({ maps: many }), tools)).rejects.toMatchObject({ reason: 'maps' });
    expect(image).not.toHaveBeenCalled();
  });

  it('says the map drawn of the total and the page written; aborted, it draws nothing more and makes no file', async () => {
    const progress: ProjectPdfProgress[] = [];
    await pdfOf(contentOf({ maps: WALK }), { progress: (step) => progress.push(step) });
    expect(progress.filter((step) => step.kind === 'drawing')).toEqual([1, 2, 3, 4].map((index) => ({ kind: 'drawing', index, total: 4 })));
    expect(progress.flatMap((step) => (step.kind === 'writing' ? [step.page] : []))).toEqual([1, 2, 3, 4, 5, 6]);
    const controller = new AbortController();
    const image = vi.fn(() => {
      controller.abort();
      return Promise.resolve(IMAGE);
    });
    const tools: ProjectPdfTools = { fonts: interFonts(), t, language: 'fr', shown: 'fr', signal: controller.signal };
    await expect(buildProjectPdf(contentOf({ maps: WALK.map((map) => ({ ...map, image })) }), tools)).rejects.toMatchObject({ name: 'AbortError' });
    expect(image).toHaveBeenCalledTimes(1);
  });
});
