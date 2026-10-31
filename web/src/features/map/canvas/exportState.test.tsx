// exportState.test.tsx
// The canvas while an image of it is captured: its class, every step drawn, the steps outside the frame drawn as the others, the arrow head and the line of the classification inside the view, the canvas given to the exports (annex B 2.14, B-7-20, S-4.1-08).

import { act, cleanup, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { frameOf, sampleMap, stepOf } from '../../../test/maps.ts';
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
  it('the canvas takes the state of an export while an image is captured, for a PNG or a PDF, never for a workbook', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    expect(editor.store.getState().exporting).toBeNull();
    for (const [run, shown] of [[{ format: 'png', caption: 'Classification : Interne' }, true], [{ format: 'pdf', caption: null }, true], [{ format: 'xlsx', caption: null }, false], [null, false]] as const) {
      exporting(editor, run);
      expect(canvas.classList.contains('is-exporting')).toBe(shown);
    }
  });

  it('every step is drawn, also far out of the view, and the arrow head lives in the view with the flows', async () => {
    const far = sampleMap();
    const { canvas } = await showEditor({ ...far, steps: [...far.steps, stepOf('s9', 'l2', 60, { code: 'VTE-E099' })] });
    const view = canvas.querySelector('.react-flow__viewport');
    expect(within(view as HTMLElement).getByTestId('step-VTE-E099')).toBeInTheDocument();
    expect(view?.querySelectorAll('.react-flow__node-step')).toHaveLength(5);
    expect(view?.querySelector(`marker#${ARROW_MARKER}`)).not.toBeNull();
    expect(canvas.querySelectorAll(`#${ARROW_MARKER}`)).toHaveLength(1);
  });

  it('the steps outside the frame are drawn as the others during the capture only (B-2.14-11)', async () => {
    // The frame round s2 and s4 covers s3, which was not brought back: it is outside the frame.
    const { editor, canvas } = await showEditor(sampleMap({ dissolution: frameOf(['s2', 's4']) }));
    const outside = () => ['VTE-E001', 'VTE-E002', 'VTE-E003', 'VTE-E004'].map((code) => within(canvas).getByTestId(`step-${code}`).classList.contains('is-outside'));
    expect(outside()).toEqual([false, false, true, false]);
    exporting(editor, { format: 'pdf', caption: null });
    expect(outside()).toEqual([false, false, false, false]);
    exporting(editor, null);
    expect(outside()).toEqual([false, false, true, false]);
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
