// copyImage.test.ts
// The map, or the steps of a selection, copied to the clipboard as an image: the capture of the PNG export with its line of classification, bounded to 8 000 px a side, the selection framed on its steps and the flows between them (R8), a refusal said with the PNG export offered, nothing while another export runs (lot 1 of the 3.4, ADR 109).

import { act } from '@testing-library/react';
import { toCanvas } from 'html-to-image';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { captureDownloads } from '../../../../test/downloads.ts';
import { showEditor } from '../../../../test/editor.tsx';
import { fakeApi } from '../../../../test/fakeApi.ts';
import { laneOf, mapOf, PROJECT_ID, sampleMap, staircaseMap, stepOf } from '../../../../test/maps.ts';
import { CAPTION_HEIGHT, CAPTURE_MARGIN, EXTRA_HEIGHT } from '../../export/capture.ts';
import { COLUMN_WIDTH, STEP_HEIGHT, STEP_WIDTH, UNDER_STEP } from '../../model/geometry/constants.ts';
import { copyMapImage } from './copyImage.ts';
import { exportMap } from './exports.ts';

vi.mock('html-to-image', () => ({ toCanvas: vi.fn() }));

const PNG = new Blob(['png'], { type: 'image/png' });

/** What the canvas showed when it was captured: the line under the map, the elements left out of the image. */
interface Seen {
  readonly caption: string | null;
  readonly leftOut: readonly string[];
}

type Part = Blob | Promise<Blob>;

class FakeItem {
  constructor(readonly parts: Record<string, Part>) {}
}

let downloads: ReturnType<typeof captureDownloads>;
let seen: Seen[];
let written: Blob[];

/** The clipboard of the browser: it waits for the image of each item, as Chromium does; refuses when told. */
function giveClipboard(refuses = false) {
  const write = vi.fn(async (items: readonly FakeItem[]) => {
    if (refuses) throw new DOMException('Refused.', 'NotAllowedError');
    for (const item of items) written.push(await (item.parts['image/png'] ?? Promise.reject(new Error('No image.'))));
  });
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { write } });
  return write;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  downloads = captureDownloads();
  seen = [];
  written = [];
  vi.stubGlobal('ClipboardItem', FakeItem);
  vi.mocked(toCanvas).mockReset().mockImplementation((view: HTMLElement) => {
    const canvas = view.closest('[data-testid="map-canvas"]');
    const leftOut = [...(canvas?.querySelectorAll('.is-left-out') ?? [])].map((element) => element.getAttribute('data-id') ?? element.getAttribute('data-testid') ?? '');
    seen.push({ caption: canvas?.querySelector('[data-testid="export-caption"]')?.textContent ?? null, leftOut });
    return Promise.resolve({ toBlob: (done: (blob: Blob) => void) => { done(PNG); }, toDataURL: () => 'data:,' } as unknown as HTMLCanvasElement);
  });
});

afterEach(() => {
  downloads.restore();
  Reflect.deleteProperty(navigator, 'clipboard');
  vi.unstubAllGlobals();
});

/** The editor of a map, its project « Confidentiel » readable. */
async function copying(map = sampleMap()) {
  fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'confidential', version: 4 } } });
  return showEditor(map);
}

/** The options html-to-image was last given. */
const lastOptions = () => vi.mocked(toCanvas).mock.lastCall?.[1];

/** Where a node of the canvas stands, as React Flow places it. */
function placeOf(canvas: HTMLElement, id: string): { x: number; y: number } {
  const transform = canvas.querySelector<HTMLElement>(`.react-flow__node[data-id="${id}"]`)?.style.transform ?? '';
  const [x, y] = (/translate\((-?[\d.]+)px,\s?(-?[\d.]+)px\)/.exec(transform)?.slice(1) ?? []).map(Number);
  return { x: x ?? Number.NaN, y: y ?? Number.NaN };
}

describe('the copy of the map as an image', () => {
  it('copies the whole map with its line of classification, in the frame of the PNG export', async () => {
    const write = giveClipboard();
    const { editor, canvas, notes } = await copying();
    await act(() => copyMapImage(editor));
    expect([written, write.mock.calls.length]).toEqual([[PNG], 1]);
    expect(seen).toEqual([{ caption: 'Classification : Confidentiel', leftOut: [] }]);
    const copied = lastOptions();
    await act(() => exportMap(editor, 'png'));
    const exported = lastOptions();
    expect([copied?.width, copied?.height, copied?.style?.transform, copied?.pixelRatio]).toEqual([exported?.width, exported?.height, exported?.style?.transform, exported?.pixelRatio]);
    expect(copied?.pixelRatio).toBe(2);
    expect(notes).toEqual([
      ['Copie de l’image…', undefined, 'export'], ['Image copiée : la coller (Ctrl+V) dans un courriel ou une diapositive.', 'success', 'export'],
      ['Export en cours…', undefined, 'export'], ['Export terminé : le fichier est dans le dossier Téléchargements.', 'success', 'export'],
    ]);
    expect([editor.store.getState().exporting, canvas.classList.contains('is-exporting')]).toEqual([null, false]);
  });

  it('a map larger than 4 000 px is copied under 8 000 px a side, the PNG export keeping 12 000', async () => {
    giveClipboard();
    // 41 columns: some 9 000 px wide.
    const { editor } = await copying(mapOf({ lanes: [laneOf('l1', 0)], steps: [stepOf('s1', 'l1', 0), stepOf('s2', 'l1', 40)] }));
    await act(() => copyMapImage(editor));
    const copied = lastOptions();
    await act(() => exportMap(editor, 'png'));
    const exported = lastOptions();
    expect(copied?.width).toBeGreaterThan(8_000);
    expect((copied?.width ?? 0) * (copied?.pixelRatio ?? 0)).toBeCloseTo(8_000, 6);
    expect((exported?.width ?? 0) * (exported?.pixelRatio ?? 0)).toBeCloseTo(12_000, 6);
    expect([copied?.width, copied?.height]).toEqual([exported?.width, exported?.height]);
    expect(written).toEqual([PNG]);
  });

});

describe('a copy refused or left alone', () => {
  it('a refusal says so and offers the PNG export, which runs', async () => {
    const { editor, notes } = await copying();
    const notify = vi.spyOn(editor, 'notify');
    // A browser without ClipboardItem: nothing is captured for nothing.
    vi.stubGlobal('ClipboardItem', undefined);
    await act(() => copyMapImage(editor));
    expect([seen, notes.at(-1)]).toEqual([[], ['L’image n’a pas pu être copiée.', 'error', 'export']]);
    // A browser that refuses the writing.
    vi.stubGlobal('ClipboardItem', FakeItem);
    giveClipboard(true);
    await act(() => copyMapImage(editor));
    expect([written, notes.at(-1)]).toEqual([[], ['L’image n’a pas pu être copiée.', 'error', 'export']]);
    const action = notify.mock.lastCall?.[2];
    expect(action?.label).toBe('Exporter en PNG');
    downloads.files.length = 0;
    await act(async () => {
      action?.run();
      await vi.waitFor(() => {
        expect(downloads.files.map((file) => file.name)).toEqual([expect.stringMatching(/^VIS_Visites_.*\.png$/)]);
      });
    });
    expect(editor.store.getState().exporting).toBeNull();
  });

  it('nothing while another export runs', async () => {
    const write = giveClipboard();
    const { editor, notes } = await copying();
    act(() => {
      editor.store.setState({ exporting: { format: 'xlsx', caption: null } });
    });
    await act(() => copyMapImage(editor));
    await act(() => copyMapImage(editor, ['s2']));
    expect([write.mock.calls.length, vi.mocked(toCanvas).mock.calls.length, notes]).toEqual([0, 0, []]);
    expect(editor.store.getState().exporting).toEqual({ format: 'xlsx', caption: null });
  });
});

describe('the copy of a selection as an image', () => {
  it('the copy of a staircase over three lanes is framed tight on its steps and the frequency under the last one, not on their lanes (R8)', async () => {
    giveClipboard();
    const { editor, canvas } = await copying(staircaseMap());
    const whole = editor.canvas.target()?.bounds ?? { x: 0, y: 0, width: 0, height: 0 };
    const [c1, c3] = [placeOf(canvas, 'c1'), placeOf(canvas, 'c3')];
    await act(() => copyMapImage(editor, ['c1', 'c2', 'c3']));
    const frame = lastOptions();
    const height = c3.y + STEP_HEIGHT + UNDER_STEP.frequencyTop + UNDER_STEP.frequencyHeight - c1.y;
    expect([frame?.width, frame?.height, frame?.style?.transform]).toEqual([
      Math.ceil(2 * COLUMN_WIDTH + STEP_WIDTH + 2 * CAPTURE_MARGIN), Math.ceil(height + 2 * CAPTURE_MARGIN + EXTRA_HEIGHT + CAPTION_HEIGHT),
      `translate(${String(CAPTURE_MARGIN - c1.x)}px, ${String(CAPTURE_MARGIN - c1.y)}px) scale(1)`,
    ]);
    expect(seen[0]?.caption).toBe('Classification : Confidentiel');
    expect([...seen[0]?.leftOut ?? []].sort()).toEqual(['above', 'below', 'f1', 'f4', 'f5', 'f6', 'left', 'lowLeft', 'right']);
    expect(written).toEqual([PNG]);
    // The canvas gives the bounds of the whole map again once the copy is done.
    expect(editor.canvas.target()?.bounds).toEqual(whole);
  });
});
