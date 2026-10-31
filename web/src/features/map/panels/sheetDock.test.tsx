// sheetDock.test.tsx
// The sheet opened from the map: docked on the right of the canvas, a new one for each step, the gauge of the step that follows it, the map read again after the documents, the project read once (annex B 4, B-2.1-39, AR-91).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { openSheetOf, showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { clipboard } from '../state/clipboard.ts';
import { documentOf, inputOf, memberOf, OTHER_ID, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;
const pane = (canvas: HTMLElement) => canvas.querySelector('.react-flow__pane') as HTMLElement;
const project = { json: { members: [memberOf(OTHER_ID, 'Alex Martin')], sharing_group_label: null } };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the sheet docked on the right (B-4-01, B-4-05, B-2.1-39, B-2.1-40)', () => {
  it('Enter on a step with content docks its sheet beside the canvas, in the workspace, then follows a click on another step; a click on the background closes it; the project is read once (AR-151)', async () => {
    const sent = fakeApi({
      'GET /api/steps/s2/sheet': { json: sheetViewOf() }, 'GET /api/steps/s3/sheet': { json: sheetViewOf({ step_id: 's3' }) }, [`GET /api/projects/${PROJECT_ID}`]: project,
    });
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    const panel = await screen.findByTestId('sheet-panel');
    // In its frame, which resizes and folds (spec 3.0 section 15.2).
    const frame = panel.closest('.side-dock');
    expect([frame?.parentElement?.classList.contains('map-workspace'), frame?.previousElementSibling?.contains(canvas)]).toEqual([true, true]);
    expect(within(panel).getByTestId('sheet-title')).toHaveTextContent('VTE-E002 · Vérifier la demande');
    expect(within(panel).getByTestId('sheet-meta')).toHaveTextContent('Couloir : Commercial');
    fireEvent.click(node(canvas, 's3'));
    await screen.findByText('VTE-E003', { selector: 'code' });
    fireEvent.click(pane(canvas));
    expect(screen.queryByTestId('sheet-panel')).toBeNull();
    expect(callsTo(sent, 'GET', `/api/projects/${PROJECT_ID}`)).toHaveLength(1);
  });

  it('the step and its lane named in the language shown', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: project });
    await i18next.changeLanguage('en');
    const { canvas } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    expect(await screen.findByTestId('sheet-title')).toHaveTextContent('VTE-E002 · Check the request');
    expect(await screen.findByText('Lane: Sales')).toBeInTheDocument();
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

  it('a section folded stays folded for the next sheets (AR-91)', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: project });
    const { canvas, editor } = await showEditor(sampleMap());
    openSheetOf(canvas, 's2');
    fireEvent.click(await screen.findByTestId('sheet-fold-outputs'));
    expect([editor.preferences.getState().sheetOutputsFolded, screen.queryByTestId('sheet-outputs')]).toEqual([true, null]);
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
