// svgDraw.ts
// The pieces the image SVG of a map is drawn with (plan 3.1, L7): numbers rounded, texts escaped and set on their line as the style sheet sets them, rectangles with their rounded corners, sides of a card, icons, the head of a flow drawn as a path; nothing the readers of SVG of Word or PowerPoint would not draw (no reference, no variable, no foreignObject).

import type { Point } from '../model/geometry/grid.ts';
import type { RoutedFlow } from '../model/geometry/routing/model.ts';
import type { TextFont, TextMeasure } from '../model/textSize.ts';
import type { SvgIconName, SvgIcons } from './svgIcons.ts';

/** A number of the image: two decimals at most, never a NaN nor an infinite one (0 instead). */
export const num = (value: number): string => (Number.isFinite(value) ? String(Math.round(value * 100) / 100) : '0');

/** A text inside an element or an attribute of XML. */
export const esc = (text: string): string => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;');

/** The family of the texts of the canvas, Inter first as the page draws them; the others for a reader that has no Inter. */
export const SANS = "'Inter Variable', Inter, 'Segoe UI', Arial, sans-serif";

/** The family of the codes, as the canvas writes them. */
export const MONO = "ui-monospace, 'Cascadia Mono', Consolas, monospace";

/** The ascent and the descent of Inter, in ems: the baseline of a text in its line box as the browser places it. */
const ASCENT = 0.96875;
const DESCENT = 0.2421875;

/** The baseline of a line of a height, from its top, for a text of a size. */
export const baseline = (top: number, lineHeight: number, size: number): number => top + (lineHeight - (ASCENT + DESCENT) * size) / 2 + ASCENT * size;

/** How a line is written: its font, its colour, its anchor, its family and its style. */
export interface LineStyle {
  readonly font: TextFont;
  readonly fill: string;
  readonly anchor?: 'start' | 'middle' | 'end';
  readonly family?: string;
  readonly italic?: boolean;
}

/** A line of text on its line box: `x` at its start, its middle or its end by its anchor; its content already in XML. */
export function lineOf(x: number, top: number, lineHeight: number, style: LineStyle, content: string): string {
  const anchor = style.anchor === undefined || style.anchor === 'start' ? '' : ` text-anchor="${style.anchor}"`;
  const family = style.family === undefined ? '' : ` font-family="${style.family}"`;
  const italic = style.italic === true ? ' font-style="italic"' : '';
  return `<text x="${num(x)}" y="${num(baseline(top, lineHeight, style.font.size))}" font-size="${num(style.font.size)}" font-weight="${String(style.font.weight)}" fill="${style.fill}"${anchor}${family}${italic}>${content}</text>`;
}

/** Lines cut at a number, the last one ending by an ellipsis that holds in the width, as `-webkit-line-clamp` cuts them. */
export function clamped(lines: readonly string[], max: number, width: number, measure: TextMeasure, font: TextFont): string[] {
  if (lines.length <= max) return [...lines];
  const kept = lines.slice(0, max);
  let last = `${kept[max - 1] ?? ''}…`;
  while (last.length > 1 && measure(last, font) > width) last = `${last.slice(0, -1).replace(/[\s\S]$/u, '').trimEnd()}…`;
  kept[max - 1] = last;
  return kept;
}

/** A rectangle with its corners rounded, its radii bounded by its half sizes as the browser bounds them. */
export function rectOf(x: number, y: number, width: number, height: number, radius: number, attributes: string, radiusY = radius): string {
  const rx = Math.max(0, Math.min(radius, width / 2));
  const ry = Math.max(0, Math.min(radiusY, height / 2));
  return `<rect x="${num(x)}" y="${num(y)}" width="${num(Math.max(0, width))}" height="${num(Math.max(0, height))}" rx="${num(rx)}" ry="${num(ry)}" ${attributes}/>`;
}

/**
 * The left border of a card of a height, its corners of a radius, as the browser paints a border of 3 px against the top
 * and bottom borders of `side` px: the band along the left side, following the rounding of its corners on the inside and
 * the outside, ending where the corner turns to the top and bottom borders (at the angle of their widths). Of a pill, a crescent.
 */
export function stripeOf(x: number, y: number, height: number, radius: number, side: number, width = 3): string {
  const outer = Math.min(radius, height / 2);
  const [rx, ry] = [Math.max(0, outer - width), Math.max(0, outer - side)];
  const [top, bottom, centre] = [y + outer, y + height - outer, x + outer];
  const angle = Math.atan2(width, side);
  const [cos, sin] = [Math.cos(angle), Math.sin(angle)];
  const point = (along: number, across: number, upward: boolean, cy: number): string => `${num(centre - along * cos)} ${num(cy + (upward ? -1 : 1) * across * sin)}`;
  return `M${point(outer, outer, true, top)}A${num(outer)} ${num(outer)} 0 0 0 ${num(x)} ${num(top)}V${num(bottom)}A${num(outer)} ${num(outer)} 0 0 0 ${point(outer, outer, false, bottom)}`
    + `L${point(rx, ry, false, bottom)}A${num(rx)} ${num(ry)} 0 0 1 ${num(x + width)} ${num(bottom)}V${num(top)}A${num(rx)} ${num(ry)} 0 0 1 ${point(rx, ry, true, top)}Z`;
}

/** An icon of the canvas, its square of `size` at a point, its strokes in a colour and a width of its units of 24. */
export const iconOf = (icons: SvgIcons, name: SvgIconName, x: number, y: number, size: number, stroke: string, width = 2): string =>
  `<g transform="translate(${num(x)} ${num(y)}) scale(${num(size / 24)})" fill="none" stroke="${stroke}" stroke-width="${num(width)}" stroke-linecap="round" stroke-linejoin="round">${icons[name]}</g>`;

/** The direction a flow arrives in at its end: the tangent of its last segment, upward for a loop drawn under its row. */
function arrival(route: RoutedFlow): { tip: Point; dx: number; dy: number } | null {
  const tip = route.points.at(-1);
  if (tip === undefined) return null;
  if (route.curve !== null) return { tip, dx: 0, dy: tip.y < route.curve.low ? -1 : 1 };
  const before = [...route.points].reverse().find((point) => point.x !== tip.x || point.y !== tip.y);
  if (before === undefined) return null;
  const length = Math.hypot(tip.x - before.x, tip.y - before.y);
  return { tip, dx: (tip.x - before.x) / length, dy: (tip.y - before.y) / length };
}

/**
 * The head of a flow, as the marker of the canvas draws it (polyline -5,-4 0,0 -5,4 of a box of 20 units in 18 × 1.6
 * pixels): a path of its own, filled and stroked, its point on the end of the flow.
 */
export function headOf(route: RoutedFlow, colour: string): string {
  const end = arrival(route);
  if (end === null) return '';
  const scale = (18 * 1.6) / 20;
  const at = (back: number, side: number): string =>
    `${num(end.tip.x - end.dx * back * scale - end.dy * side * scale)} ${num(end.tip.y - end.dy * back * scale + end.dx * side * scale)}`;
  return `<path class="head" d="M${at(5, -4)}L${at(0, 0)}L${at(5, 4)}Z" fill="${colour}" stroke="${colour}" stroke-width="${num(scale)}" stroke-linejoin="round" stroke-linecap="round"/>`;
}
