// capture.test.ts
// The capture of the map: bounds without the frame, margin and extra height, pixel ratio, elements left out, the wait for the steps drawn, html-to-image called with them, the PNG of the canvas (B-2.14-03 to B-2.14-07, B-2.9-20, B-2.10-26).

import { toCanvas } from 'html-to-image';
import type { Schema } from '../../../api/client.ts';
import { flowElements, type MapNode } from '../canvas/flowElements.ts';
import { LANE_HEADER_WIDTH } from '../model/geometry/constants.ts';
import { layoutMap } from '../model/mapLayout.ts';
import { frameOf, sampleMap, streamOf } from '../../../test/maps.ts';
import { captionPosition, captureFrame, captureView, fontFaces, keptInCapture, LEFT_OUT, mapBounds, pixelRatio, pngOf, waitForSteps, type WaitClock } from './capture.ts';

vi.mock('html-to-image', () => ({ toCanvas: vi.fn() }));

const measure = (text: string) => text.length * 6;

/** The figures of a step with content, unknown but its throughput. */
const STEP_STREAM: Schema<'StepStream'> = {
  step_id: '', throughput: 120, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: null, basis: 'none', work_minutes: null, work_origin: 'none',
  lead_hours: null, lead_origin: 'none', expected_lead_hours: null, work_per_case_minutes: null, charge_hours: null, fte: null, human: true, from_draft: false,
};

describe('the bounds of the image (B-2.14-05, B-2.9-20)', () => {
  it('the lanes from the margin of their headers to the end of the map; the frame never counts', () => {
    const layout = layoutMap(sampleMap({ dissolution: frameOf(['s2']) }), 'fr', measure);
    const { nodes } = flowElements(layout);
    expect(nodes.some((node) => node.type === 'frame')).toBe(true);
    expect(mapBounds(nodes)).toEqual({ x: -LANE_HEADER_WIDTH, y: 0, width: layout.width + LANE_HEADER_WIDTH, height: layout.height });
    const frame = nodes.find((node) => node.type === 'frame');
    const wide = { ...frame, position: { x: -5000, y: -5000 }, width: 99_999, height: 99_999 } as typeof nodes[number];
    expect(mapBounds([...nodes, wide])).toEqual(mapBounds(nodes));
    expect(mapBounds([])).toBeNull();
  });

  it('with the value stream layer, its cartouches are in the image and nothing under the lanes (spec 14.9, spec 3.0 section 15.4)', () => {
    const map = sampleMap({ value_stream: streamOf({ steps: { s2: { ...STEP_STREAM, step_id: 's2' } } }) });
    const writing = { language: 'fr', workdayHours: 7, t: (key: string) => key };
    const layout = layoutMap(map, 'fr', measure, writing);
    const { nodes } = flowElements(layout);
    expect(nodes.filter((node) => node.type === 'card').map((node) => node.id)).toEqual(['card:s2']);
    expect(mapBounds(nodes)).toEqual({ x: -LANE_HEADER_WIDTH, y: 0, width: layout.width + LANE_HEADER_WIDTH, height: layout.height });
  });

  it('the box runs from the nodes furthest left and up to the ends of those furthest right and down', () => {
    const node = (x: number, y: number) => ({ id: String(x), type: 'step', position: { x, y }, width: 170, height: 64, data: {} }) as unknown as MapNode;
    expect(mapBounds([node(400, 300), node(100, 50), node(250, 120)])).toEqual({ x: 100, y: 50, width: 470, height: 314 });
  });

  it('a margin of 24 round the bounds, 20 more pixels in height, the line of the caption when asked; the view moved past the margin', () => {
    const bounds = { x: -184, y: 0, width: 1000.4, height: 360 };
    expect(captureFrame(bounds)).toEqual({ width: 1049, height: 428, transform: 'translate(208px, 24px) scale(1)' });
    expect(captureFrame({ ...bounds, y: 10 }, true)).toEqual({ width: 1049, height: 452, transform: 'translate(208px, 14px) scale(1)' });
    expect(captionPosition(bounds)).toEqual({ x: -184, y: 380 });
  });
});

describe('the resolution (B-2.14-06)', () => {
  it('twice the resolution, bounded so that neither side passes 12 000 pixels, never under half', () => {
    expect(pixelRatio(1500, 600)).toBe(2);
    expect(pixelRatio(12_000, 600)).toBe(1);
    expect(pixelRatio(600, 8000)).toBe(1.5);
    expect(pixelRatio(100_000, 600)).toBe(0.5);
  });
});

describe('the elements left out of the image (B-2.14-07, B-2.10-26)', () => {
  it('the « + » and their hover zone, the handles, the gauges and the frame; anything else is kept', () => {
    expect(LEFT_OUT).toEqual(['quick-add', 'quick-add-bridge', 'react-flow__handle', 'step-gauge', 'react-flow__node-frame']);
    for (const name of LEFT_OUT) {
      const element = document.createElement('span');
      element.className = `other ${name}`;
      expect(keptInCapture(element)).toBe(false);
    }
    const kept = document.createElement('div');
    kept.className = 'step step-label quick-add-other';
    const drawing = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    drawing.setAttribute('class', 'step-gauge');
    expect([keptInCapture(kept), keptInCapture(drawing), keptInCapture(document.createTextNode('x'))]).toEqual([true, true, true]);
  });
});

/** A clock whose frames last 16 ms and whose pauses are written down. */
function fakeClock(onFrame: (time: number) => void = () => undefined): WaitClock & { pauses: number[]; frames: () => number } {
  let time = 0;
  let frames = 0;
  const pauses: number[] = [];
  return {
    pauses,
    frames: () => frames,
    now: () => time,
    frame: () => {
      time += 16;
      frames += 1;
      onFrame(time);
      return Promise.resolve();
    },
    pause: (milliseconds) => {
      pauses.push(milliseconds);
      return Promise.resolve();
    },
  };
}

describe('the wait for the steps drawn (B-2.14-03)', () => {
  const root = (steps: number) => {
    const element = document.createElement('div');
    element.innerHTML = '<div class="react-flow__node-step"></div>'.repeat(steps);
    return element;
  };

  it('stops at the first frame where every step is drawn, then waits 50 ms', async () => {
    const element = root(1);
    const clock = fakeClock((time) => {
      if (time === 48) element.innerHTML += '<div class="react-flow__node-step"></div>'.repeat(2);
    });
    await waitForSteps(element, 3, clock);
    expect([clock.frames(), clock.pauses]).toEqual([3, [50]]);
  });

  it('gives up after 3 s when the steps never come, then still waits 50 ms', async () => {
    const clock = fakeClock();
    await waitForSteps(root(2), 3, clock);
    expect([clock.frames(), clock.pauses]).toEqual([Math.ceil(3000 / 16), [50]]);
  });
});

describe('the capture by html-to-image (B-2.14-04 to B-2.14-07, B-2.14-12)', () => {
  it('draws the view framed on the bounds, on the background of the charter, at its ratio, through the filter', async () => {
    const canvas = document.createElement('canvas');
    vi.mocked(toCanvas).mockResolvedValue(canvas);
    const view = document.createElement('div');
    const captured = await captureView(view, { x: -184, y: 0, width: 1000, height: 360 }, { background: ' #FFFFFF ', fonts: '@font-face { }', caption: false });
    expect(captured).toEqual({ canvas, width: 1048, height: 428 });
    expect(vi.mocked(toCanvas)).toHaveBeenCalledWith(view, {
      backgroundColor: '#FFFFFF', width: 1048, height: 428, pixelRatio: 2, style: { width: '1048px', height: '428px', transform: 'translate(208px, 24px) scale(1)' }, filter: keptInCapture,
      fontEmbedCSS: '@font-face { }',
    });
    await captureView(view, { x: 0, y: 0, width: 100, height: 100 }, { background: '', fonts: '', caption: true });
    expect(vi.mocked(toCanvas).mock.lastCall?.[1]).not.toHaveProperty('backgroundColor');
    expect(vi.mocked(toCanvas).mock.lastCall?.[1]?.height).toBe(192);
  });

  it('the PNG of the canvas, or a failure when the canvas gives none', async () => {
    const png = new Blob(['png'], { type: 'image/png' });
    const canvas = { toBlob: vi.fn((done: (blob: Blob | null) => void, type: string) => { done(type === 'image/png' ? png : null); }) } as unknown as HTMLCanvasElement;
    await expect(pngOf(canvas)).resolves.toBe(png);
    const empty = { toBlob: (done: (blob: Blob | null) => void) => { done(null); } } as unknown as HTMLCanvasElement;
    await expect(pngOf(empty)).rejects.toThrow();
  });
});

/** A style sheet as the browser gives it: its address and its rules, the faces of fonts among them. */
const sheetOf = (href: string | null, faces: readonly string[], others = 0) => ({
  href,
  cssRules: [...faces.map((css) => Object.create(CSSFontFaceRule.prototype, { cssText: { value: css } }) as CSSRule), ...Array.from({ length: others }, () => ({ cssText: '.a { }' }) as CSSRule)],
}) as unknown as CSSStyleSheet;

describe('the fonts of the image, read without a base address (S-5.4-04)', () => {
  it('each face of font with its files as data addresses, resolved against its sheet, each file read once; other rules and sheets of another origin left out', async () => {
    // The bytes of a file are the end of its name; the reply of jsdom has no Blob of its own, the bytes are given as such.
    const read = vi.fn((address: string) => Promise.resolve(new Response(new TextEncoder().encode(address.slice(-7)), { headers: { 'Content-Type': 'font/woff2' } })));
    const foreign = { href: 'http://localhost:4000/other.css', get cssRules(): CSSRuleList { throw new DOMException('refused', 'SecurityError'); } } as unknown as CSSStyleSheet;
    const css = await fontFaces([
      sheetOf('http://localhost/assets/index.css', ['@font-face { src: url("inter-a.woff2") format("woff2"); }', "@font-face { src: url('/assets/inter-a.woff2'), url(data:font/woff2;base64,AA==); }"], 2),
      foreign,
      sheetOf(null, ['@font-face { src: url(./b.woff2); }']),
    ], read as unknown as typeof fetch);
    expect(read.mock.calls.map(([address]) => address)).toEqual(['http://localhost/assets/inter-a.woff2', 'http://localhost:3000/b.woff2']);
    expect(css.split('\n')).toEqual([
      '@font-face { src: url("data:font/woff2;base64,YS53b2ZmMg==") format("woff2"); }',
      '@font-face { src: url("data:font/woff2;base64,YS53b2ZmMg=="), url(data:font/woff2;base64,AA==); }',
      '@font-face { src: url("data:font/woff2;base64,Yi53b2ZmMg=="); }',
    ]);
  });

  it('a face whose file cannot be read is left out, the others kept; no face gives an empty text', async () => {
    const read = vi.fn((address: string) => Promise.resolve(address.endsWith('gone.woff2') ? new Response(null, { status: 404 }) : new Response(new TextEncoder().encode('x'))));
    const css = await fontFaces([sheetOf('http://localhost/a.css', ['@font-face { src: url(gone.woff2); }', '@font-face { src: url(here.woff2); }'])], read as unknown as typeof fetch);
    expect(css).toBe('@font-face { src: url("data:application/octet-stream;base64,eA=="); }');
    expect(await fontFaces([sheetOf('http://localhost/a.css', [], 3)], read as unknown as typeof fetch)).toBe('');
  });
});
