// mermaidRender.ts
// Draws the Mermaid diagrams the exports write in a real Chromium, with the version of Mermaid of the tests, in a page styled as the preview of the reader of reference, and measures what a reader would find cut or covered (plan 3.1, rendu Mermaid: no title cut, nothing that covers another thing).

import { join } from 'node:path';
import { chromium } from '@playwright/test';

/** What a diagram drawn shows wrong: the texts cut by their box, the things that cover one another. */
export interface Drawing {
  readonly kind: string;
  /** How many things the measure checked: none means it found nothing it knows, never a proof. */
  readonly measured: number;
  readonly cut: readonly string[];
  readonly overlaps: readonly string[];
}

const MERMAID = join(import.meta.dirname, '..', '..', 'node_modules', 'mermaid', 'dist', 'mermaid.min.js');

/**
 * The page of the preview of the reader of reference (Markdown Preview Enhanced 0.8.15 in VS Code, its theme github-light):
 * 16 px, lines 1.6 high, which a box of HTML of a diagram inherits.
 */
const PAGE = '<!DOCTYPE html><html><head><style>body { margin: 0; font-family: "Helvetica Neue", Helvetica, "Segoe UI", Arial, sans-serif; '
  + 'font-size: 16px; line-height: 1.6; word-wrap: break-word; }</style></head><body><div id="out" style="width:1100px"></div></body></html>';

/**
 * Measures a diagram in the page: run there, it reads only the drawing. `widen` shows its labels larger than they were
 * measured, as a font that differs between the measure and the display does: the case of a title cut, for the proof that the
 * measure finds it.
 */
async function measure([diagram, widen]: readonly [string, boolean]): Promise<Drawing> {
  type Box = DOMRect;
  const engine = (window as unknown as { mermaid: { render: (id: string, text: string) => Promise<{ svg: string }> } }).mermaid;
  const out = document.getElementById('out') as HTMLElement;
  const { svg } = await engine.render(`d${String(Date.now())}`, diagram);
  out.innerHTML = svg;
  const kind = out.querySelector('svg')?.getAttribute('aria-roledescription') ?? '';
  if (widen) out.insertAdjacentHTML('beforeend', '<style>#out .nodeLabel { font-size: 180% !important; }</style>');
  const box = (element: Element): Box => element.getBoundingClientRect();
  const inside = (a: Box, b: Box, slack: number) => a.left >= b.left - slack && a.right <= b.right + slack && a.top >= b.top - slack && a.bottom <= b.bottom + slack;
  const meet = (a: Box, b: Box) => Math.min(a.right, b.right) - Math.max(a.left, b.left) > 1 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 1;
  const name = (element: Element) => element.id || element.getAttribute('class') || element.tagName;
  const cut: string[] = [];
  const overlaps: string[] = [];
  const pairs = (list: readonly Element[], label: string) => {
    for (let i = 0; i < list.length; i++)
      for (let j = i + 1; j < list.length; j++) {
        const [a, b] = [list[i] as Element, list[j] as Element];
        if (meet(box(a), box(b))) overlaps.push(`${label}: ${name(a)} / ${name(b)}`);
      }
  };
  // A flowchart: each label inside its shape and never wider than its box, no two nodes, no label of a flow on a node,
  // no title of a lane on a node.
  const nodes = [...out.querySelectorAll('g.node')];
  for (const node of nodes) {
    const shape = node.querySelector('rect, polygon, circle, ellipse, path');
    const label = node.querySelector('.label div, .label text');
    if (shape === null || label === null) continue;
    const text = label.querySelector('span, p') ?? label;
    if (!inside(box(text), box(shape), 2) || label.scrollWidth > label.clientWidth + 1) cut.push(name(node));
  }
  pairs(nodes, 'nodes');
  for (const label of out.querySelectorAll('g.edgeLabel .label')) {
    if (box(label).width === 0) continue;
    for (const node of nodes) if (meet(box(label), box(node))) overlaps.push(`flow label / ${name(node)}`);
  }
  for (const title of out.querySelectorAll('g.cluster .cluster-label'))
    for (const node of nodes) {
      if (!meet(box(title), box(node))) continue;
      const [t, n] = [box(title), box(node)];
      overlaps.push(`lane title « ${title.textContent} » (${String(Math.round(t.top))}-${String(Math.round(t.bottom))}, ${String(Math.round(t.left))}-${String(Math.round(t.right))}) / ${node.textContent} (${String(Math.round(n.top))}-${String(Math.round(n.bottom))}, ${String(Math.round(n.left))}-${String(Math.round(n.right))})`);
    }
  // A pie: its legend never on its circle; its legend entries never on one another.
  const circle = out.querySelector('circle.pieOuterCircle');
  const legends = [...out.querySelectorAll('g.legend')];
  if (circle !== null) for (const legend of legends) if (meet(box(legend), box(circle))) overlaps.push(`legend / pie: ${legend.textContent}`);
  pairs(legends, 'legends');
  // A quadrant chart: its points and their names never on one another, nor on the titles of the quadrants.
  const points = [...out.querySelectorAll('g.data-point text')];
  pairs(points, 'points');
  for (const point of points) for (const title of out.querySelectorAll('g.quadrant text')) if (meet(box(point), box(title))) overlaps.push(`point / quadrant: ${point.textContent}`);
  // A journey: the block of HTML of each step and section inside its box, its height too (two lines 1.6 high did not hold
  // in 50 px: the end of the text was cut), and no entry of its legend on a box nor on another entry.
  const frames = kind !== 'journey' ? [] : [...out.querySelectorAll('foreignObject')].flatMap((object) => {
    const [frame, block] = [object.parentElement?.closest('g')?.querySelector(':scope > rect'), object.firstElementChild];
    return frame == null || block === null ? [] : [{ frame, block }];
  });
  for (const { frame, block } of frames) if (!inside(box(block), box(frame), 0.5)) cut.push(`box: ${block.textContent}`);
  const entries = [...out.querySelectorAll('text.legend')];
  for (const entry of entries) for (const { frame } of frames) if (meet(box(entry), box(frame))) overlaps.push(`legend / box: ${entry.textContent}`);
  pairs(entries, 'legends');
  // A timeline: each text inside its box.
  const periods = [...out.querySelectorAll('g.timeline-node')];
  for (const node of periods) {
    const frame = node.querySelector('path.node-bkg');
    const text = node.querySelector('text');
    if (frame !== null && text !== null && !inside(box(text), box(frame), 2)) cut.push(`period: ${text.textContent}`);
  }
  const measured = nodes.length + legends.length + points.length + frames.length + periods.length;
  return { kind, measured, cut, overlaps };
}

/** Opens Chromium once on the page of the preview, gives a function that draws and measures a diagram, closes it at the end. */
export async function withDrawings<T>(run: (draw: (diagram: string, widen?: boolean) => Promise<Drawing>) => Promise<T>): Promise<T> {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
    await page.setContent(PAGE);
    await page.addScriptTag({ path: MERMAID });
    await page.evaluate(() => {
      (window as unknown as { mermaid: { initialize: (options: object) => void } }).mermaid.initialize({ startOnLoad: false });
    });
    return await run((diagram, widen = false) => page.evaluate(measure, [diagram, widen] as const));
  } finally {
    await browser.close();
  }
}
