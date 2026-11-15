// sheetDock.test.tsx
// The sheet opened from the map: docked on the right of the canvas, a new one for each step, folded at each opening unless « Tout déplier » was asked (essai J10), the gauge of the step that follows it, the map read again after the documents, the project read once (annex B 4, B-2.1-39, AR-91).

import { act, fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { openSheetOf, showEditor as showFolded } from '../../../test/editor.tsx';
import { callsTo, fakeApi, type FakeRoutes } from '../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap, sheetOf, stepOf } from '../../../test/maps.ts';
import { clipboard } from '../state/clipboard.ts';
import { contentOf, documentOf, inputOf, memberOf, OTHER_ID, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';

/**
 * The map shown as after « Tout déplier »: most of these tests are about what a sheet does, not about its folds, and
 * reach its fields at once; the folds of a sheet opened from the map are told by the tests that call `showFolded`.
 */
async function showEditor(...given: Parameters<typeof showFolded>) {
  const shown = await showFolded(...given);
  shown.editor.store.setState({ sheetsUnfolded: true });
  return shown;
}

const SECTIONS = ['facts', 'inputs', 'procedure', 'outputs', 'tools'] as const;
/** Whether each section of the sheet drawn is unfolded, in their order. */
const unfoldedSections = () => SECTIONS.map((section) => screen.getByTestId(`sheet-fold-${section}`).getAttribute('aria-expanded') === 'true');

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;
const pane = (canvas: HTMLElement) => canvas.querySelector('.react-flow__pane') as HTMLElement;
const project = { json: { members: [memberOf(OTHER_ID, 'Alex Martin')], sharing_group_label: null } };

/** The output « Facture » of s2, the sheet of s2 with the steps that receive it, the sheet of a step that ticked it. */
const invoice = documentOf('o1', { step_id: 's2', name: 'Facture' });
const producedFor = (...stepIds: string[]) => ({ json: sheetViewOf({ outputs: [{ document: invoice, used_by: stepIds.map((stepId) => documentUseOf('o1', stepId)) }] }) });
const receivedBy = (stepId: string, overrides: Partial<ReturnType<typeof sheetViewOf>> = {}) =>
  ({ json: sheetViewOf({ step_id: stepId, inputs: [inputOf(invoice, { used: true, use: documentUseOf('o1', stepId) })], ...overrides }) });
/** Waits for the effects of the sheet just drawn before the caret is read: a caret taken late would be seen. */
const settled = () => act(() => new Promise<void>((resolve) => { setTimeout(resolve, 20); }));
/** The map shown with the sheet of s2 open, the sheets of the steps answered as given. */
async function showProducer(sheets: FakeRoutes, map = sampleMap()) {
  fakeApi({ 'GET /api/steps/s2/sheet': producedFor('s3'), [`GET /api/projects/${PROJECT_ID}`]: project, ...sheets });
  const shown = await showEditor(map);
  openSheetOf(shown.canvas, 's2');
  return shown;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('from the outputs of a step to the pain point of the step that receives (F-118)', () => {
  it('« Signaler un irritant dans CODE », in the outputs, opens the sheet of the step that receives, its inputs unfolded, the caret in the pain point of the document (F-118)', async () => {
    const output = documentOf('o1', { step_id: 's2', name: 'Facture' });
    const produced = sheetViewOf({ outputs: [{ document: output, used_by: [documentUseOf('o1', 's3')] }] });
    const received = sheetViewOf({ step_id: 's3', inputs: [inputOf(output, { used: true, use: documentUseOf('o1', 's3') })] });
    fakeApi({ 'GET /api/steps/s2/sheet': { json: produced }, 'GET /api/steps/s3/sheet': { json: received }, [`GET /api/projects/${PROJECT_ID}`]: project });
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    fireEvent.click(await screen.findByRole('button', { name: 'Signaler un irritant dans VIS-E003 Facture' }));
    const field = await screen.findByLabelText('Irritant sur « Facture »');
    expect(within(screen.getByTestId('sheet-panel')).getByTestId('sheet-title')).toHaveTextContent('VIS-E003');
    await vi.waitFor(() => { expect(document.activeElement).toBe(field); });
    // Asked once: the sheet opened again from the map leaves the caret alone.
    fireEvent.click(pane(canvas));
    openSheetOf(canvas, 's3');
    const again = await screen.findByLabelText('Irritant sur « Facture »');
    await settled();
    expect(document.activeElement === again).toBe(false);
  });

  it('a caret asked for a sheet that could not take it is not given to the sheet of another step that uses the same document (F-118)', async () => {
    const output = documentOf('o1', { step_id: 's2', name: 'Facture' });
    const produced = sheetViewOf({ outputs: [{ document: output, used_by: [documentUseOf('o1', 's3'), documentUseOf('o1', 's5')] }] });
    // The sheet of s3 shows no use to type in (read by someone who may not fill it): the request is forgotten there (R4-10).
    const closed = sheetViewOf({ step_id: 's3' });
    const other = sheetViewOf({ step_id: 's5', inputs: [inputOf(output, { used: true, use: documentUseOf('o1', 's5') })] });
    fakeApi({
      'GET /api/steps/s2/sheet': { json: produced }, 'GET /api/steps/s3/sheet': { json: closed }, 'GET /api/steps/s5/sheet': { json: other },
      [`GET /api/projects/${PROJECT_ID}`]: project,
    });
    const map = sampleMap();
    const { canvas } = await showEditor({ ...map, steps: [...map.steps, stepOf('s5', 'l2', 5, { code: 'VIS-E005', label: 'Archiver' })] });
    openSheetOf(canvas, 's2');
    fireEvent.click(await screen.findByRole('button', { name: 'Signaler un irritant dans VIS-E003 Facture' }));
    await screen.findByText('VIS-E003', { selector: 'code' });
    fireEvent.click(node(canvas, 's5'));
    const elsewhere = await screen.findByLabelText('Irritant sur « Facture »');
    await settled();
    expect([screen.getByTestId('sheet-title').textContent, document.activeElement === elsewhere]).toEqual(['VIS-E005 · Archiver', false]);
  });

  it('the inputs unfold for the opening that asks a pain point only, the other sections left folded: the sheet opened next from the map is folded again, and a fold by hand folds (essai J10, audit 3.2)', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': producedFor('s3'), 'GET /api/steps/s3/sheet': receivedBy('s3'), [`GET /api/projects/${PROJECT_ID}`]: project });
    const { canvas } = await showFolded(sampleMap());
    const report = async () => {
      openSheetOf(canvas, 's2');
      fireEvent.click(await screen.findByTestId('sheet-fold-outputs'));
      fireEvent.click(await screen.findByTestId('output-report-o1-s3'));
      await screen.findByLabelText('Irritant sur « Facture »');
    };
    await report();
    expect(unfoldedSections()).toEqual([false, true, false, false, false]);
    // The sheet opened again from the map is folded as any other.
    fireEvent.click(pane(canvas));
    openSheetOf(canvas, 's3');
    await screen.findByTestId('sheet-fold-inputs');
    expect([unfoldedSections(), screen.queryByLabelText('Irritant sur « Facture »')]).toEqual([[false, false, false, false, false], null]);
    // Folded by hand in the opening that unfolded it, the section folds at once.
    fireEvent.click(pane(canvas));
    await report();
    fireEvent.click(screen.getByTestId('sheet-fold-inputs'));
    expect([unfoldedSections(), screen.queryByLabelText('Irritant sur « Facture »')]).toEqual([[false, false, false, false, false], null]);
  });
});

describe('the way to the pain point, whatever the sheet that receives (R4-01, R4-10, R4-11)', () => {
  it('shows the form of a sheet that holds a free text only: its inputs, the caret in the pain point (R4-01)', async () => {
    await showProducer({ 'GET /api/steps/s3/sheet': receivedBy('s3', { content: contentOf({ free_text: 'Tout est ici' }) }) });
    fireEvent.click(await screen.findByTestId('output-report-o1-s3'));
    const field = await screen.findByLabelText('Irritant sur « Facture »');
    await vi.waitFor(() => { expect(document.activeElement).toBe(field); });
    expect(screen.getByTestId('sheet-mode-form')).toHaveAttribute('aria-checked', 'true');
  });

  it('a caret the sheet drawn had no field for is forgotten at once: its input ticked later in the same sheet leaves the caret alone (R4-10)', async () => {
    // The use was withdrawn since the sheet of the producer was read: the sheet that opens offers the document unticked.
    const unticked = { json: sheetViewOf({ step_id: 's3', inputs: [inputOf(invoice)] }) };
    await showProducer({ 'GET /api/steps/s3/sheet': unticked, 'PUT /api/steps/s3/uses/o1': receivedBy('s3') });
    fireEvent.click(await screen.findByTestId('output-report-o1-s3'));
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Utiliser « Facture »' }));
    const field = await screen.findByLabelText('Irritant sur « Facture »');
    await settled();
    expect(document.activeElement === field).toBe(false);
  });

  it('a caret asked for a sheet that could not be read is forgotten as it closes (R4-10)', async () => {
    const { canvas, notes } = await showProducer({ 'GET /api/steps/s3/sheet': [{ refused: 503, code: 'SERVICE_INDISPONIBLE', message: 'Service indisponible.' }, receivedBy('s3')] });
    fireEvent.click(await screen.findByTestId('output-report-o1-s3'));
    await vi.waitFor(() => { expect(notes).toEqual([['Service indisponible.', 'error']]); });
    fireEvent.click(pane(canvas));
    openSheetOf(canvas, 's3');
    const field = await screen.findByLabelText('Irritant sur « Facture »');
    await settled();
    expect(document.activeElement === field).toBe(false);
  });

  it('no link to a step that became an event or a gateway that joins: it holds no sheet, the map opens none (R4-11)', async () => {
    const map = sampleMap();
    const steps = [...map.steps.map((step) => (step.id === 's3' ? { ...step, type: 'end' as const } : step)), stepOf('s5', 'l2', 5, { code: 'VIS-E005', label: 'Archiver' })];
    await showProducer({ 'GET /api/steps/s2/sheet': producedFor('s3', 's5') }, { ...map, steps });
    const output = await screen.findByTestId('output-o1');
    // The step that became an end still uses the document, and is still named; only the way to its sheet is gone.
    expect(output.querySelector('.entry-details')).toHaveTextContent('utilisé par VIS-E003, VIS-E005');
    expect([...output.querySelectorAll('.report-pain')].map((link) => link.getAttribute('data-testid'))).toEqual(['output-report-o1-s5']);
  });

  it('no link to a step whose sheet the person may not fill: the help still says where a pain point is noted (audit 3.2 UX-MAJ-02)', async () => {
    const map = sampleMap();
    const steps = [...map.steps.map((step) => (step.id === 's3' ? { ...step, sheet: sheetOf({ can_fill: false }) } : step)), stepOf('s5', 'l2', 5, { code: 'VIS-E005', label: 'Archiver' })];
    await showProducer({ 'GET /api/steps/s2/sheet': producedFor('s3', 's5') }, { ...map, steps });
    const output = await screen.findByTestId('output-o1');
    expect(output.querySelector('.entry-details')).toHaveTextContent('utilisé par VIS-E003, VIS-E005');
    expect([...output.querySelectorAll('.report-pain')].map((link) => link.getAttribute('data-testid'))).toEqual(['output-report-o1-s5']);
    expect(screen.getByTestId('sheet-section-outputs')).toHaveTextContent('Un irritant sur un document se note dans la fiche de l’étape qui le reçoit.');
  });
});

describe('the sheet docked on the right (B-4-01, B-4-05, B-2.1-39, B-2.1-40)', () => {
  // The first render of React Flow in the file, under the load of the whole suite: a delay of its own (P6-T06, F-34).
  it('Enter on a step with content docks its sheet beside the canvas, in the workspace, then follows a click on another step; a click on the background closes it; the project is read once (AR-151)', { timeout: 15_000 }, async () => {
    const sent = fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, 'GET /api/steps/s3/sheet': { json: sheetViewOf({ step_id: 's3' }) }, [`GET /api/projects/${PROJECT_ID}`]: project,
    });
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    const panel = await screen.findByTestId('sheet-panel');
    // In its frame, which resizes and folds (spec 3.0 section 15.2).
    const frame = panel.closest('.side-dock');
    expect([frame?.parentElement?.classList.contains('map-workspace'), frame?.previousElementSibling?.contains(canvas)]).toEqual([true, true]);
    expect(within(panel).getByTestId('sheet-title')).toHaveTextContent('VIS-E002 · Vérifier la demande');
    expect(within(panel).getByTestId('sheet-meta')).toHaveTextContent('Couloir : Commercial');
    fireEvent.click(node(canvas, 's3'));
    await screen.findByText('VIS-E003', { selector: 'code' });
    fireEvent.click(pane(canvas));
    expect(screen.queryByTestId('sheet-panel')).toBeNull();
    expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(1);
  });

  it('a project that could not be read is read again when a sheet opens again, then kept (audit 5.5)', async () => {
    const sent = fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf() },
      [`GET /api/projects/${PROJECT_ID}`]: [{ refused: 503, code: 'SERVICE_INDISPONIBLE' }, project],
    });
    const { canvas } = await showEditor(sampleMap());
    for (let opening = 0; opening < 3; opening += 1) {
      openSheetOf(canvas, 's2');
      await screen.findByTestId('sheet-panel');
      await vi.waitFor(() => { expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`).length).toBeGreaterThan(Math.min(opening, 1)); });
      fireEvent.click(pane(canvas));
    }
    expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(2);
  });

  it('the step and its lane named in the language shown', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: project });
    await i18next.changeLanguage('en');
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    expect(await screen.findByTestId('sheet-title')).toHaveTextContent('VIS-E002 · Check the request');
    expect(await screen.findByText('Lane: Visits')).toBeInTheDocument();
  });
});

describe('a new panel for each step (B-4-06, B-4-45)', () => {
  it('what the first step left leaves at once when the second opens; the second starts from its own sheet', async () => {
    const sent = fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, 'GET /api/steps/s3/sheet': { json: sheetViewOf({ step_id: 's3' }) },
      'PUT /api/steps/s2/sheet': { json: sheetViewOf({ status: 'draft' }) }, [`GET /api/projects/${PROJECT_ID}`]: project,
    });
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    fireEvent.change(await screen.findByTestId('sheet-purpose'), { target: { value: 'Vérifier' } });
    fireEvent.click(node(canvas, 's3'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/steps/s2/sheet')).toHaveLength(1);
    });
    expect(await screen.findByTestId('sheet-purpose')).toHaveValue('');
  });
});

describe('what the sheet changes on the map (B-4-50, B-2.6-11, B-4-63)', () => {
  it('the gauge of the step follows the sheet saved, without reading the map', async () => {
    fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, 'PUT /api/steps/s2/sheet': { json: sheetViewOf({ status: 'draft', completeness: 40 }) }, [`GET /api/projects/${PROJECT_ID}`]: project,
    });
    const { canvas } = await showEditor(sampleMap(), sampleMap({ name: 'Relue' }));
    openSheetOf(canvas, 's2');
    fireEvent.change(await screen.findByTestId('sheet-purpose'), { target: { value: 'Vérifier' } });
    fireEvent.click(screen.getByTestId('sheet-close'));
    await vi.waitFor(() => {
      expect((within(node(canvas, 's2')).getByTestId('step-gauge').firstElementChild as HTMLElement).style.width).toBe('40%');
    });
    expect(screen.queryByText('Relue')).toBeNull();
  });

  it('a document ticked reads the map again, the flow then carries it', async () => {
    const near = inputOf(documentOf('d1', { name: 'Demande' }));
    fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf({ inputs: [near] }) }, [`GET /api/projects/${PROJECT_ID}`]: project,
      'PUT /api/steps/s2/uses/d1': { json: sheetViewOf({ inputs: [{ ...near, used: true, use: documentUseOf('d1', 's2') }] }) },
    });
    const carried = sampleMap({ flows: sampleMap().flows.map((flow) => (flow.id === 'f1' ? { ...flow, documents: ['Demande'] } : flow)) });
    const { canvas, editor } = await showEditor(sampleMap(), carried);
    openSheetOf(canvas, 's2');
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Utiliser « Demande »' }));
    await vi.waitFor(() => {
      expect(editor.store.getState().map?.flows.find((flow) => flow.id === 'f1')?.documents).toEqual(['Demande']);
    });
  });

});

describe('the folds of a sheet opened from the map (essai J10, AR-91)', () => {
  const sheets = () => {
    const filled = contentOf({ tools: 'Excel, ERP', frequency: 'per_case', duration_minutes: 10 });
    fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf({ content: filled }) }, 'GET /api/steps/s3/sheet': { json: sheetViewOf({ step_id: 's3' }) },
      [`GET /api/projects/${PROJECT_ID}`]: project,
    });
  };

  it('opens folded, every section, its summary in its head and the tools as pills: read at a glance; a section unfolded by hand is so for this sheet only', async () => {
    sheets();
    const { canvas } = await showFolded(sampleMap());
    openSheetOf(canvas, 's2');
    await screen.findByTestId('sheet-fold-facts');
    expect([unfoldedSections(), screen.queryByLabelText('Acteur'), screen.getByTestId('sheet-fold-all').textContent]).toEqual([[false, false, false, false, false], null, 'Tout déplier']);
    expect([screen.getByTestId('sheet-summary-facts').textContent, [...screen.getByTestId('sheet-summary-tools').querySelectorAll('.tool-pill')].map((pill) => pill.textContent)])
      .toEqual(['À chaque dossier · 10 minutes', ['Excel', 'ERP']]);
    fireEvent.click(screen.getByTestId('sheet-fold-procedure'));
    expect(unfoldedSections()).toEqual([false, false, true, false, false]);
    fireEvent.click(pane(canvas));
    openSheetOf(canvas, 's3');
    await screen.findByText('VIS-E003', { selector: 'code' });
    expect(unfoldedSections()).toEqual([false, false, false, false, false]);
  });

  it('« Tout déplier » unfolds every section and is kept for the sheets opened next, while the map stays open; « Tout replier » folds them again', async () => {
    sheets();
    const { canvas, editor } = await showFolded(sampleMap());
    openSheetOf(canvas, 's2');
    fireEvent.click(await screen.findByTestId('sheet-fold-procedure'));
    fireEvent.click(screen.getByTestId('sheet-fold-all'));
    expect([unfoldedSections(), screen.getByTestId('sheet-fold-all').textContent, editor.store.getState().sheetsUnfolded]).toEqual([[true, true, true, true, true], 'Tout replier', true]);
    fireEvent.click(pane(canvas));
    openSheetOf(canvas, 's3');
    await screen.findByText('VIS-E003', { selector: 'code' });
    expect(unfoldedSections()).toEqual([true, true, true, true, true]);
    // A section folded by hand leaves the others as they are; « Tout déplier » is offered again, and unfolds it.
    fireEvent.click(screen.getByTestId('sheet-fold-inputs'));
    expect([unfoldedSections(), screen.getByTestId('sheet-fold-all').textContent]).toEqual([[true, false, true, true, true], 'Tout déplier']);
    fireEvent.click(screen.getByTestId('sheet-fold-all'));
    fireEvent.click(screen.getByTestId('sheet-fold-all'));
    expect([unfoldedSections(), editor.store.getState().sheetsUnfolded]).toEqual([[false, false, false, false, false], false]);
  });
});

describe('one Escape on the map with the sheet open (P6-T10, B-2.5-24, B-4-04)', () => {
  it('closes the sheet and gives up the cut at once, as in the 1.x; Escape in a field of the sheet only closes it', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: project });
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    await screen.findByTestId('sheet-actor');
    fireEvent.keyDown(document.body, { key: 'x', ctrlKey: true });
    expect(clipboard.getState().content?.mode).toBe('cut');
    fireEvent.keyDown(screen.getByTestId('sheet-actor'), { key: 'Escape' });
    expect(screen.queryByTestId('sheet-panel')).toBeNull();
    expect(clipboard.getState().content?.mode).toBe('cut');
    openSheetOf(canvas, 's2');
    await screen.findByTestId('sheet-actor');
    fireEvent.keyDown(document.body, { key: 'Escape' });
    expect(screen.queryByTestId('sheet-panel')).toBeNull();
    expect(clipboard.getState().content).toBeNull();
  });
});
