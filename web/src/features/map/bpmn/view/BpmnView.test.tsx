// BpmnView.test.tsx
// The map shown as a diagram BPMN 2.0 (ADR 106): its copy read for the view without any event, its steps drawn as forms of their kind, a step clicked opening its sheet read only, a detailed step diving into its detail with the view kept, opened where the map showed its steps and given back to it, the details switched off and kept by the browser, the names of the documents each in its place (R8), Alt+B back to the map, the .bpmn exported from its dialog.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PMAP_FORMAT, type PmapDocument } from '../../../../shared/pmap.ts';
import { captureDownloads } from '../../../../test/downloads.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../../test/editor.tsx';
import { MAP_ID, PROJECT_ID, sampleMap, stepOf } from '../../../../test/maps.ts';
import { estimateMeasure } from '../../model/textSize.ts';
import { createMapEditor } from '../../state/editor.ts';
import { tellView } from '../../state/viewHandover.ts';
import { bpmnView } from '../bpmnState.ts';

/** The copy the host gives for the sample map: its steps by their codes, a document of the second used by the third with a pain point. */
const copy: PmapDocument = {
  format: PMAP_FORMAT, version: '1.5', project: { code: 'VIS', name: 'Visites', language: 'fr' },
  map: {
    lanes: [{ id: 'VIS-C01', name: 'Commercial' }, { id: 'VIS-C02', name: 'Comptabilité', type: 'department' }],
    steps: [
      { id: 'VIS-E001', lane: 'VIS-C01', label: 'Demande reçue', type: 'start', column: 0 },
      { id: 'VIS-E002', lane: 'VIS-C01', label: 'Vérifier la demande', column: 1, sub_map: { lanes: [{ id: 'VIS-C09', name: 'Commercial' }], steps: [{ id: 'VIS-E009', lane: 'VIS-C09', label: 'Lire', column: 0 }] },
        sheet: { outputs: [{ id: 'VIS-E002-S1', name: 'Bon de commande' }] } },
      { id: 'VIS-E003', lane: 'VIS-C01', label: 'Conforme ?', type: 'decision', column: 2, sheet: { uses: [{ document: 'VIS-E002-S1', pain_point: 'Incomplet' }] } },
      { id: 'VIS-E004', lane: 'VIS-C02', label: 'Facture émise', type: 'end', column: 3 },
    ],
    flows: [{ source: 'VIS-E001', target: 'VIS-E002' }, { source: 'VIS-E002', target: 'VIS-E003' }, { source: 'VIS-E003', target: 'VIS-E004', label: 'oui' }, { source: 'VIS-E003', target: 'VIS-E002', label: 'non' }],
  },
};

const map = () => {
  const base = sampleMap();
  return { ...base, steps: base.steps.map((step) => (step.id === 's2' ? stepOf('s2', 'l1', 1, { label: 'Vérifier la demande', sub_map_id: 'm2', sub_map_empty: false }) : step)) };
};

function arrange(storage = memoryStorage(), given: PmapDocument = copy) {
  const sent = fakeApi({
    [`GET /api/maps/${MAP_ID}/pmap`]: { text: JSON.stringify(given) },
    'GET /api/steps/s2/sheet': { refused: 404, code: 'INTROUVABLE' },
    [`GET /api/projects/${PROJECT_ID}`]: { json: { classification: 'internal', members: [], sharing_group_label: null } },
  });
  const navigate = vi.fn();
  const editor = createMapEditor({ source: sourceOf(map()), storage, navigate });
  renderMapScreen({ mapId: MAP_ID, editor });
  return { sent, navigate, editor, storage };
}

/** The wait for the drawing: the first one loads the layout of the view cold, beyond a second when the whole suite runs. */
const DRAWN = { timeout: 5000 };

/** The move and the zoom of the canvas shown, as React Flow writes them. */
const viewportOf = (): string => document.querySelector<HTMLElement>('.react-flow__viewport')?.style.transform ?? '';

/**
 * The box of the name of a document drawn: each line centred on the x of its text, as wide as measured in the small font
 * of the view (the estimate, as in a page without canvas); its lines, and the whole name given on hover.
 */
function nameBox(group: HTMLElement): { left: number; right: number; lines: string[]; title: string } {
  const text = group.querySelector('text');
  const lines = [...(text?.querySelectorAll('tspan') ?? [])].map((line) => line.textContent);
  const x = Number(text?.getAttribute('x'));
  const half = Math.max(...lines.map((line) => estimateMeasure(line, { size: 11, weight: 400 }))) / 2;
  return { left: x - half, right: x + half, lines, title: group.querySelector('title')?.textContent ?? '' };
}

beforeEach(() => {
  tellView('none', 'map', () => null);
  bpmnView.setState({ open: true });
});

afterEach(() => {
  bpmnView.setState({ open: false });
});

describe('the view of a map in BPMN 2.0', () => {
  it('reads the copy of the map for the view, and draws each step as a form of its kind', async () => {
    const { sent } = arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(screen.queryByTestId('map-canvas')).toBeNull();
    expect(callsTo(sent, 'GET', `/api/maps/${MAP_ID}/pmap`).map((call) => call.query.get('purpose'))).toEqual(['view']);
    expect(screen.getAllByTestId('bpmn-node').map((node) => node.getAttribute('data-kind'))).toEqual(['startEvent', 'subProcess', 'exclusiveGateway', 'endEvent']);
    expect(screen.getAllByTestId('bpmn-lane')).toHaveLength(2);
    expect(screen.getByTestId('bpmn-badge')).toHaveTextContent('BPMN 2.0 diagram · read only');
  });

  it('opens the sheet of a step clicked, or reached by Enter', async () => {
    const { editor } = arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    const step = screen.getAllByTestId('bpmn-node').find((node) => node.getAttribute('data-step') === 'VIS-E002');
    fireEvent.click(step as Element);
    expect(editor.store.getState().openSheet).toBe('s2');
    act(() => { editor.store.setState({ openSheet: null }); });
    fireEvent.keyDown(step as Element, { key: 'Enter' });
    expect(editor.store.getState().openSheet).toBe('s2');
  });

  it('dives into the detail of a detailed step by a double click, the view kept', async () => {
    const { navigate } = arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    fireEvent.doubleClick(screen.getAllByTestId('bpmn-node').find((node) => node.getAttribute('data-step') === 'VIS-E002') as Element);
    expect(navigate).toHaveBeenCalledWith(`/maps/m2`);
    expect(bpmnView.getState().open).toBe(true);
  });

  it('draws the documents and the note of an open pain point, and hides them when the details are switched off, the choice kept', async () => {
    const { storage } = arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    // The document drawn once, near the step that makes it: the decision that uses it is a gateway, which reads no data in BPMN;
    // its pain point is noted on the step that makes the document.
    expect([screen.getAllByTestId('bpmn-data').length, screen.getAllByTestId('bpmn-note').length]).toEqual([1, 1]);
    await userEvent.click(screen.getByTestId('bpmn-toggle-details'));
    await waitFor(() => { expect(screen.queryAllByTestId('bpmn-data')).toHaveLength(0); });
    expect(screen.queryAllByTestId('bpmn-note')).toHaveLength(0);
    expect(storage.written.get('app.bpmnShowDetails')).toBe('0');
  });

  it('comes back to the map by Alt+B and by its button', async () => {
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    await userEvent.keyboard('{Alt>}b{/Alt}');
    await screen.findByTestId('map-canvas');
    expect(bpmnView.getState().open).toBe(false);
    act(() => { bpmnView.setState({ open: true }); });
    await userEvent.click(await screen.findByTestId('bpmn-close'));
    await screen.findByTestId('map-canvas');
    // From the map, Alt+B pressed then released opens the view and leaves it open: the view, drawn on the press, takes
    // no release for a new Alt+B (seen by the journey of the 3.3 in a real browser, the Alt key still held).
    await userEvent.keyboard('{Alt>}b{/Alt}');
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(bpmnView.getState().open).toBe(true);
  });

  it('exports the .bpmn from its dialog: the copy read as an export, the file named by the rule of the exports', async () => {
    const downloads = captureDownloads();
    try {
      const { sent } = arrange();
      await userEvent.click(await screen.findByTestId('bpmn-export-open'));
      const dialog = await screen.findByTestId('export-bpmn');
      expect(within(dialog).getByTestId('export-bpmn-subprocesses')).toBeChecked();
      await userEvent.click(within(dialog).getByTestId('export-bpmn-confirm'));
      await waitFor(() => { expect(downloads.files).toHaveLength(1); });
      expect(callsTo(sent, 'GET', `/api/maps/${MAP_ID}/pmap`).map((call) => call.query.get('purpose'))).toEqual(['view', 'bpmn']);
      const file = downloads.files[0];
      expect(file?.name).toMatch(/^VIS_Visites_\d{4}-\d{2}-\d{2}\.bpmn$/);
      const text = await file?.content.text();
      expect(text).toContain('<bpmn:subProcess id="Step_VIS-E002"');
      expect(text).toContain('Classification: Internal');
    } finally {
      downloads.restore();
    }
  });
});

describe('the view in BPMN 2.0 read only, and by the keyboard (audit of the copy 64, P1-02, P1-03, P2-02)', () => {
  const nodeOf = (code: string): Element => screen.getAllByTestId('bpmn-node').find((node) => node.getAttribute('data-step') === code) as Element;

  it('draws a detailed step as a sub-process from the map, the copy of the view carrying no level below it', async () => {
    // Audit of the copy 64, P2-03: the host reads the map alone for the view; the map says which steps are detailed.
    const alone = JSON.parse(JSON.stringify(copy, (key, value: unknown) => (key === 'sub_map' ? undefined : value))) as PmapDocument;
    const { navigate } = arrange(memoryStorage(), alone);
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(screen.getAllByTestId('bpmn-node').map((node) => node.getAttribute('data-kind'))).toEqual(['startEvent', 'subProcess', 'exclusiveGateway', 'endEvent']);
    fireEvent.doubleClick(nodeOf('VIS-E002'));
    expect(navigate).toHaveBeenCalledWith('/maps/m2');
  });

  it('writes the label of an event and of a gateway centred under its form, not from its middle to the right', async () => {
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    for (const code of ['VIS-E001', 'VIS-E003', 'VIS-E004']) {
      const texts = nodeOf(code).querySelectorAll('text');
      const label = texts[texts.length - 1];
      expect(label?.getAttribute('text-anchor')).toBe('middle');
      expect(label?.getAttribute('dominant-baseline')).toBe('hanging');
    }
  });

  it('opens a sheet by Space as by Enter, dives by Alt+Down into a detailed step, opens nothing for a start or an end', async () => {
    const { editor, navigate } = arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    fireEvent.keyDown(nodeOf('VIS-E002'), { key: ' ' });
    expect(editor.store.getState().openSheet).toBe('s2');
    fireEvent.click(nodeOf('VIS-E001'));
    expect(editor.store.getState().openSheet).toBeNull();
    fireEvent.keyDown(nodeOf('VIS-E004'), { key: 'Enter' });
    expect(editor.store.getState().openSheet).toBeNull();
    fireEvent.keyDown(nodeOf('VIS-E004'), { key: 'ArrowDown', altKey: true });
    expect(navigate).not.toHaveBeenCalled();
    fireEvent.keyDown(nodeOf('VIS-E002'), { key: 'ArrowDown', altKey: true });
    expect(navigate).toHaveBeenCalledWith('/maps/m2');
    expect(nodeOf('VIS-E002').getAttribute('aria-keyshortcuts')).toBe('Enter Space Alt+ArrowDown');
  });

  it('writes the words of the .bpmn in the language the map is shown in, not in the language of the interface', async () => {
    const downloads = captureDownloads();
    try {
      const { editor } = arrange();
      await screen.findByTestId('bpmn-drawing', {}, DRAWN);
      act(() => { editor.store.getState().show({ language: 'fr', origin: 'fr', locked: false }); });
      await userEvent.click(screen.getByTestId('bpmn-export-open'));
      await userEvent.click(within(await screen.findByTestId('export-bpmn')).getByTestId('export-bpmn-confirm'));
      await waitFor(() => { expect(downloads.files).toHaveLength(1); });
      const text = await downloads.files[0]?.content.text();
      expect(text).toContain('Classification : Interne');
      expect(text).not.toContain('Internal');
    } finally {
      downloads.restore();
    }
  });

  it('leaves on the ribbon over the view only the level and the display: nothing that changes the map', async () => {
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    const ribbon = screen.getByTestId('ribbon');
    expect([...ribbon.querySelectorAll('[data-testid^="ribbon-group-"]')].map((group) => group.getAttribute('data-testid'))).toEqual(['ribbon-group-level', 'ribbon-group-display']);
    expect(screen.queryByTestId('ribbon-undo')).toBeNull();
    expect(screen.getByTestId('ribbon-bpmn')).toBeInTheDocument();
  });

  it('keeps the keys that change nothing (the ribbon folded, the assistant), runs none that changes the map', async () => {
    const { editor, sent } = arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    await userEvent.keyboard('{Control>}{F1}{/Control}');
    await screen.findByTestId('ribbon-folded');
    await userEvent.keyboard('{Control>}i{/Control}');
    expect(editor.assistant.getState().open).toBe(true);
    await userEvent.keyboard('{Control>}{Shift>}g{/Shift}{/Control}{Control>}z{/Control}{Control>}a{/Control}{Delete}');
    expect(sent.filter((call) => call.method !== 'GET')).toEqual([]);
    expect(editor.store.getState().selection).toEqual({ steps: [], flows: [] });
  });
});

describe('the place of the view in BPMN 2.0 (trial of the 3.3)', () => {
  it('opens at its start at zoom 1, not framed whole, and frames the whole diagram on « Show all »', async () => {
    tellView('another-map', 'map', () => ({ code: 'VIS-E003', page: { x: 400, y: 300 }, zoom: 0.5 }));
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(viewportOf()).toBe('translate(16px,16px) scale(1)');
    expect(screen.getByTestId('bpmn-fit')).toBeEnabled();
  });

  it('opens where the map showed its steps, at its zoom, and gives them back to the map where it shows them', async () => {
    tellView(MAP_ID, 'map', () => ({ code: 'VIS-E003', page: { x: 400, y: 300 }, zoom: 0.5 }));
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(viewportOf()).toMatch(/scale\(0\.5\)$/);
    expect(viewportOf()).not.toBe('translate(16px,16px) scale(0.5)');
    // The screen of a test has no size: the view in BPMN tells no step, and the map, opened again, would start at its start.
    tellView(MAP_ID, 'bpmn', () => ({ code: 'VIS-E002', page: { x: 300, y: 200 }, zoom: 1.5 }));
    await userEvent.keyboard('{Alt>}b{/Alt}');
    await screen.findByTestId('map-canvas');
    await waitFor(() => { expect(viewportOf()).toMatch(/scale\(1\.5\)$/); });
  });
});

describe('« Show all » of the view in BPMN 2.0 (arbitration of the lot 1 of the 3.4)', () => {
  /** A view zoomed out far from the start: what the framing must leave, never larger than 100 %. */
  const zoomedOut = () => { tellView(MAP_ID, 'map', () => ({ code: 'VIS-E003', page: { x: 400, y: 300 }, zoom: 0.5 })); };
  const scaleOf = (): number => Number(/scale\(([\d.]+)\)$/.exec(viewportOf())?.[1] ?? Number.NaN);

  it('Shift+1 frames the whole diagram, as its button does, never past 100 %', async () => {
    zoomedOut();
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(scaleOf()).toBe(0.5);
    await userEvent.keyboard('{Shift>}1{/Shift}');
    await waitFor(() => { expect(scaleOf()).toBeGreaterThan(0.5); }, DRAWN);
    expect(scaleOf()).toBeLessThanOrEqual(1);
    const byKey = viewportOf();
    act(() => { tellView(MAP_ID, 'bpmn', () => null); });
    fireEvent.click(screen.getByTestId('bpmn-fit'));
    await waitFor(() => { expect(scaleOf()).toBeLessThanOrEqual(1); }, DRAWN);
    expect(viewportOf()).toBe(byKey);
  });

  it('the button Show all gives its shortcut in its title, as the one of the map', async () => {
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    expect(screen.getByTestId('bpmn-fit')).toHaveAttribute('title', 'Fit the whole diagram in the window (Shift+1)');
  });

  it('Shift+! on an English keyboard frames it too', async () => {
    zoomedOut();
    arrange();
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    await userEvent.keyboard('{Shift>}!{/Shift}');
    await waitFor(() => { expect(scaleOf()).toBeGreaterThan(0.5); }, DRAWN);
    expect(scaleOf()).toBeLessThanOrEqual(1);
  });
});

describe('the names of the documents under the steps (R8)', () => {
  it('writes the names of two documents side by side each in its place, on two lines at most, the whole name on hover; none runs on a note (R8)', async () => {
    const steps = copy.map.steps ?? [];
    const two: PmapDocument = { ...copy, map: { ...copy.map, steps: steps.map((step) => (step.id !== 'VIS-E002' ? { ...step, sheet: {} } : { ...step, sheet: { outputs: [
      { id: 'VIS-E002-S1', name: 'Factures fournisseurs du mois écoulé', medium: 'software' }, { id: 'VIS-E002-S2', name: 'Factures clients archivées' },
    ] } })) } };
    arrange(memoryStorage(), two);
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    const boxes = screen.getAllByTestId('bpmn-data').map(nameBox).sort((a, b) => a.left - b.left);
    const [first, second] = boxes;
    if (first === undefined || second === undefined) throw new Error('Two documents are drawn.');
    expect(first.right).toBeLessThanOrEqual(second.left);
    expect(boxes.map((box) => box.title)).toEqual(['Factures fournisseurs du mois écoulé', 'Factures clients archivées']);
    expect(boxes.map((box) => [box.lines.length <= 2, box.lines.at(-1)?.endsWith('…')])).toEqual([[true, true], [true, true]]);
  });

  it('writes the name of a document clear of the note on its right (R8)', async () => {
    const steps = copy.map.steps ?? [];
    const long: PmapDocument = { ...copy, map: { ...copy.map, steps: steps.map((step) => (step.id !== 'VIS-E002' ? step : { ...step, sheet: { outputs: [{ id: 'VIS-E002-S1', name: 'Récapitulatifs des commandes' }] } })) } };
    arrange(memoryStorage(), long);
    await screen.findByTestId('bpmn-drawing', {}, DRAWN);
    const [box] = screen.getAllByTestId('bpmn-data').map(nameBox);
    const note = Number(screen.getByTestId('bpmn-note').querySelector('.bpmn-note-back')?.getAttribute('x'));
    expect(box?.right).toBeLessThanOrEqual(note);
    expect(box?.title).toBe('Récapitulatifs des commandes');
  });
});
