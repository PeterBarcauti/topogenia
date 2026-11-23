// projectPdf.ts
// The PDF of the project (ADR 113): a title page and the contents A4 portrait, each map A3 landscape level by level under its path, then as chosen its value stream, its RACI matrix, its sheets A4 portrait, the action plan of its maps last; contents linked and bookmarks nested, filled once every page is laid; heads and lines cut by the measure of the lines as written; the classification and the number put on every page by one last pass; bounds of 100 maps and 1000 pages (the 12 levels held by the walk).

import type { TFunction } from 'i18next';
import type { jsPDF as JsPdf } from 'jspdf';
import type { Schema } from '../../../../api/client.ts';
import { PRODUCT_NAME } from '../../../../shared/i18n/i18n.ts';
import type { PmapDocument, PmapMap, PmapSheet } from '../../../../shared/pmap.ts';
import { actionPlanSheet } from '../actionPlanSheet.ts';
import { laneName, stepLabel } from '../../model/mapLayout.ts';
import { canvasMeasure, type TextMeasure } from '../../model/textSize.ts';
import { planEntries } from '../../plan/actionPlan.ts';
import {
  beyondInter, cutTo, ellipsized, GREY, PDF_FACES, PDF_MARGIN, registerFonts, writeMapPage, writerOf, writeStreamPages,
  type LineDrawer, type MapImage, type PdfFonts, type Write,
} from '../pdf.ts';
import { streamPdfOf } from '../streamPdf.ts';
import { flowSheets, FLOW_TOP, wrapText, type FlowSheet, type Measure } from './pageFlow.ts';
import { sheetBlocks } from './sheetBlocks.ts';
import { planTable, raciTable, tablePages, type TableData } from './tablePages.ts';

type MapView = Schema<'MapView'>;
type ActionPlanView = Schema<'ActionPlanView'>;

/** The bounds of the document: the maps walked, the levels under the map it starts at (that one the first, held by the walk), the pages written. */
export const PROJECT_PDF_LIMITS = { maps: 100, levels: 12, pages: 1000 } as const;

/** The document refused by a bound: nothing is drawn past the maps, no file is made past the pages. */
export class ProjectPdfRefusal extends Error {
  constructor(readonly reason: 'maps' | 'pages') {
    super(`The project is too large for one PDF: ${reason}.`);
    this.name = 'ProjectPdfRefusal';
  }
}

/** A sheet of the copy and the names of the documents of its own map, by their identifier in the copy. */
export interface CopySheet {
  readonly sheet: PmapSheet;
  readonly documentName: (id: string) => string | undefined;
}

/**
 * The sheets of the copy read for the PDF, by the code of their step (the identifier of a step in the copy), at every level;
 * a document used is named by the outputs of the steps of its own map, the only ones a step may use, so that a document its
 * map does not name is left out.
 */
export function copySheets(copy: PmapDocument): ReadonlyMap<string, CopySheet> {
  const sheets = new Map<string, CopySheet>();
  const visit = (map: PmapMap): void => {
    const steps = map.steps ?? [];
    const names = new Map(steps.flatMap((step) => step.sheet?.outputs ?? []).flatMap((output) => (output.id === undefined ? [] : [[output.id, output.name] as const])));
    const documentName = (id: string) => names.get(id);
    for (const step of steps) {
      if (step.sheet !== undefined) sheets.set(step.id, { sheet: step.sheet, documentName });
      if (step.sub_map !== undefined) visit(step.sub_map);
    }
  };
  visit(copy.map);
  return sheets;
}

/** The width in millimetres of a line Inter cannot write, drawn as an image, in a size in points, bold or not. */
export type WideMeasure = (text: string, size: number, bold: boolean) => number;

/** Millimetres in a point; the pixels of a point and the pixel left each side when a line is drawn as an image (`drawLine`). */
const MM_PER_POINT = 25.4 / 72;
const DRAWN_PIXELS_PER_POINT = 4;
const DRAWN_SIDES = 2;

/** The width of a line drawn as an image, from the measure of the canvas: as wide as the picture `drawLine` makes of it. */
export function drawnWidth(measure: TextMeasure): WideMeasure {
  return (text, size, bold) => {
    const pixels = Math.ceil(measure(text, { size: size * DRAWN_PIXELS_PER_POINT, weight: bold ? 700 : 400 })) + DRAWN_SIDES;
    return (pixels * MM_PER_POINT) / DRAWN_PIXELS_PER_POINT;
  };
}

/** The measure of the lines as they are written: by jsPDF in Inter, by the canvas for a line drawn as an image (Chinese, Japanese). */
function pdfMeasure(doc: JsPdf, wide: WideMeasure): Measure {
  return (text, face, size) => {
    if (beyondInter(text)) return wide(text, size, face === 'bold');
    doc.setFont(PDF_FACES[face], 'normal');
    doc.setFontSize(size);
    return doc.getTextWidth(text);
  };
}

/** The facts of the title page, in the language shown: the date of the day alone, the description or else the summary of the main map. */
export interface ProjectPdfFacts {
  readonly name: string;
  readonly code: string;
  /** « Classification : Interne », printed on every page. */
  readonly classification: string;
  readonly date: string;
  readonly description: string | null;
  readonly summary: string | null;
  /** The name of the map the document starts at, when it is not the whole project. */
  readonly scope: string | null;
}

/** The four boxes of the dialog. */
export interface ProjectPdfChoice {
  readonly sheets: boolean;
  readonly raci: boolean;
  readonly stream: boolean;
  readonly plan: boolean;
}

/** A map of the walk: its view, its name, « CODE - name », its path joined by « > », its image drawn when its page is written. */
export interface ProjectPdfMap {
  readonly view: MapView;
  readonly name: string;
  readonly title: string;
  readonly path: string;
  readonly image: () => Promise<MapImage>;
}

/** What the document holds: the maps in the order of the walk (level by level), the sheets of the copy, the plan read, the levels left out. */
export interface ProjectPdfContent {
  readonly facts: ProjectPdfFacts;
  readonly choice: ProjectPdfChoice;
  readonly maps: readonly ProjectPdfMap[];
  readonly sheets: ReadonlyMap<string, CopySheet>;
  readonly plan: ActionPlanView | null;
  /** Maps below the twelve levels were left out by the walk: the contents says so. */
  readonly depthCut: boolean;
}

/** Where the writing is: a map drawn of the total, a page written. */
export type ProjectPdfProgress = { readonly kind: 'drawing'; readonly index: number; readonly total: number } | { readonly kind: 'writing'; readonly page: number };

/** What the document is written with: the faces, the texts, the languages of the interface and of the maps, the drawer and the measure of a line Inter lacks, the abandon, the progress. */
export interface ProjectPdfTools {
  readonly fonts: PdfFonts;
  /** The texts of the interface, as the sheet and its durations read them. */
  readonly t: TFunction;
  readonly language: string;
  readonly shown: string;
  readonly draw?: LineDrawer;
  readonly wide?: WideMeasure;
  readonly signal?: AbortSignal;
  readonly progress?: (step: ProjectPdfProgress) => void;
}

/** A map placed in the document: its level under the first map, the code of its parent step, its bookmark parent, its page. */
interface Placed {
  readonly map: ProjectPdfMap;
  readonly level: number;
  readonly parent: number | null;
  readonly parentCode: string | null;
  page: number;
}

/** Each map with its level and its parent among the maps before it: the map holding the step it details. */
function placedMaps(maps: readonly ProjectPdfMap[]): Placed[] {
  const placed: Placed[] = [];
  for (const map of maps) {
    const parent = placed.findIndex((candidate) => candidate.map.view.steps.some((step) => step.id === map.view.parent_step_id));
    const above = placed[parent];
    const code = above?.map.view.steps.find((step) => step.id === map.view.parent_step_id)?.code ?? null;
    placed.push({ map, level: above === undefined ? 0 : above.level + 1, parent: parent < 0 ? null : parent, parentCode: code, page: 0 });
  }
  return placed;
}

/** The writer of the document: its page added and counted, the bound held, the abandon heard, the progress said. */
interface Pen {
  readonly doc: JsPdf;
  readonly write: Write;
  readonly measure: Measure;
  readonly tools: ProjectPdfTools;
  readonly facts: ProjectPdfFacts;
  /** Adds a page of the format given, refused past the bound. */
  readonly add: (format: 'a3' | 'a4') => void;
  /** Refuses now pages that would pass the bound. */
  readonly reserve: (pages: number) => void;
  /** Says the pages written since the last time. */
  readonly counted: () => void;
}

const pageSize = (doc: JsPdf) => ({ width: doc.internal.pageSize.getWidth(), height: doc.internal.pageSize.getHeight() });

function penOf(doc: JsPdf, tools: ProjectPdfTools, facts: ProjectPdfFacts): Pen {
  const raw = writerOf(doc, tools.draw);
  // A text left empty (the date, the classification, the footer the pass writes) is not written at all.
  const write: Write = (text, x, y, line) => {
    if (text !== '') raw(text, x, y, line);
  };
  let said = 0;
  const counted = () => {
    for (let page = said + 1; page <= doc.getNumberOfPages(); page++) tools.progress?.({ kind: 'writing', page });
    said = doc.getNumberOfPages();
  };
  const reserve = (pages: number) => {
    tools.signal?.throwIfAborted();
    if (doc.getNumberOfPages() + pages > PROJECT_PDF_LIMITS.pages) throw new ProjectPdfRefusal('pages');
  };
  const add = (format: 'a3' | 'a4') => {
    reserve(1);
    doc.addPage(format, format === 'a3' ? 'landscape' : 'portrait');
    counted();
  };
  return { doc, write, measure: pdfMeasure(doc, tools.wide ?? drawnWidth(canvasMeasure())), tools, facts, add, reserve, counted };
}

/** The room the classification takes on the right of the head of a page, so that a title or a path does not run under it. */
function classificationRoom(pen: Pen): number {
  return pen.measure(pen.facts.classification, 'bold', 10) + 6;
}

/** A text cut to a width by the measure of the lines as written, in Inter or drawn as an image, an ellipsis at its end. */
const cutLine = (pen: Pen, text: string, width: number, face: 'regular' | 'bold', size: number): string => cutTo(text, width, (line) => pen.measure(line, face, size));

/** The title of a page in bold 16 and the line under it in regular 10, both cut before the classification. */
function headTexts(pen: Pen, title: string, subtitle: string): { readonly title: string; readonly subtitle: string } {
  const room = pageSize(pen.doc).width - 2 * PDF_MARGIN - classificationRoom(pen);
  return { title: cutLine(pen, title, room, 'bold', 16), subtitle: cutLine(pen, subtitle, room, 'regular', 10) };
}

/** The head of a page of the document: its title in bold, the line under it in grey, both cut before the classification. */
function writeHead(pen: Pen, title: string, subtitle: string): void {
  const { doc, write } = pen;
  const head = headTexts(pen, title, subtitle);
  doc.setTextColor(0);
  write(head.title, PDF_MARGIN, PDF_MARGIN + 4, { face: 'bold', size: 16, grey: false });
  doc.setTextColor(GREY);
  write(head.subtitle, PDF_MARGIN, PDF_MARGIN + 11, { face: 'regular', size: 10, grey: true });
}

/** The title page, the first: the code, the name, the classification, the date, the scope, the description or the summary, its last line ending on an ellipsis when the page cuts it. */
function writeTitlePage(pen: Pen): void {
  const { doc, write, measure, facts, tools } = pen;
  const { width, height } = pageSize(doc);
  const room = width - 2 * PDF_MARGIN;
  const bottom = height - PDF_MARGIN - 6;
  let y = 70;
  const lines = (text: string, face: 'regular' | 'bold', size: number, grey: boolean, advance: number) => {
    const wrapped = wrapText(text, room, measure, face, size);
    for (const [index, line] of wrapped.entries()) {
      if (y > bottom) return;
      const cut = y + advance > bottom && index < wrapped.length - 1;
      doc.setTextColor(grey ? GREY : 0);
      write(cut ? ellipsized(line, room, (part) => measure(part, face, size)) : line, PDF_MARGIN, y, { face, size, grey });
      y += advance;
    }
  };
  lines(facts.code, 'regular', 12, true, 12);
  lines(facts.name, 'bold', 24, false, 10);
  y += 4;
  lines(facts.classification, 'bold', 12, false, 7);
  lines(facts.date, 'regular', 11, true, 7);
  if (facts.scope !== null) lines(tools.t('project_pdf.scope_line', { name: facts.scope }), 'regular', 11, true, 7);
  y += 8;
  const description = (facts.description ?? '').trim();
  const about = description === '' ? (facts.summary ?? '').trim() : description;
  if (about !== '') lines(about, 'regular', 11, false, 5.5);
}

/** The height of a line of the contents, in millimetres, and the indent of a level. */
const CONTENTS_LINE = 6;
const CONTENTS_INDENT = 6;

/** The lines of the contents a page holds, from the top of the lines to the room of the footer. */
const contentsPerPage = (height: number): number => Math.floor((height - PDF_MARGIN - 6 - FLOW_TOP - CONTENTS_LINE) / CONTENTS_LINE);

/** A line of the contents: its level, the code of the parent step, its text, the page it goes to, none for a line said only. */
interface ContentsLine {
  readonly level: number;
  readonly code: string | null;
  readonly text: string;
  readonly page: number | null;
}

/** The contents on the pages reserved for it, from the second: a line a map set in by its level, its page on the right and a link to it. */
function writeContents(pen: Pen, lines: readonly ContentsLine[], first: number, pages: number): void {
  const { doc, write, measure, tools } = pen;
  for (let rank = 0; rank < pages; rank++) {
    doc.setPage(first + rank);
    const { width, height } = pageSize(doc);
    writeHead(pen, tools.t('project_pdf.contents'), '');
    const perPage = contentsPerPage(height);
    lines.slice(rank * perPage, (rank + 1) * perPage).forEach((line, index) => {
      const y = FLOW_TOP + CONTENTS_LINE * (index + 1);
      const right = width - PDF_MARGIN;
      let x = PDF_MARGIN + line.level * CONTENTS_INDENT;
      if (line.code !== null) {
        doc.setTextColor(GREY);
        write(line.code, x, y, { face: 'regular', size: 10, grey: true });
        x += measure(line.code, 'regular', 10) + 2;
      }
      const number = line.page === null ? '' : String(line.page);
      doc.setTextColor(line.page === null ? GREY : 0);
      write(cutLine(pen, line.text, right - x - measure(number, 'regular', 10) - 4, 'regular', 10), x, y, { face: 'regular', size: 10, grey: line.page === null });
      if (line.page === null) return;
      write(number, right, y, { face: 'regular', size: 10, grey: false, right: true });
      doc.link(PDF_MARGIN, y - CONTENTS_LINE + 1.5, width - 2 * PDF_MARGIN, CONTENTS_LINE, { pageNumber: line.page });
    });
  }
}

/** The pages of a table on pages A3 landscape, its head on each: the texts of its cells and a thin rule under its header and each row. */
function writeTable(pen: Pen, data: TableData, title: string, subtitle: string): number {
  const { doc, write, measure, tools } = pen;
  pen.add('a3');
  const first = doc.getNumberOfPages();
  const page = pageSize(doc);
  const laid = tablePages(data, { page, measure, language: tools.language });
  pen.reserve(laid.length - 1);
  laid.forEach((table, index) => {
    if (index > 0) pen.add('a3');
    writeHead(pen, title, subtitle);
    doc.setTextColor(0);
    for (const cell of table.cells) write(cell.text, cell.x, cell.y, { face: cell.face, size: cell.size, grey: false });
    doc.setDrawColor(GREY);
    doc.setLineWidth(0.2);
    for (const rule of table.rules) doc.line(PDF_MARGIN, rule, page.width - PDF_MARGIN, rule);
  });
  return first;
}

/** The sheets of a map in the reading order of its steps: those the copy gives, with something in them, under their code and label, their state as the map shows it. */
function mapSheets(pen: Pen, view: MapView, sheets: ReadonlyMap<string, CopySheet>): FlowSheet[] {
  const { t, language, shown } = pen.tools;
  return view.steps.flatMap((step) => {
    const found = sheets.get(step.code);
    if (found === undefined) return [];
    const blocks = sheetBlocks({ sheet: found.sheet, status: step.sheet.status, documentName: found.documentName }, t, language);
    const title = `${step.code} ${stepLabel(step, shown)}`;
    return blocks.length === 0 ? [] : [{ title, continued: t('project_pdf.continued', { title }), blocks }];
  });
}

/** The sheets of a map on pages A4 portrait after it, their head saying whose they are. */
function writeSheets(pen: Pen, map: ProjectPdfMap, sheets: readonly FlowSheet[]): void {
  const { doc, write, measure, tools } = pen;
  pen.add('a4');
  const pages = flowSheets(sheets, { page: pageSize(doc), measure });
  pen.reserve(pages.length - 1);
  pages.forEach((lines, index) => {
    if (index > 0) pen.add('a4');
    writeHead(pen, tools.t('project_pdf.sheets_of', { map: map.title }), map.path);
    for (const line of lines) {
      doc.setTextColor(line.grey ? GREY : 0);
      write(line.text, line.x, line.y, { face: line.face, size: line.size, grey: line.grey });
    }
  });
}

/** The page of a map, A3 landscape: its image under its title and path, or « Map without steps. » for an empty map, whose image is not asked. */
async function writeMap(pen: Pen, placed: Placed): Promise<void> {
  const { doc, write, tools } = pen;
  pen.add('a3');
  placed.page = doc.getNumberOfPages();
  const { map } = placed;
  if (map.view.steps.length === 0) {
    writeHead(pen, map.title, map.path);
    doc.setTextColor(GREY);
    write(tools.t('project_pdf.empty_map'), PDF_MARGIN, FLOW_TOP + 10, { face: 'regular', size: 12, grey: true });
    return;
  }
  const image = await map.image();
  tools.signal?.throwIfAborted();
  const head = headTexts(pen, map.title, map.path);
  doc.setTextColor(0);
  // The date, the classification and the footer are left to the last pass, the same on every page.
  writeMapPage(doc, write, image, { ...head, date: '', classification: '', footer: '' });
}

/** A map and the pages chosen after it: its value stream, its RACI matrix when it carries a letter, its sheets. */
async function writeMapPart(pen: Pen, placed: Placed, content: ProjectPdfContent): Promise<void> {
  const { doc, write, tools } = pen;
  const { view, title, path } = placed.map;
  await writeMap(pen, placed);
  if (content.choice.stream) {
    writeStreamPages(doc, write, streamPdfOf(view, tools.shown, title, tools.language, tools.t), '');
    // Its pages are added by the writer of the value stream: counted once written, a few at most.
    pen.reserve(0);
    pen.counted();
  }
  if (content.choice.raci && view.raci.length > 0) {
    const table = raciTable(view, (lane) => laneName(lane, tools.shown), (step) => stepLabel(step, tools.shown), { code: tools.t('raci.column_code'), step: tools.t('raci.column_step') });
    writeTable(pen, table, `${tools.t('export.sheet_raci')} · ${title}`, path);
  }
  if (!content.choice.sheets) return;
  const sheets = mapSheets(pen, view, content.sheets);
  if (sheets.length > 0) writeSheets(pen, placed.map, sheets);
}

/** The plan of the maps in the document: the host gives the lines of every level below the map it starts at, those past the walk left out. */
function documentPlan(plan: ActionPlanView | null, placed: readonly Placed[]): ActionPlanView | null {
  if (plan === null) return null;
  const maps = new Set(placed.map((map) => map.map.view.id));
  return { ...plan, lines: plan.lines.filter((line) => maps.has(line.map_id)) };
}

/** The action plan last, A3 landscape, when it has a line: the eleven cells of its entries in the order of the host. */
function writePlan(pen: Pen, plan: ActionPlanView): number | null {
  const { t, shown } = pen.tools;
  const sheet = actionPlanSheet(plan, shown, t);
  if (sheet === null) return null;
  return writeTable(pen, planTable(planEntries(plan, shown, t), sheet.headers), t('export.sheet_action_plan'), '');
}

/**
 * The last pass, on every page of either size, the only place they are written: the classification at the top right in
 * bold grey, where the PDF of a map has it; at the foot « Generated by », the product and the code on the left, « Page n of N » on the right.
 */
function stampPages(pen: Pen): void {
  const { doc, write, facts, tools } = pen;
  const total = doc.getNumberOfPages();
  const footer = `${tools.t('export.footer', { name: PRODUCT_NAME })} · ${facts.code}`;
  for (let page = 1; page <= total; page++) {
    doc.setPage(page);
    const { width, height } = pageSize(doc);
    doc.setTextColor(GREY);
    write(facts.classification, width - PDF_MARGIN, PDF_MARGIN + 11, { face: 'bold', size: 10, grey: true, right: true });
    write(footer, PDF_MARGIN, height - PDF_MARGIN + 2, { face: 'regular', size: 8, grey: true });
    write(tools.t('project_pdf.page', { page, total }), width - PDF_MARGIN, height - PDF_MARGIN + 2, { face: 'regular', size: 8, grey: true, right: true });
  }
}

/** The bookmarks: one a map, nested under the map whose step it details, then the action plan. */
function writeBookmarks(doc: JsPdf, placed: readonly Placed[], plan: { readonly title: string; readonly page: number } | null): void {
  const items: unknown[] = [];
  for (const map of placed) {
    const parent = map.parent === null ? null : items[map.parent];
    items.push(doc.outline.add(parent, map.parentCode === null ? map.map.name : `${map.parentCode} ${map.map.name}`, { pageNumber: map.page }));
  }
  if (plan !== null) doc.outline.add(null, plan.title, { pageNumber: plan.page });
}

/** The bound of the maps the walk keeps, held again before anything is drawn; the levels are the walk's alone (it reads none below the twelfth). */
function holdBounds(placed: readonly Placed[]): void {
  if (placed.length > PROJECT_PDF_LIMITS.maps) throw new ProjectPdfRefusal('maps');
}

/**
 * The PDF of the project, built by jsPDF loaded on demand (B-0-04), in Inter (AR-52), a line whose letters Inter lacks drawn
 * by the drawer given (ADR 083): the title page, the contents reserved then filled, each map and its pages in the order of
 * the walk, its image asked one at a time, the plan last, the bookmarks, the last pass on every page. The abandon is heard
 * between two maps and two pages; past a bound the document is refused and no file is made.
 */
export async function buildProjectPdf(content: ProjectPdfContent, tools: ProjectPdfTools): Promise<Blob> {
  const placed = placedMaps(content.maps);
  holdBounds(placed);
  const { jsPDF } = await import('jspdf');
  const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
  registerFonts(doc, tools.fonts);
  const { t } = tools;
  const pen = penOf(doc, tools, content.facts);
  pen.counted();
  writeTitlePage(pen);
  const plan = content.choice.plan ? documentPlan(content.plan, placed) : null;
  const hasPlan = plan !== null && plan.lines.length > 0;
  const lines = placed.length + (hasPlan ? 1 : 0) + (content.depthCut ? 1 : 0);
  // The contents is reserved before the maps, its number of pages known from its lines and the size of its first page.
  pen.add('a4');
  const reserved = Math.max(1, Math.ceil(lines / contentsPerPage(pageSize(doc).height)));
  for (let page = 1; page < reserved; page++) pen.add('a4');
  for (const [index, map] of placed.entries()) {
    tools.signal?.throwIfAborted();
    tools.progress?.({ kind: 'drawing', index: index + 1, total: placed.length });
    await writeMapPart(pen, map, content);
  }
  const planPage = hasPlan ? writePlan(pen, plan) : null;
  const contents: ContentsLine[] = [
    ...placed.map((map) => ({ level: map.level, code: map.parentCode, text: map.map.name, page: map.page })),
    ...(planPage === null ? [] : [{ level: 0, code: null, text: t('export.sheet_action_plan'), page: planPage }]),
    ...(content.depthCut ? [{ level: 0, code: null, text: t('project_pdf.depth_cut', { max: PROJECT_PDF_LIMITS.levels }), page: null }] : []),
  ];
  writeContents(pen, contents, 2, reserved);
  writeBookmarks(doc, placed, planPage === null ? null : { title: t('export.sheet_action_plan'), page: planPage });
  stampPages(pen);
  doc.setProperties({ title: `${content.facts.code} - ${t('project_pdf.file')}`, creator: PRODUCT_NAME });
  tools.signal?.throwIfAborted();
  return doc.output('blob');
}
