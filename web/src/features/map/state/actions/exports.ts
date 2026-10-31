// exports.ts
// The exports of the map: its image saved as a PNG or placed in a PDF A3 (the pages of the value stream after it, its layer on), the workbook of its steps, links and RACI matrix; the project read for its classification; the notifications; nothing written, nothing to undo (annex B 2.14, B-2.6-45, S-4.1-08, ADR 066).

import { api, type Schema } from '../../../../api/client.ts';
import { crumbName } from '../../../../app/EditorBar.tsx';
import { download, exportFileName } from '../../../../shared/download.ts';
import { i18next, PRODUCT_NAME, type Translate } from '../../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../../shared/i18n/localizedName.ts';
import { captureView, fontFaces, pngOf, waitForSteps, type Captured } from '../../export/capture.ts';
import { buildPdf, type PdfHeader, type DrawnLine } from '../../export/pdf.ts';
import { pdfFonts } from '../../export/pdfFonts.ts';
import { streamPdfOf } from '../../export/streamPdf.ts';
import { argbOf, buildMapWorkbook, workbookSheets, type WorkbookLook } from '../../export/workbook.ts';
import type { RaciLetter } from '../../model/raci.ts';
import type { MapEditor } from '../editor.ts';
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

/** Millimetres in a point, and the pixels of a point when a line is drawn: sharp enough for print. */
const MM_PER_POINT = 25.4 / 72;
const PIXELS_PER_POINT = 4;

/**
 * A line of the header whose letters Inter lacks, drawn by the browser in the fonts of the page (ADR 083: Chinese,
 * Japanese); null when the page cannot draw.
 */
function drawLine(text: string, size: number, bold: boolean, grey: boolean): DrawnLine | null {
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
function pdfHeader(map: MapView, name: string, classification: string, shown: string): PdfHeader {
  return {
    title: `${map.project_code} - ${name}`,
    subtitle: map.breadcrumb.map((crumb) => crumbName({ mapId: crumb.map_id, name: crumb.name, sourceLanguage: crumb.source_language, translations: crumb.translations }, shown)).join(' > '),
    date: new Date().toLocaleString(i18next.language, { dateStyle: 'long', timeStyle: 'short' }),
    classification,
    footer: t('export.footer', { name: PRODUCT_NAME }),
  };
}

/**
 * The image of the map: the canvas in its state of export, every step drawn (3 s at most) and 50 ms more, then the view
 * captured whole, with the line of the classification for a PNG (B-2.14-02, B-2.14-03, S-4.1-08).
 */
async function capture(editor: MapEditor, map: MapView, format: ExportFormat, caption: string | null): Promise<Captured> {
  editor.store.setState({ exporting: { format, caption } });
  const shown = editor.canvas.target();
  if (shown === null) throw new Error('No canvas is shown.');
  await waitForSteps(shown.element, map.steps.length);
  // Read again after the wait: the canvas may have left meanwhile.
  const target = editor.canvas.target();
  const view = target?.element.querySelector<HTMLElement>('.react-flow__viewport') ?? null;
  if (target === null || view === null || target.bounds === null) throw new Error('The map has nothing to draw.');
  return captureView(view, target.bounds, { background: token('--surface'), fonts: await fontFaces(), caption: caption !== null });
}

/** The file of an export, made with the project read: its classification printed on it (S-4.1-08). */
async function exportFile(editor: MapEditor, map: MapView, format: ExportFormat): Promise<{ readonly content: Blob; readonly name: string }> {
  const project = await api.get('/api/projects/{id}', { path: { id: map.project_id } });
  const classification = t('export.classification', { level: t(`classification.${project.classification}`) });
  // The names of the map in the language it is shown in (ADR 083); the texts of the file in the language of the interface.
  const shown = editor.store.getState().shown?.language ?? i18next.language;
  const name = localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, shown);
  const fileName = exportFileName(map.project_code, name, `.${format}`);
  if (format === 'xlsx') {
    // The value stream in two sheets more, with its layer on, as the image shows it (spec 14.9).
    const sheets = workbookSheets(map, shown, t, editor.preferences.getState().showValueStream);
    return { content: await buildMapWorkbook(sheets, { title: name, classification }, workbookLook()), name: fileName };
  }
  const image = await capture(editor, map, format, format === 'png' ? classification : null);
  if (format === 'png') return { content: await pngOf(image.canvas), name: fileName };
  const fonts = await pdfFonts();
  const header = pdfHeader(map, name, classification, shown);
  // With the layer on, the summary, the main path and the check on pages of their own (spec 3.0 section 15.4).
  const stream = editor.preferences.getState().showValueStream ? streamPdfOf(map, shown, header.title, i18next.language, t) : null;
  return { content: await buildPdf({ dataUrl: image.canvas.toDataURL('image/png'), width: image.width, height: image.height }, header, fonts, drawLine, stream), name: fileName };
}

/**
 * Exports the map shown as a PDF A3, a PNG or a workbook, saved by the browser: « Export en cours… », then done or
 * failed (B-2.14-30). Nothing goes through the writes of the map, so nothing is undone (B-2.6-45); an export asked while
 * another runs is left alone.
 */
export async function exportMap(editor: MapEditor, format: ExportFormat): Promise<void> {
  const { map, exporting } = editor.store.getState();
  if (map === null || exporting !== null) return;
  editor.store.setState({ exporting: { format, caption: null } });
  editor.notify(t('export.running'));
  try {
    const file = await exportFile(editor, map, format);
    download(file.content, file.name);
    editor.notify(t('export.done'), 'success');
  } catch {
    editor.notify(t('export.failed'), 'error');
  } finally {
    editor.store.setState({ exporting: null });
  }
}
