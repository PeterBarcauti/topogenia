// projectPdf.test.ts
// The task of the PDF of the project against a fake host: the walk first, the copy for a pdf read once from the map the export starts at and the only source of the sheets, the action plan from its route, the progress, the abandon (nothing more read, nothing written, nothing saved), a refusal of the host, the name of the file.

import type { Schema } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT, type PmapDocument } from '../../../../shared/pmap.ts';
import { captureDownloads } from '../../../../test/downloads.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { callsTo, fakeApi, heldReply, type FakeRoutes, type SentCall } from '../../../../test/fakeApi.ts';
import { laneOf, mapOf, PROJECT_ID, sheetOf, stepOf } from '../../../../test/maps.ts';
import { pdfPages, type PdfPage } from '../../../../test/pdf.ts';
import { PDF_FACES, PDF_MARGIN } from '../../export/pdf.ts';
import type { ProjectPdfChoice } from '../../export/project/projectPdf.ts';
import { exportProjectPdf, type ProjectPdfAsk, type ProjectPdfStatus } from './projectPdf.ts';

// The faces of Inter read from the package on the disk: jsdom serves no file of the application.
vi.mock('../../export/pdfFonts.ts', async () => {
  const { interFonts } = await import('../../../../test/pdf.ts');
  return { pdfFonts: () => Promise.resolve(interFonts()) };
});

// The writing of the document passing the bound of the pages, when the test says so (a real document of 1 000 pages is not written here).
let passPages = false;
vi.mock('../../export/project/projectPdf.ts', async (importOriginal) => {
  const real = await importOriginal<typeof import('../../export/project/projectPdf.ts')>();
  return { ...real, buildProjectPdf: (...args: Parameters<typeof real.buildProjectPdf>) => (passPages ? Promise.reject(new real.ProjectPdfRefusal('pages')) : real.buildProjectPdf(...args)) };
});

/** A white pixel: what the browser gives for each map drawn. */
const PIXEL = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

let downloads: ReturnType<typeof captureDownloads>;
let drawn: number;

beforeEach(async () => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 10, 21, 10, 15, 20));
  await i18next.changeLanguage('fr');
  passPages = false;
  downloads = captureDownloads();
  // The browser drawing each map: its image decoded at once, a canvas whose context measures 7 a letter, a pixel as its picture.
  drawn = 0;
  vi.stubGlobal('Image', class { src = ''; decode = () => Promise.resolve(); });
  const context = {
    font: '', fillStyle: '', textBaseline: '', fillRect: vi.fn(), fillText: vi.fn(), measureText: (text: string) => ({ width: 7 * text.length }),
    drawImage: () => { drawn += 1; },
  };
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(context as unknown as CanvasRenderingContext2D);
  vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue(PIXEL);
});

afterEach(() => {
  downloads.restore();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const lanes = [laneOf('l1', 0, { name: 'Accueil' })];
const crumb = (id: string, name: string) => ({ map_id: id, name, source_language: 'fr', translations: {} });

/**
 * Two levels: Visites, whose step Accueillir is detailed by the map Accueillir and whose step Facturer says it has a
 * validated sheet; Accueillir holds Contrôler, whose sheet its map shows as a draft.
 */
const ROOT = mapOf({
  id: 'root', name: 'Visites', lanes, breadcrumb: [crumb('root', 'Visites')], summary: null,
  steps: [
    stepOf('a', 'l1', 0, { code: 'VIS-E001', label: 'Accueillir', sub_map_id: 'ma', sub_map_empty: false, sheet: sheetOf({ status: 'validated', completeness: 1 }) }),
    stepOf('b', 'l1', 1, { code: 'VIS-E002', label: 'Facturer', sheet: sheetOf({ status: 'validated', completeness: 1 }) }),
  ],
});
const MA = mapOf({
  id: 'ma', name: 'Accueillir', lanes, parent_step_id: 'a', depth: 2, breadcrumb: [crumb('root', 'Visites'), crumb('ma', 'Accueillir')],
  steps: [stepOf('c', 'l1', 0, { code: 'VIS-E003', label: 'Contrôler', sheet: sheetOf({ status: 'draft', completeness: 0.5 }) })],
});

/** The copy of the branch the host serves for a pdf: the sheets the person may see, the one of the detailed map among them; none for Facturer. */
const COPY: PmapDocument = {
  format: PMAP_FORMAT, version: '1.5', project: { name: 'Visites clients' },
  map: {
    lanes: [],
    steps: [
      { id: 'VIS-E001', lane: 'c1', label: 'Accueillir', sheet: { purpose: 'Accueillir le visiteur' }, sub_map: { lanes: [], steps: [{ id: 'VIS-E003', lane: 'c1', label: 'Contrôler', sheet: { purpose: 'Contrôler la pièce' } }] } },
      { id: 'VIS-E002', lane: 'c1', label: 'Facturer' },
    ],
  },
};

const named = (text: string): Schema<'NamedText'> => ({ text, source_language: 'fr', translations: {} });
const PLAN: Schema<'ActionPlanView'> = {
  open_lost_hours_per_month: 6,
  lines: [{
    pain_point_id: 'p1', code: 'VIS-I001', description: 'Saisie en double', status: 'reported', lost_hours_per_month: 6, action: 'Lire l’extraction', owner: 'Service paie',
    due_date: '2026-12-15', map_id: 'ma', map: named('Accueillir'), step_id: 'c', step_code: 'VIS-E003', step: named('Contrôler'), lane: named('Accueil'), open: true,
    receiving_step_id: 'c', receiving_map_id: 'ma', document_id: null, can_fill: false, status_step_id: 'c', version: 1, status_changed_at: null,
  }],
};

const PROJECT = {
  id: PROJECT_ID, code: 'VIS', name: 'Visites clients', source_language: 'fr', name_translations: { en: 'Customer visits' },
  description: 'Le parcours d’une visite.', shared_folder: null, classification: 'confidential', sharing_group_label: null, translation_glossary: null,
  root_map_id: 'root', role: 'reader', members: [], version: 4,
};

/** The reply of the host for a map: its view and the history of its writes. */
const replyOf = (view: Schema<'MapView'>) => ({ json: { map: view, history: { undo: [], redo: [] } } });

/** The routes of the host: the project, each map, the copy, the plan; those given replace them. */
const routesOf = (overrides: FakeRoutes = {}): FakeRoutes => ({
  [`GET /api/projects/${PROJECT_ID}`]: { json: PROJECT },
  'GET /api/maps/root': replyOf(ROOT),
  'GET /api/maps/ma': replyOf(MA),
  'GET /api/maps/root/pmap': { text: JSON.stringify(COPY) },
  'GET /api/maps/ma/pmap': { text: JSON.stringify({ ...COPY, map: COPY.map.steps?.[0]?.sub_map }) },
  'GET /api/maps/root/action-plan': { json: PLAN },
  'GET /api/maps/ma/action-plan': { json: PLAN },
  ...overrides,
});

const NONE: ProjectPdfChoice = { sheets: false, raci: false, stream: false, plan: false };
const SHEETS: ProjectPdfAsk = { scope: 'project', choice: { ...NONE, sheets: true } };

/** Runs the task from the detailed map Accueillir shown: the calls sent, the notifications, the statuses said, its outcome. */
async function run(ask: ProjectPdfAsk, routes: FakeRoutes = routesOf(), controller = new AbortController(), onStatus: (status: ProjectPdfStatus) => void = () => undefined) {
  const sent = fakeApi(routes);
  const { editor, notes } = await editorOf(MA);
  const statuses: ProjectPdfStatus[] = [];
  const outcome = await exportProjectPdf(editor, ask, { signal: controller.signal, progress: (status) => { statuses.push(status); onStatus(status); } });
  return { sent, notes, statuses, outcome };
}

/** The pages of the file saved. */
async function savedPages(): Promise<PdfPage[]> {
  const file = downloads.files[0];
  expect(file).toBeDefined();
  return pdfPages(new TextDecoder('latin1').decode(await file?.content.arrayBuffer()));
}

const textsOf = (page: PdfPage | undefined): string[] => (page?.texts ?? []).map((text) => text.text);
const titleOf = (page: PdfPage | undefined): string => page?.texts.find((text) => text.size === 16 && text.face === PDF_FACES.bold && Math.abs(text.y - (PDF_MARGIN + 4)) < 0.01)?.text ?? '';
const index = (sent: readonly SentCall[], path: string): number => sent.findIndex((call) => call.path === path);
const DONE: [string, 'success'] = ['Export terminé : le fichier est dans le dossier Téléchargements.', 'success'];
const day = () => new Date().toISOString().slice(0, 10);

describe('the sheets: the copy for a pdf, the only source', () => {
  it('with the sheets, the copy is read once, with the purpose pdf, from the map the export starts at, after the walk', { timeout: 15_000 }, async () => {
    const { sent, notes, outcome } = await run(SHEETS);
    expect([outcome, notes]).toEqual([{ kind: 'done' }, [DONE]]);
    const copies = sent.filter((call) => call.path.endsWith('/pmap'));
    expect(copies.map((call) => [call.path, call.query.get('purpose')])).toEqual([['/api/maps/root/pmap', 'pdf']]);
    expect(index(sent, '/api/maps/root/pmap')).toBeGreaterThan(Math.max(index(sent, '/api/maps/root'), index(sent, '/api/maps/ma')));
    // The sheet of the detailed map comes from the same copy, after its map.
    const pages = await savedPages();
    expect(pages.slice(2).map(titleOf)).toEqual(['VIS - Visites', 'Fiches · VIS - Visites', 'VIS - Accueillir', 'Fiches · VIS - Accueillir']);
    expect(textsOf(pages[5])).toEqual(expect.arrayContaining(['VIS-E003 Contrôler', 'Contrôler la pièce']));
    // From the map shown, This map and its levels reads the copy of that map.
    downloads.files.length = 0;
    const branch = await run({ ...SHEETS, scope: 'branch' });
    expect(branch.sent.filter((call) => call.path.endsWith('/pmap')).map((call) => [call.path, call.query.get('purpose')])).toEqual([['/api/maps/ma/pmap', 'pdf']]);
    expect(callsTo(branch.sent, 'GET', '/api/maps/root')).toEqual([]);
  });

  it('without the sheets, no copy is read', { timeout: 15_000 }, async () => {
    const { sent, outcome } = await run({ scope: 'project', choice: { sheets: false, raci: true, stream: true, plan: true } });
    expect(outcome).toEqual({ kind: 'done' });
    expect(sent.filter((call) => call.path.endsWith('/pmap'))).toEqual([]);
    expect((await savedPages()).some((page) => titleOf(page).startsWith('Fiches'))).toBe(false);
  });

  it('a step gets a sheet only when the copy gives it one, even when its map says it has content', { timeout: 15_000 }, async () => {
    const { sent } = await run(SHEETS);
    const pages = await savedPages();
    const texts = pages.filter((page) => titleOf(page).startsWith('Fiches')).flatMap(textsOf);
    expect(texts).toContain('VIS-E001 Accueillir');
    expect(texts).not.toContain('VIS-E002 Facturer');
    // No other reading of a sheet: the copy is the only source.
    expect(sent.filter((call) => call.path.startsWith('/api/steps/'))).toEqual([]);
    // A copy giving no sheet gives no page of sheets, whatever the maps say.
    downloads.files.length = 0;
    await run(SHEETS, routesOf({ 'GET /api/maps/root/pmap': { text: JSON.stringify({ ...COPY, map: { lanes: [], steps: [] } }) } }));
    expect((await savedPages()).some((page) => titleOf(page).startsWith('Fiches'))).toBe(false);
  });

  it('the state under a sheet is the one its map shows', { timeout: 15_000 }, async () => {
    await run(SHEETS);
    const pages = await savedPages();
    expect(textsOf(pages[3])).toContain('État de la fiche : validée');
    expect(textsOf(pages[5])).toContain('État de la fiche : brouillon');
  });
});

describe('the action plan, the pages, the file', () => {
  it('the action plan comes from the route of the plan of the map the export starts at, through planEntries', { timeout: 15_000 }, async () => {
    const { sent } = await run({ scope: 'branch', choice: { ...NONE, plan: true } });
    expect(sent.filter((call) => call.path.endsWith('/action-plan')).map((call) => call.path)).toEqual(['/api/maps/ma/action-plan']);
    const last = (await savedPages()).at(-1);
    expect(titleOf(last)).toBe('Plan d’actions');
    expect(textsOf(last)).toEqual(expect.arrayContaining(['VIS-I001', 'Saisie en double', 'Lire l’extraction', 'Service paie']));
    // Without the box, the route is not read.
    const without = await run({ scope: 'branch', choice: NONE });
    expect(without.sent.filter((call) => call.path.endsWith('/action-plan'))).toEqual([]);
  });

  it('the file is named after the code and the name of the project', { timeout: 15_000 }, async () => {
    await run({ scope: 'project', choice: NONE });
    expect(downloads.files.map((file) => [file.name, file.content.type])).toEqual([[`VIS_Visites-clients-projet_${day()}.pdf`, 'application/pdf']]);
    // The title page: the name, the code, the classification, the day alone, the description.
    expect(textsOf((await savedPages())[0])).toEqual(expect.arrayContaining(['VIS', 'Visites clients', 'Classification : Confidentiel', '21 novembre 2026', 'Le parcours d’une visite.']));
    downloads.files.length = 0;
    await i18next.changeLanguage('en');
    await run({ scope: 'branch', choice: NONE });
    expect(downloads.files.map((file) => file.name)).toEqual([`VIS_Customer-visits-project_${day()}.pdf`]);
    expect(textsOf((await savedPages())[0])).toEqual(expect.arrayContaining(['Customer visits', 'November 21, 2026', 'Map “Accueillir” and its levels']));
  });

  it('each map is drawn once, from its own image, when its page is written', { timeout: 15_000 }, async () => {
    await run({ scope: 'project', choice: NONE });
    expect(drawn).toBe(2);
    expect((await savedPages()).slice(2).map((page) => page.images.length)).toEqual([1, 1]);
  });
});

describe('the progress, the bounds, the abandon, the failures', () => {
  it('the progress says the maps read, the map drawn of the total, the page written', { timeout: 15_000 }, async () => {
    const { statuses } = await run(SHEETS);
    const texts = statuses.map((status) => status.text);
    expect(texts.slice(0, 3)).toEqual(['Lecture des cartes : 1', 'Lecture des cartes : 2', 'Lecture des fiches…']);
    expect(texts).toEqual(expect.arrayContaining(['Carte 1 sur 2', 'Carte 2 sur 2', 'Écriture du document : page 1', 'Écriture du document : page 6']));
    expect(texts.indexOf('Carte 2 sur 2')).toBeGreaterThan(texts.indexOf('Carte 1 sur 2'));
    // The bar follows the maps drawn; it waits without a value before them.
    expect(statuses.find((status) => status.text === 'Carte 2 sur 2')).toEqual({ text: 'Carte 2 sur 2', value: 2, max: 2 });
    expect(statuses[0]).toEqual({ text: 'Lecture des cartes : 1', value: null, max: null });
  });

  it('cancelled, nothing is downloaded and Export cancelled is said', async () => {
    // Cancelled while a map is read: no other map, no copy, no plan, no file.
    const held = heldReply();
    const controller = new AbortController();
    const sent = fakeApi(routesOf({ 'GET /api/maps/ma': held.reply }));
    const { editor, notes } = await editorOf(MA);
    const running = exportProjectPdf(editor, { scope: 'project', choice: { sheets: true, raci: true, stream: true, plan: true } }, { signal: controller.signal, progress: () => undefined });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'GET', '/api/maps/ma')).toHaveLength(1);
    });
    controller.abort();
    held.give(replyOf(MA));
    expect(await running).toEqual({ kind: 'cancelled' });
    expect(sent.map((call) => call.path)).toEqual([`/api/projects/${PROJECT_ID}`, '/api/maps/root', '/api/maps/ma']);
    expect([downloads.files, notes]).toEqual([[], [['Export annulé.', undefined]]]);
  });

  it('cancelled once the maps are read, the copy is not asked, so nothing is written by the host', async () => {
    const controller = new AbortController();
    const { sent, notes, outcome } = await run(SHEETS, routesOf(), controller, (status) => {
      if (status.text === 'Lecture des cartes : 2') controller.abort();
    });
    expect(outcome).toEqual({ kind: 'cancelled' });
    expect(sent.filter((call) => call.path.endsWith('/pmap') || call.path.endsWith('/action-plan'))).toEqual([]);
    expect([downloads.files, notes]).toEqual([[], [['Export annulé.', undefined]]]);
  });

  it('cancelled while a map is drawn, nothing more is drawn and nothing is downloaded', { timeout: 15_000 }, async () => {
    const controller = new AbortController();
    const { notes, outcome } = await run(SHEETS, routesOf(), controller, (status) => {
      if (status.text === 'Carte 1 sur 2') controller.abort();
    });
    expect([outcome, drawn, downloads.files, notes]).toEqual([{ kind: 'cancelled' }, 0, [], [['Export annulé.', undefined]]]);
  });

});

describe('the refusals', () => {
  it('past 100 maps the export is refused, said in the dialog: nothing more is read, nothing saved', async () => {
    const children = Array.from({ length: 101 }, (_, rank) => `c${String(rank)}`);
    const big = mapOf({ ...ROOT, steps: children.map((id, rank) => stepOf(`s${String(rank)}`, 'l1', rank, { code: `VIS-E${String(rank + 100)}`, sub_map_id: id, sub_map_empty: true })) });
    const { sent, notes, outcome } = await run(SHEETS, routesOf({ 'GET /api/maps/root': replyOf(big) }));
    // From a detailed map, the whole project asked: the dialog offers this map and its levels.
    expect(outcome).toEqual({ kind: 'refused', reason: 'maps', message: 'Plus de 100 cartes : choisissez « Cette carte et ses niveaux ».' });
    expect(sent.map((call) => call.path)).toEqual([`/api/projects/${PROJECT_ID}`, '/api/maps/root']);
    expect([downloads.files, notes]).toEqual([[], []]);
  });

  it('past 100 maps, the refusal advises only what the dialog offers where it is said', async () => {
    const children = Array.from({ length: 101 }, (_, rank) => `c${String(rank)}`);
    const big = (view: Schema<'MapView'>) => mapOf({ ...view, steps: children.map((id, rank) => stepOf(`s${String(rank)}`, 'l1', rank, { code: `VIS-E${String(rank + 100)}`, sub_map_id: id, sub_map_empty: false })) });
    const below = 'Plus de 100 cartes : ouvrez une carte détaillée plus bas, puis choisissez « Cette carte et ses niveaux ».';
    // This map and its levels already asked, from the detailed map.
    const branch = await run({ ...SHEETS, scope: 'branch' }, routesOf({ 'GET /api/maps/ma': replyOf(big(MA)) }));
    expect(branch.outcome).toEqual({ kind: 'refused', reason: 'maps', message: below });
    // From the main map, where the dialog offers the whole project only.
    fakeApi(routesOf({ 'GET /api/maps/root': replyOf(big(ROOT)) }));
    const { editor } = await editorOf(ROOT);
    const main = await exportProjectPdf(editor, SHEETS, { signal: new AbortController().signal, progress: () => undefined });
    expect(main).toEqual({ kind: 'refused', reason: 'maps', message: below });
  });

  it('past 1 000 pages, the refusal advises only what the dialog offers where it is said', async () => {
    passPages = true;
    // From the detailed map, a map and its levels exist to export.
    const detailed = await run(SHEETS);
    expect(detailed.outcome).toEqual({ kind: 'refused', reason: 'pages', message: 'Plus de 1000 pages : décochez les fiches ou exportez une carte détaillée et ses niveaux.' });
    // From the main map, the dialog offers the whole project only: no detailed map is advised.
    fakeApi(routesOf());
    const { editor } = await editorOf(ROOT);
    const main = await exportProjectPdf(editor, SHEETS, { signal: new AbortController().signal, progress: () => undefined });
    expect(main).toEqual({ kind: 'refused', reason: 'pages', message: 'Plus de 1000 pages : décochez les fiches.' });
  });

  it('below twelve levels the maps are left out and said in the contents, never refused', { timeout: 30_000 }, async () => {
    // Thirteen levels under the main map, each map detailing the step of the one before.
    const ids = Array.from({ length: 13 }, (_, rank) => (rank === 0 ? 'root' : `n${String(rank)}`));
    const views = ids.map((id, rank) => mapOf({
      id, name: `Niveau ${String(rank)}`, lanes, parent_step_id: rank === 0 ? null : `t${String(rank - 1)}`, breadcrumb: [crumb('root', 'Visites')],
      steps: [stepOf(`t${String(rank)}`, 'l1', 0, { code: `VIS-E${String(rank + 100)}`, sub_map_id: ids[rank + 1] ?? null, sub_map_empty: false })],
    }));
    const routes = Object.fromEntries(views.map((view) => [`GET /api/maps/${view.id}`, replyOf(view)]));
    const { outcome, sent } = await run({ scope: 'project', choice: NONE }, routesOf(routes));
    expect(outcome).toEqual({ kind: 'done' });
    expect(callsTo(sent, 'GET', '/api/maps/n12')).toEqual([]);
    const pages = await savedPages();
    expect(pages.slice(2).map(titleOf)).toEqual(views.slice(0, 12).map((view) => `VIS - ${view.name}`));
    expect(textsOf(pages[1])).toContain('Les cartes au-delà de 12 niveaux ne sont pas dans ce document.');
  });

  it('asked while another export runs, the export is refused with the message of the exports, nothing read', async () => {
    const sent = fakeApi(routesOf());
    const { editor, notes } = await editorOf(MA);
    editor.store.setState({ exporting: { format: 'wall', caption: null } });
    const outcome = await exportProjectPdf(editor, SHEETS, { signal: new AbortController().signal, progress: () => undefined });
    expect(outcome).toEqual({ kind: 'refused', reason: 'busy', message: 'Export en cours…' });
    expect([sent, notes, downloads.files]).toEqual([[], [], []]);
  });

  it('a refusal of the host fails the export and nothing is downloaded', { timeout: 15_000 }, async () => {
    const copy = await run(SHEETS, routesOf({ 'GET /api/maps/root/pmap': { refused: 403, code: 'ACCES_REFUSE' } }));
    expect([copy.outcome, copy.notes, downloads.files]).toEqual([{ kind: 'failed' }, [['L’export a échoué. Réessayer dans un instant.', 'error']], []]);
    const map = await run(SHEETS, routesOf({ 'GET /api/maps/ma': { refused: 404, code: 'INTROUVABLE' } }));
    expect([map.outcome, map.notes, downloads.files]).toEqual([{ kind: 'failed' }, [['L’export a échoué. Réessayer dans un instant.', 'error']], []]);
    expect(map.sent.filter((call) => call.path.endsWith('/pmap'))).toEqual([]);
  });
});

