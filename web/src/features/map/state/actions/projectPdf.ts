// projectPdf.ts
// The task of the PDF of the project (ADR 113): the project read, the maps walked from the map the export starts at, then the copy for a pdf (the only source of the sheets, read once) and the action plan; each map drawn from its view when its page is written; the progress said, the abandon heard between two readings, two maps and two pages; the file saved, or nothing; refused while another export runs.

import type { TFunction } from 'i18next';
import { api, type Schema } from '../../../../api/client.ts';
import { download, exportFileName } from '../../../../shared/download.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { formatLongDate } from '../../../../shared/i18n/formatDate.ts';
import { localizedName } from '../../../../shared/i18n/localizedName.ts';
import { pmapOf } from '../../../../shared/pmap.ts';
import { pdfFonts } from '../../export/pdfFonts.ts';
import { mapRaster } from '../../export/project/mapRaster.ts';
import {
  buildProjectPdf, copySheets, PROJECT_PDF_LIMITS, ProjectPdfRefusal,
  type CopySheet, type ProjectPdfChoice, type ProjectPdfFacts, type ProjectPdfMap, type ProjectPdfProgress,
} from '../../export/project/projectPdf.ts';
import { branchOffered, walkProject } from '../../export/project/projectWalk.ts';
import type { MapEditor } from '../editor.ts';
import { classificationText, drawLine, pdfHeader, setupOf, svgLook, svgText, windowPainter, type SvgSetup } from './exports.ts';

type MapView = Schema<'MapView'>;
type ProjectDetail = Schema<'ProjectDetail'>;

/** Where the document starts: the main map of the project, or the map shown and its levels. */
export type ProjectPdfScope = 'project' | 'branch';

/** What the dialog asks: the scope and its four boxes. */
export interface ProjectPdfAsk {
  readonly scope: ProjectPdfScope;
  readonly choice: ProjectPdfChoice;
}

/** What the dialog shows while the task runs: the step said, and its bar (the maps drawn of the total; no value while unknown). */
export interface ProjectPdfStatus {
  readonly text: string;
  readonly value: number | null;
  readonly max: number | null;
}

/** What the task runs with: the abandon of the dialog (Cancel, Escape, its closing), and where the progress is said. */
export interface ProjectPdfRun {
  readonly signal: AbortSignal;
  readonly progress: (status: ProjectPdfStatus) => void;
}

/** Why the export is refused: past the maps or the pages, or another export running. */
export type ProjectPdfReason = 'maps' | 'pages' | 'busy';

/** How the task ended: saved, cancelled, failed (said by a notification), or refused by a bound or by an export running (said by the dialog). */
export type ProjectPdfOutcome =
  | { readonly kind: 'done' | 'cancelled' | 'failed' }
  | { readonly kind: 'refused'; readonly reason: ProjectPdfReason; readonly message: string };

/**
 * The texts of the refusals, said in the dialog. Past the maps, the advice is what the dialog offers there: on a detailed
 * map asked for the whole project, its branch; else, a detailed map further down to open, then its branch. Past the pages,
 * a detailed map and its levels are advised on a detailed map only; the main map is told to untick the sheets.
 */
function refusedOf(reason: ProjectPdfReason, t: TFunction, map: MapView, ask: ProjectPdfAsk): ProjectPdfOutcome {
  const scope = t('project_pdf.scope_branch');
  const maps = ask.scope === 'project' && branchOffered(map) ? 'project_pdf.too_many_maps' : 'project_pdf.too_many_maps_below';
  // Past the pages, a detailed map and its levels are advised only where there is one to export.
  const pages = branchOffered(map) ? 'project_pdf.too_many_pages' : 'project_pdf.too_many_pages_top';
  const message = reason === 'busy' ? t('export.running') : reason === 'maps' ? t(maps, { max: PROJECT_PDF_LIMITS.maps, scope }) : t(pages, { max: PROJECT_PDF_LIMITS.pages });
  return { kind: 'refused', reason, message };
}

/** The progress of the writing as the dialog says it: the bar follows the maps drawn, the text the last step. */
function writingProgress(run: ProjectPdfRun, t: TFunction): (step: ProjectPdfProgress) => void {
  let bar: Pick<ProjectPdfStatus, 'value' | 'max'> = { value: null, max: null };
  return (step) => {
    if (step.kind === 'drawing') bar = { value: step.index, max: step.total };
    const text = step.kind === 'drawing' ? t('project_pdf.drawing', { index: step.index, total: step.total }) : t('project_pdf.writing', { page: step.page });
    run.progress({ text, ...bar });
  };
}

/** The sheets as the person sees them, from one copy of the branch the export starts at, read for a pdf (the host writes its event). */
async function readSheets(start: string, signal: AbortSignal): Promise<ReadonlyMap<string, CopySheet>> {
  signal.throwIfAborted();
  return copySheets(pmapOf(await api.get('/api/maps/{id}/pmap', { path: { id: start }, query: { purpose: 'pdf' }, signal })));
}

/** What a map is drawn with: the preferences of the person, its band of the value stream as the box says, the empty rows of the map shown only. */
const setupFor = (setup: SvgSetup, view: MapView, shownId: string, stream: boolean): SvgSetup => ({
  preferences: { ...setup.preferences, showValueStream: stream }, rows: view.id === shownId ? setup.rows : {},
});

/** The maps of the walk for the document: each named and titled as the PDF of a map, its image drawn from its view only when its page is written. */
function documentMaps(editor: MapEditor, views: readonly MapView[], shown: string, classification: string, stream: boolean): ProjectPdfMap[] {
  const setup = setupOf(editor);
  const shownId = editor.store.getState().map?.id ?? '';
  const analysis = editor.preferences.getState().showAnalysis;
  return views.map((view) => {
    const name = localizedName({ name: view.name, source_language: view.source_language, name_translations: view.translations }, shown);
    const header = pdfHeader(view, name, classification, shown);
    const image = async () => {
      const svg = await svgText(view, shown, { title: header.title, caption: classification, editable: false, analysis }, setupFor(setup, view, shownId, stream));
      return mapRaster(svg.text, windowPainter(svg.text, svgLook().surface));
    };
    return { view, name, title: header.title, path: header.subtitle, image };
  });
}

/** The facts of the title page: the project named in the language shown, the day alone, its description or the summary of the first map, the scope. */
function factsOf(project: ProjectDetail, first: MapView, scope: string | null, shown: string, classification: string): ProjectPdfFacts {
  const summary = first.summary ?? null;
  return {
    name: localizedName(project, shown), code: project.code, classification,
    date: formatLongDate(new Date(), i18next.language),
    description: project.description,
    summary: summary === null ? null : localizedName({ name: summary.text, source_language: summary.source_language, name_translations: summary.translations }, shown),
    scope,
  };
}

/** The file of the project: the readings in their order, then the document; a bound passed gives its refusal, nothing read past it. */
async function projectFile(editor: MapEditor, map: MapView, ask: ProjectPdfAsk, run: ProjectPdfRun, t: TFunction): Promise<ProjectPdfOutcome> {
  const { signal } = run;
  const shown = editor.store.getState().shown?.language ?? i18next.language;
  const project = await api.get('/api/projects/{id}', { path: { id: map.project_id }, signal });
  const classification = classificationText(project);
  const start = ask.scope === 'branch' ? map.id : map.breadcrumb[0]?.map_id ?? project.root_map_id;
  const walk = await walkProject(start, { signal, progress: (count) => { run.progress({ text: t('project_pdf.reading', { count }), value: null, max: null }); } });
  if (walk.kind === 'too_many') return refusedOf('maps', t, map, ask);
  const [first] = walk.maps;
  if (first === undefined) throw new Error('The walk read no map.');
  if (ask.choice.sheets) run.progress({ text: t('project_pdf.reading_sheets'), value: null, max: null });
  const sheets = ask.choice.sheets ? await readSheets(start, signal) : new Map<string, CopySheet>();
  signal.throwIfAborted();
  const plan = ask.choice.plan ? await api.get('/api/maps/{id}/action-plan', { path: { id: start }, signal }) : null;
  const scope = ask.scope === 'branch' ? localizedName({ name: first.name, source_language: first.source_language, name_translations: first.translations }, shown) : null;
  const facts = factsOf(project, first, scope, shown, classification);
  const content = { facts, choice: ask.choice, maps: documentMaps(editor, walk.maps, shown, classification, ask.choice.stream), sheets, plan, depthCut: walk.depthCut };
  const tools = { fonts: await pdfFonts(), t, language: i18next.language, shown, draw: drawLine, signal, progress: writingProgress(run, t) };
  const blob = await buildProjectPdf(content, tools);
  signal.throwIfAborted();
  download(blob, exportFileName(project.code, `${facts.name} - ${t('project_pdf.file')}`, '.pdf'));
  return { kind: 'done' };
}

/**
 * Exports the project as one PDF from the map shown, as the dialog asks it: saved by the browser and « Export terminé… »;
 * cancelled, nothing saved and « Export annulé. »; a bound passed, its refusal given to the dialog; anything else, failed.
 * Nothing goes through the writes of the map, and the canvas is not put in its state of export. Asked while another
 * export runs, it is refused, as the other exports leave alone one asked meanwhile (the dialog is modal: none starts
 * while it runs).
 */
export async function exportProjectPdf(editor: MapEditor, ask: ProjectPdfAsk, run: ProjectPdfRun): Promise<ProjectPdfOutcome> {
  const t = i18next.getFixedT(i18next.language);
  const { map, exporting } = editor.store.getState();
  if (map === null) return { kind: 'failed' };
  if (exporting !== null) return refusedOf('busy', t, map, ask);
  try {
    const outcome = await projectFile(editor, map, ask, run, t);
    if (outcome.kind === 'done') editor.notify(t('export.done'), 'success');
    return outcome;
  } catch (error) {
    if (run.signal.aborted) {
      editor.notify(t('project_pdf.cancelled'));
      return { kind: 'cancelled' };
    }
    if (error instanceof ProjectPdfRefusal) return refusedOf(error.reason, t, map, ask);
    editor.notify(t('export.failed'), 'error');
    return { kind: 'failed' };
  }
}
