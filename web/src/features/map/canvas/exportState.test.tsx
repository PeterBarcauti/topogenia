// exportState.test.tsx
// The canvas while an image of it is captured: its class, every step drawn, the steps outside the frame drawn as the others, the rest of the map left out while a selection is copied, the arrow head and the line of the classification inside the view, the canvas given to the exports (annex B 2.14, B-7-20, S-4.1-08, lot 1 of the 3.4).

import { act, cleanup, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { frameOf, sampleMap, staircaseMap, stepOf, streamOf } from '../../../test/maps.ts';
import { keptInCapture } from '../export/capture.ts';
import { LANE_HEADER_WIDTH } from '../model/geometry/constants.ts';
import type { ExportRun } from '../state/mapStore.ts';
import { ARROW_MARKER } from './flowElements.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const exporting = (editor: Awaited<ReturnType<typeof showEditor>>['editor'], run: ExportRun | null) => {
  act(() => {
    editor.store.setState({ exporting: run });
  });
};

describe('the state of an export (B-2.14-02, B-7-20)', () => {
  it('the canvas takes the state of an export while an image is captured, for a PNG or a PDF, never for a workbook nor an image SVG, drawn from the layout', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    expect(editor.store.getState().exporting).toBeNull();
    for (const [run, shown] of [[{ format: 'png', caption: 'Classification : Interne' }, true], [{ format: 'pdf', caption: null }, true], [{ format: 'xlsx', caption: null }, false], [{ format: 'svg', caption: null }, false], [{ format: 'md', caption: null }, false], [null, false]] as const) {
      exporting(editor, run);
      expect(canvas.classList.contains('is-exporting')).toBe(shown);
    }
  });

  it('every step is drawn, also far out of the view, and the arrow head lives in the view with the flows', async () => {
    const far = sampleMap();
    const { canvas } = await showEditor({ ...far, steps: [...far.steps, stepOf('s9', 'l2', 60, { code: 'VIS-E099' })] });
    const view = canvas.querySelector('.react-flow__viewport');
    expect(within(view as HTMLElement).getByTestId('step-VIS-E099')).toBeInTheDocument();
    expect(view?.querySelectorAll('.react-flow__node-step')).toHaveLength(5);
    expect(view?.querySelector(`marker#${ARROW_MARKER}`)).not.toBeNull();
    expect(canvas.querySelectorAll(`#${ARROW_MARKER}`)).toHaveLength(1);
  });

  it('the steps outside the frame are drawn as the others during the capture only (B-2.14-11)', async () => {
    // The frame round s2 and s4 covers s3, which was not brought back: it is outside the frame.
    const { editor, canvas } = await showEditor(sampleMap({ dissolution: frameOf(['s2', 's4']) }));
    const outside = () => ['VIS-E001', 'VIS-E002', 'VIS-E003', 'VIS-E004'].map((code) => within(canvas).getByTestId(`step-${code}`).classList.contains('is-outside'));
    expect(outside()).toEqual([false, false, true, false]);
    exporting(editor, { format: 'pdf', caption: null });
    expect(outside()).toEqual([false, false, false, false]);
    exporting(editor, null);
    expect(outside()).toEqual([false, false, true, false]);
  });

  it('while a selection is copied, the other steps, their flows, labels and cards are left out of the image, and come back after', async () => {
    const figures = (stepId: string): Schema<'StepStream'> => ({
      step_id: stepId, throughput: 120, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: null, basis: 'none', work_minutes: null, work_origin: 'none',
      lead_hours: null, lead_origin: 'none', expected_lead_hours: null, work_per_case_minutes: null, charge_hours: null, fte: null, human: true, from_draft: false,
    });
    const { editor, canvas } = await showEditor(sampleMap({ value_stream: streamOf({ steps: { s2: figures('s2'), s3: figures('s3') } }) }));
    act(() => {
      editor.preferences.getState().set('showValueStream', true);
    });
    const leftOut = () => [...canvas.querySelectorAll('.is-left-out')].map((element) => element.getAttribute('data-id')).sort();
    const labels = () => [...canvas.querySelectorAll('[data-flow-id]')].map((element) => element.getAttribute('data-flow-id')).sort();
    const unnamed = () => canvas.querySelectorAll('.react-flow__node-lane.is-name-left-out').length;
    const before = labels();
    expect([leftOut(), unnamed(), before]).toEqual([[], 0, expect.arrayContaining(['f2', 'f3', 'f4'])]);
    expect(canvas.querySelectorAll('.react-flow__node-card')).toHaveLength(2);
    // s3 and s4 chosen: only the flow between them stays, with its label « oui »; the names of the lanes are not in the image.
    exporting(editor, { format: 'png', caption: 'Classification : Interne', steps: ['s3', 's4'] });
    expect(leftOut()).toEqual(['card:s2', 'f1', 'f2', 'f4', 's1', 's2']);
    expect([labels(), unnamed()]).toEqual([['f3'], 2]);
    expect(within(canvas).getByTestId('edge-label-f3')).toHaveTextContent('oui');
    exporting(editor, null);
    expect([leftOut(), labels(), unnamed()]).toEqual([[], before, 0]);
    // The whole map captured, for the PNG export or its copy: nothing left out.
    exporting(editor, { format: 'png', caption: 'Classification : Interne' });
    expect([leftOut(), labels(), unnamed()]).toEqual([[], before, 0]);
  });

});

describe('the canvas given to the exports (annex B 2.14)', () => {
  it('its element and the bounds of its lanes and steps while it is shown, none once it has left', async () => {
    const { editor, canvas } = await showEditor(sampleMap({ dissolution: frameOf(['s2']) }));
    const target = editor.canvas.target();
    expect(target?.element).toBe(canvas);
    expect(target?.bounds).toMatchObject({ x: -LANE_HEADER_WIDTH, y: 0 });
    expect(target?.bounds?.width).toBeGreaterThan(4 * 170);
    cleanup();
    expect(editor.canvas.target()).toBeNull();
  });
});

describe('the line of the classification (S-4.1-08)', () => {
  it('stands in the view under the map while the image of a PNG is captured, with the caption given', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    expect(within(canvas).queryByTestId('export-caption')).toBeNull();
    exporting(editor, { format: 'png', caption: 'Classification : Confidentiel' });
    const caption = within(canvas).getByTestId('export-caption');
    expect(caption).toHaveTextContent('Classification : Confidentiel');
    expect(caption.closest('.react-flow__viewport')).not.toBeNull();
    const bounds = editor.canvas.target()?.bounds;
    expect(caption.style.transform).toBe(`translate(${String(bounds?.x)}px, ${String((bounds?.y ?? 0) + (bounds?.height ?? 0) + 20)}px)`);
    exporting(editor, { format: 'pdf', caption: null });
    expect(within(canvas).queryByTestId('export-caption')).toBeNull();
  });
});

describe('the image of a staircase over three lanes (R8)', () => {
  it('while a staircase over three lanes is copied, the capture takes its three steps and the flows between them, nothing of the steps left, right, above or under them (R8)', async () => {
    const { editor, canvas } = await showEditor(staircaseMap());
    const view = canvas.querySelector<HTMLElement>('.react-flow__viewport');
    if (view === null) throw new Error('No view.');
    // What the capture takes: the elements its filter keeps, the inside of an element left out never read (html-to-image).
    const taken = () => {
      const kept: Element[] = [];
      const visit = (element: Element) => {
        for (const child of element.children) {
          if (!keptInCapture(child)) continue;
          kept.push(child);
          visit(child);
        }
      };
      visit(view);
      return {
        steps: kept.flatMap((element) => (element.classList.contains('react-flow__node-step') ? [element.getAttribute('data-id')] : [])).sort(),
        flows: kept.flatMap((element) => (element.classList.contains('react-flow__edge') ? [element.getAttribute('data-id')] : [])).sort(),
        frequencies: kept.filter((element) => element.getAttribute('data-testid') === 'step-frequency').map((element) => element.textContent),
      };
    };
    const all = { steps: ['above', 'below', 'c1', 'c2', 'c3', 'left', 'lowLeft', 'right'], flows: ['f1', 'f2', 'f3', 'f4', 'f5', 'f6'], frequencies: ['Hebdomadaire', 'Mensuelle'] };
    expect(taken()).toEqual(all);
    // The frequency of the step above, which hangs towards the second step, stays out with its step.
    exporting(editor, { format: 'png', caption: 'Classification : Interne', steps: ['c1', 'c2', 'c3'] });
    expect(taken()).toEqual({ steps: ['c1', 'c2', 'c3'], flows: ['f2', 'f3'], frequencies: ['Mensuelle'] });
    exporting(editor, null);
    expect(taken()).toEqual(all);
  });
});
