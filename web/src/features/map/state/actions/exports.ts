// exports.ts
// The exports of the map: its image saved as a PNG or placed in a PDF A3 (the pages of the value stream after it, its layer on), its image SVG redrawn from its layout with its copy on option and the marks of the layer « Analysis » with the layer on, its poster for a wall cut into pages A4 or A3 drawn from that image (Makigami M9), the workbook of its steps, links, RACI matrix and action plan, the Markdown with its Mermaid diagram and the same tables; the image PNG of the map or of a selection, for its copy; the project read for its classification; the notifications; nothing written, nothing to undo (annex B 2.14, B-2.6-45, S-4.1-08, ADR 066, ADR 109, plan 3.1 L3).

import { api, type Schema } from '../../../../api/client.ts';
import { crumbName } from '../../../../app/EditorBar.tsx';
import { download, exportFileName } from '../../../../shared/download.ts';
import { i18next, PRODUCT_NAME, type Translate } from '../../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../../shared/i18n/localizedName.ts';
import { EXPORT_TOAST_KEY } from '../../../../shared/ui/toasts.ts';
import { copyMetadata } from '../../../project/files/svgCopy.ts';
import { captureView, fontFaces, pngOf, waitForSteps, type Captured } from '../../export/capture.ts';
import { buildPdf, type PdfHeader, type DrawnLine } from '../../export/pdf.ts';
import { pdfFonts } from '../../export/pdfFonts.ts';
import { streamPdfOf } from '../../export/streamPdf.ts';
import { mapMarkdown, MARKDOWN_TYPE } from '../../export/markdown.ts';
import { reportMarkdown } from '../../export/report.ts';
import type { MermaidLook } from '../../export/mermaid.ts';
import { mapSvg, svgSize, type SvgAnalysis, type SvgLook } from '../../export/svg.ts';
import { svgIcons } from '../../export/svgIcons.ts';
import { actionPlanSheet } from '../../export/actionPlanSheet.ts';
import { buildWallPdf, windowSvg, type WindowPainter } from '../../export/wallPdf.ts';
import { wallPlan, type Paper, type Size } from '../../export/wallPrint.ts';
import { argbOf, buildMapWorkbook, workbookSheets, type Sheet, type WorkbookLook } from '../../export/workbook.ts';
import { analysisMarksOf } from '../../model/analysis.ts';
import { layoutMap, type MapLayout } from '../../model/mapLayout.ts';
import type { RaciLetter } from '../../model/raci.ts';
import { canvasMeasure, type TextMeasure } from '../../model/textSize.ts';
import type { LaneRows } from '../../model/gridShape.ts';
import type { MapEditor } from '../editor.ts';
import type { DisplayPreferences } from '../preferences.ts';
import { shapeOf } from '../gridShape.ts';
import type { ExportFormat } from '../mapStore.ts';

type MapView = Schema<'MapView'>;

const t: Translate = (key, options) => i18next.t(key, options);

/** The value of a token of the charter on the page at the moment of the export (spec 9.2: no colour is written in the code). */
const token = (name: string): string => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

const LETTERS: readonly RaciLetter[] = ['R', 'A', 'S', 'C', 'I'];

/** The colours of the workbook: the headers of the tables and the grounds of the RACI letters, as the matrix of the map shows them (B-2.14-25, B-2.14-26). */
function workbookLook(): WorkbookLook {
  const letters = LETTERS.flatMap((letter) => {
    const argb = argbOf(token(`--raci-${letter.toLowerCase()}-bg`));
    return argb === null ? [] : [[letter, argb] as const];
  });
  return { header: argbOf(token('--table-header-bg')), letters: Object.fromEntries(letters) };
}

/** The colours of a step with a problem in a Mermaid diagram, those of a fault in the charter; none when a token is missing. */
function mermaidLook(): MermaidLook {
  const [fill, stroke] = [token('--danger-soft'), token('--danger')];
  return { problem: fill === '' || stroke === '' ? null : { fill, stroke } };
}

/** The colours of the image SVG, those of the canvas read from the charter at the export (spec 9.2). */
export function svgLook(): SvgLook {
  const tints = [1, 2, 3, 4, 5, 6, 7, 8];
  return {
    surface: token('--surface'), laneOdd: token('--lane-odd'), laneEven: token('--lane-even'), laneSeparator: token('--lane-separator'), border: token('--border'),
    text: token('--text'), textMuted: token('--text-muted'), accentStrong: token('--accent-strong'), edge: token('--edge'), frameLabelBg: token('--frame-label-bg'),
    dangerMuted: token('--danger-muted'), warningSoft: token('--warning-soft'), warningText: token('--warning-text'), warningGraphic: token('--warning-graphic'),
    raci: { A: token('--raci-a-bg'), S: token('--raci-s-bg'), C: token('--raci-c-bg'), I: token('--raci-i-bg') },
    pastels: tints.map((tint) => token(`--lane-${String(tint)}-pastel`)), inks: tints.map((tint) => token(`--lane-${String(tint)}-ink`)),
  };
}

/** The colours of the marks of the layer « Analysis », those of the canvas read from the charter at the export (plan 3.1). */
export function analysisLook(): SvgAnalysis['look'] {
  return {
    handoff: token('--analysis-handoff'), badgeBg: token('--analysis-badge-bg'), badgeFg: token('--analysis-badge-fg'), mediaBreak: token('--analysis-break'),
    classes: { value_added: token('--analysis-value-added'), necessary: token('--analysis-necessary'), waste: token('--analysis-waste') },
  };
}

/** The preferences of the person that the layout and the drawing of a map read. */
export type SvgPreferences = Pick<DisplayPreferences, 'showCodes' | 'alwaysShowPictos' | 'showRaci' | 'showValueStream'>;

/**
 * What the image SVG of a map reads of the person's view, given instead of an editor: their preferences on the layers, the
 * codes and the pictograms, and the empty rows the lanes show (none for a map not shown). The PDF of a project draws maps no
 * editor shows.
 */
export interface SvgSetup {
  readonly preferences: SvgPreferences;
  readonly rows: LaneRows;
}

/** The setup of the map the editor shows: its preferences and the empty rows its lanes show (ADR 096). */
export function setupOf(editor: MapEditor): SvgSetup {
  const { showCodes, alwaysShowPictos, showRaci, showValueStream } = editor.preferences.getState();
  return { preferences: { showCodes, alwaysShowPictos, showRaci, showValueStream }, rows: shapeOf(editor).rows };
}

/** The layout of the map as the canvas makes it, with the preferences of the person on the value stream and the empty rows the lanes show (ADR 096). */
function layoutFor(setup: SvgSetup, map: MapView, shown: string, measure: TextMeasure): MapLayout {
  const stream = setup.preferences.showValueStream ? { language: i18next.language, workdayHours: map.value_stream.work_time.workday_hours, t } : null;
  return layoutMap(map, shown, measure, stream, setup.rows);
}

/** The size of the image SVG of the map, in its units: what its poster is cut from (Makigami M9). */
export function wallSize(editor: MapEditor, map: MapView): Size {
  const shown = editor.store.getState().shown?.language ?? i18next.language;
  return svgSize(layoutFor(setupOf(editor), map, shown, canvasMeasure()));
}

/**
 * The text of the image SVG of the map (plan 3.1, L7) and its size, from the map and a setup, with no editor: its layout made again as the canvas makes it, the
 * fonts of the page loaded first so that the texts are measured in Inter, the preferences of the person on the codes,
 * the pictograms, the RACI and the value stream, the marks of the layer « Analysis » when asked; the copy of the map and
 * of the maps below it read from the host when asked, put in its metadata.
 */
export async function svgText(map: MapView, shown: string, facts: { title: string; caption: string; editable: boolean; analysis: boolean }, setup: SvgSetup): Promise<{ text: string; size: Size }> {
  // A page without its set of fonts (a test) measures with the estimate.
  if ('fonts' in document) await document.fonts.ready;
  const measure = canvasMeasure();
  const { preferences } = setup;
  const layout = layoutFor(setup, map, shown, measure);
  const [icons, fonts, copy] = await Promise.all([
    svgIcons(), fontFaces().catch(() => ''), facts.editable ? api.get('/api/maps/{id}/pmap', { path: { id: map.id } }) : Promise.resolve(null),
  ]);
  const text = mapSvg(layout, {
    look: svgLook(), t, measure, icons, codes: preferences.showCodes, pictos: preferences.alwaysShowPictos, raci: preferences.showRaci, stream: layout.valueStream,
    analysis: facts.analysis ? { marks: analysisMarksOf(map.value_stream, map.steps), look: analysisLook() } : null,
    title: facts.title, caption: facts.caption, fonts: fonts === '' ? null : fonts, copy: copy === null ? null : copyMetadata(copy),
  });
  return { text, size: svgSize(layout) };
}

/** The type of an image SVG. */
const SVG_TYPE = 'image/svg+xml';

/** What the poster of a map is asked with: its paper, its pages across, the marks of the layer « Analysis » or not (Makigami M9). */
export interface WallChoice {
  readonly paper: Paper;
  readonly across: number;
  readonly analysis: boolean;
}

/** What an export may ask besides its format: the copy of the map inside an image SVG (plan 3.1, L7), the choice of a poster. */
export interface ExportOptions {
  readonly editable?: boolean;
  readonly wall?: WallChoice;
}

/** The quality of the JPEG of a page of a poster: its texts sharp, its file a fraction of a PNG. */
const WALL_QUALITY = 0.92;

/**
 * Draws each page of a poster: the image SVG seen through its window, drawn by the browser at the size of its pixels on
 * the ground of the map, then kept as a JPEG; one page at a time, so that no canvas grows past what the browser draws.
 */
export function windowPainter(svg: string, ground: string): WindowPainter {
  return async (page) => {
    const url = URL.createObjectURL(new Blob([windowSvg(svg, page.window, page.pixels)], { type: SVG_TYPE }));
    try {
      const image = new Image();
      image.src = url;
      await image.decode();
      const canvas = document.createElement('canvas');
      canvas.width = page.pixels.width;
      canvas.height = page.pixels.height;
      const context = canvas.getContext('2d');
      if (context === null) throw new Error('The page cannot be drawn.');
      context.fillStyle = ground;
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.drawImage(image, 0, 0, canvas.width, canvas.height);
      const picture = canvas.toDataURL('image/jpeg', WALL_QUALITY);
      // The canvas is given back at once: a project holds a hundred of them, one after the other.
      canvas.width = 0;
      return picture;
    } finally {
      URL.revokeObjectURL(url);
    }
  };
}

/** Millimetres in a point, and the pixels of a point when a line is drawn: sharp enough for print. */
const MM_PER_POINT = 25.4 / 72;
const PIXELS_PER_POINT = 4;

/**
 * A line of the header whose letters Inter lacks, drawn by the browser in the fonts of the page (ADR 083: Chinese,
 * Japanese); null when the page cannot draw.
 */
export function drawLine(text: string, size: number, bold: boolean, grey: boolean): DrawnLine | null {
  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d');
  if (context === null) return null;
  const pixels = size * PIXELS_PER_POINT;
  const font = `${bold ? '700' : '400'} ${String(pixels)}px "Inter Variable", system-ui, sans-serif`;
  context.font = font;
  canvas.width = Math.ceil(context.measureText(text).width) + 2;
  canvas.height = Math.ceil(pixels * 1.3);
  context.font = font;
  context.fillStyle = grey ? token('--text-muted') : token('--text');
  context.textBaseline = 'alphabetic';
  context.fillText(text, 1, pixels);
  const millimetres = MM_PER_POINT / PIXELS_PER_POINT;
  return { dataUrl: canvas.toDataURL('image/png'), width: canvas.width * millimetres, height: canvas.height * millimetres, baseline: pixels * millimetres };
}

/**
 * The texts round the image of the PDF: « CODE - map », the breadcrumb joined by « > » in the language the map is shown in
 * (ADR 083), the long date, the classification, the footer (B-2.14-14 to B-2.14-18).
 */
export function pdfHeader(map: MapView, name: string, classification: string, shown: string): PdfHeader {
  return {
    title: `${map.project_code} - ${name}`,
    subtitle: map.breadcrumb.map((crumb) => crumbName({ mapId: crumb.map_id, name: crumb.name, sourceLanguage: crumb.source_language, translations: crumb.translations }, shown)).join(' > '),
    date: new Date().toLocaleString(i18next.language, { dateStyle: 'long', timeStyle: 'short' }),
    classification,
    footer: t('export.footer', { name: PRODUCT_NAME }),
  };
}

/**
 * What an image of the map shows: the whole map, or the steps of a selection, the rest left out; its longest side, the
 * 12 000 px of the export by default (ADR 109).
 */
export interface ImageScope {
  readonly steps?: readonly string[];
  readonly maximum?: number;
}

/**
 * The image of the map: the canvas in its state of export, every step drawn (3 s at most) and 50 ms more, then the view
 * captured whole, or framed on the steps of a selection, with the line of the classification for a PNG (B-2.14-02,
 * B-2.14-03, S-4.1-08, ADR 109).
 */
async function capture(editor: MapEditor, map: MapView, format: ExportFormat, caption: string | null, scope: ImageScope = {}): Promise<Captured> {
  editor.store.setState({ exporting: { format, caption, ...(scope.steps === undefined ? {} : { steps: scope.steps }) } });
  const shown = editor.canvas.target();
  if (shown === null) throw new Error('No canvas is shown.');
  await waitForSteps(shown.element, map.steps.length);
  // Read again after the wait: the canvas may have left meanwhile.
  const target = editor.canvas.target();
  const view = target?.element.querySelector<HTMLElement>('.react-flow__viewport') ?? null;
  if (target === null || view === null || target.bounds === null) throw new Error('The map has nothing to draw.');
  // The line of the classification kept whole under a selection narrower than it (R8); its width in the map, out of the zoom.
  const captionWidth = target.element.querySelector<HTMLElement>('[data-testid="export-caption"]')?.offsetWidth ?? 0;
  const options = { background: token('--surface'), fonts: await fontFaces(), caption: caption !== null, captionWidth };
  return captureView(view, target.bounds, { ...options, ...(scope.maximum === undefined ? {} : { maximum: scope.maximum }) });
}

/** The line of the classification of a project, as printed on the exports of its maps (S-4.1-08). */
export const classificationText = (project: { readonly classification: Schema<'Classification'> }): string => t('export.classification', { level: t(`classification.${project.classification}`) });

/** The classification of the project of the map, the project read. */
async function classificationOf(map: MapView): Promise<string> {
  return classificationText(await api.get('/api/projects/{id}', { path: { id: map.project_id } }));
}

/** The PNG of the map, its line of classification under it: the file of the PNG export. */
async function pngImage(editor: MapEditor, map: MapView, classification: string, scope: ImageScope = {}): Promise<Blob> {
  return pngOf((await capture(editor, map, 'png', classification, scope)).canvas);
}

/**
 * The image PNG of the map, or of the steps of a selection, as the PNG export makes it: the project read for its
 * classification, the same capture, the same frame, the same line under it (ADR 109).
 */
export async function mapImage(editor: MapEditor, map: MapView, scope: ImageScope = {}): Promise<Blob> {
  return pngImage(editor, map, await classificationOf(map), scope);
}

/** The action plan of the map and of its levels below as a sheet, none when it holds no pain point (Makigami M7). */
async function planSheets(map: MapView, shown: string): Promise<Sheet[]> {
  const plan = actionPlanSheet(await api.get('/api/maps/{id}/action-plan', { path: { id: map.id } }), shown, t);
  return plan === null ? [] : [plan];
}

/** The file of an export, made with the project read: its classification printed on it (S-4.1-08). */
async function exportFile(editor: MapEditor, map: MapView, format: ExportFormat, options: ExportOptions): Promise<{ readonly content: Blob; readonly name: string }> {
  const project = await api.get('/api/projects/{id}', { path: { id: map.project_id } });
  const classification = classificationText(project);
  // The names of the map in the language it is shown in (ADR 083); the texts of the file in the language of the interface.
  const shown = editor.store.getState().shown?.language ?? i18next.language;
  const name = localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, shown);
  const fileName = exportFileName(map.project_code, name, `.${format}`);
  if (format === 'xlsx') {
    // The value stream in two sheets more, with its layer on, as the image shows it (spec 14.9); the action plan of the
    // map and of its levels below after them, when it holds a pain point (Makigami M7).
    const sheets = [...workbookSheets(map, shown, t, editor.preferences.getState().showValueStream), ...await planSheets(map, shown)];
    return { content: await buildMapWorkbook(sheets, { title: name, classification }, workbookLook()), name: fileName };
  }
  if (format === 'md') {
    // The texts round the diagram as the PDF writes them, the tables of the workbook after it, its action plan among them
    // (plan 3.1, L3; Makigami M7).
    const header = pdfHeader(map, name, classification, shown);
    const facts = { title: header.title, breadcrumb: header.subtitle, classification, date: header.date, footer: header.footer };
    const text = mapMarkdown(map, shown, t, facts, mermaidLook(), editor.preferences.getState().showValueStream, await planSheets(map, shown));
    return { content: new Blob([text], { type: MARKDOWN_TYPE }), name: fileName };
  }
  if (format === 'svg') {
    // Never the copy for a project « Strictement confidentiel », whatever the dialog asked (plan 3.1, L7).
    const editable = options.editable === true && project.classification !== 'strictly_confidential';
    const facts = { title: pdfHeader(map, name, classification, shown).title, caption: classification, editable, analysis: editor.preferences.getState().showAnalysis };
    return { content: new Blob([(await svgText(map, shown, facts, setupOf(editor))).text], { type: SVG_TYPE }), name: fileName };
  }
  if (format === 'wall') {
    // The poster of the map (Makigami M9): its image SVG cut into pages, never its copy; a plan too large is refused by its dialog.
    const choice = options.wall ?? { paper: 'a4', across: 1, analysis: false };
    const title = pdfHeader(map, name, classification, shown).title;
    const svg = await svgText(map, shown, { title, caption: classification, editable: false, analysis: choice.analysis }, setupOf(editor));
    const plan = wallPlan(svg.size, choice.paper, choice.across);
    if (plan.tooMany) throw new Error('The poster has too many pages.');
    const content = await buildWallPdf(plan, windowPainter(svg.text, token('--surface')), { title, classification }, await pdfFonts(), drawLine);
    return { content, name: exportFileName(map.project_code, `${name} - ${t('export.wall_file')}`, '.pdf') };
  }
  if (format === 'png') return { content: await pngImage(editor, map, classification), name: fileName };
  const image = await capture(editor, map, format, null);
  const fonts = await pdfFonts();
  const header = pdfHeader(map, name, classification, shown);
  // With the layer on, the summary, the main path and the check on pages of their own (spec 3.0 section 15.4).
  const stream = editor.preferences.getState().showValueStream ? streamPdfOf(map, shown, header.title, i18next.language, t) : null;
  return { content: await buildPdf({ dataUrl: image.canvas.toDataURL('image/png'), width: image.width, height: image.height }, header, fonts, drawLine, stream), name: fileName };
}

/**
 * Exports the map shown as a PDF A3, a PNG, an image SVG, a workbook, a Markdown or a poster, saved by the browser: « Export en cours… », then done or
 * failed (B-2.14-30). Nothing goes through the writes of the map, so nothing is undone (B-2.6-45); an export asked while
 * another runs is left alone.
 */
export async function exportMap(editor: MapEditor, format: ExportFormat, options: ExportOptions = {}): Promise<void> {
  const { map, exporting } = editor.store.getState();
  if (map === null || exporting !== null) return;
  editor.store.setState({ exporting: { format, caption: null } });
  editor.notify(t('export.running'), undefined, undefined, EXPORT_TOAST_KEY);
  try {
    const file = await exportFile(editor, map, format, options);
    download(file.content, file.name);
    editor.notify(t('export.done'), 'success', undefined, EXPORT_TOAST_KEY);
  } catch {
    editor.notify(t('export.failed'), 'error', undefined, EXPORT_TOAST_KEY);
  } finally {
    editor.store.setState({ exporting: null });
  }
}

/**
 * The report told by the AI (plan 3.1, L4): the panel of the assistant opened, its practice `report` sent with its fixed
 * request; the panel shows the progress, the copy and paste when no model is called, then the report to save.
 */
export async function askReport(editor: MapEditor): Promise<void> {
  const panel = editor.assistant.getState();
  panel.setOpen(true);
  if (panel.status === null) await panel.loadStatus();
  await editor.assistant.getState().send(t('assistant.report.request'), 'report', []);
}

/**
 * Saves the report of the map shown as a Markdown file: its texts from the model, its diagrams, figures and tables from
 * the map, its classification printed on it (S-4.1-08); « Export en cours… », then done or failed, as the exports.
 */
export async function saveReport(editor: MapEditor, report: Schema<'AssistantReportView'>, model: string): Promise<void> {
  const { map } = editor.store.getState();
  if (map === null) return;
  editor.notify(t('export.running'), undefined, undefined, EXPORT_TOAST_KEY);
  try {
    const project = await api.get('/api/projects/{id}', { path: { id: map.project_id } });
    const classification = classificationText(project);
    const shown = editor.store.getState().shown?.language ?? i18next.language;
    const name = localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, shown);
    const header = pdfHeader(map, name, classification, shown);
    const facts = { title: header.title, breadcrumb: header.subtitle, classification, date: header.date, model, footer: header.footer };
    const text = reportMarkdown(map, shown, t, facts, report, mermaidLook(), editor.preferences.getState().showValueStream);
    download(new Blob([text], { type: MARKDOWN_TYPE }), exportFileName(map.project_code, `${name} - ${t('assistant.panel.forms.report')}`, '.md'));
    editor.notify(t('export.done'), 'success', undefined, EXPORT_TOAST_KEY);
  } catch {
    editor.notify(t('export.failed'), 'error', undefined, EXPORT_TOAST_KEY);
  }
}