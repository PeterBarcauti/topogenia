// svg.ts
// The image SVG of a map (plan 3.1, L7): the map redrawn from its layout as the canvas shows it in its state of export, lanes with their margin, flows with their heads and labels, steps with what hangs on them, the value stream under them, the marks of the layer « Analysis » when asked (its handoffs in their colour and thickness), the line of the classification; texts, paths and shapes only (no foreignObject, no variable, no script), Inter carried in the image; the copy of the map in its metadata when the person asks for it.

import { EDGE_LABEL, EDGE_STROKE, HANDOFF_STROKE, LANE_HEADER_WIDTH } from '../model/geometry/constants.ts';
import { labelWithShare, type FlowFigure, type LaneBand, type MapLayout } from '../model/mapLayout.ts';
import { wrapLines, type TextFont } from '../model/textSize.ts';
import { esc, headOf, iconOf, lineOf, num, rectOf, SANS } from './svgDraw.ts';
import { stepOf, streamOf, type StepPen } from './svgSteps.ts';

export type { AnalysisLook, SvgAnalysis, SvgLook } from './svgSteps.ts';

/** What the image is drawn with besides the steps: its title, the line of the classification, the faces of Inter, the copy of the map. */
export interface SvgOptions extends StepPen {
  readonly title: string;
  /** « Classification : Interne », under the map as a PNG writes it (S-4.1-08). */
  readonly caption: string;
  /** The `@font-face` of Inter with their files inside, null when the page could not read them. */
  readonly fonts: string | null;
  /** The metadata of the copy of the map (plan 3.1, L7), null without it. */
  readonly copy: string | null;
}

/** A flow and its head: in the colour of the analysis and thicker where the case changes hands, its head as large (plan 3.1). */
function flowOf(pen: SvgOptions, figure: FlowFigure): string {
  const handoff = pen.analysis?.marks.handoffs.has(figure.route.id) === true ? pen.analysis.look.handoff : null;
  const [colour, width] = handoff === null ? [pen.look.edge, EDGE_STROKE] : [handoff, HANDOFF_STROKE];
  return `<g class="flow"><path d="${esc(figure.route.path)}" fill="none" stroke="${colour}" stroke-width="${num(width)}"/>${headOf(figure.route, colour)}</g>`;
}

/** The margin round the map, the space under it and the line of the classification, as the PNG frames it (capture.ts). */
const MARGIN = 24;
const EXTRA = 20;
const CAPTION = 24;

const NAME: TextFont = { size: 13, weight: 600 };
const SMALL: TextFont = { size: 12, weight: 400 };
const EDGE_FONT: TextFont = { size: EDGE_LABEL.fontSize, weight: EDGE_LABEL.fontWeight };

/** The line height « normal » of Inter for a size: its ascent and its descent. */
const normal = (size: number): number => size * 1.2109375;

/** A lane: its body in alternation, its separator, its margin in its pastel with its stripe of ink, its name, its type and, under the value stream, its headcount and workload. */
function laneOf(pen: SvgOptions, band: LaneBand, width: number): string {
  const { look, measure, t } = pen;
  const [pastel, ink] = [look.pastels[band.tint - 1] ?? look.surface, look.inks[band.tint - 1] ?? look.text];
  const full = width + LANE_HEADER_WIDTH;
  const out = [
    `<rect x="${num(-LANE_HEADER_WIDTH)}" y="${num(band.top)}" width="${num(full)}" height="${num(band.height)}" fill="${band.parity === 'odd' ? look.laneOdd : look.laneEven}"/>`,
    `<rect x="${num(-LANE_HEADER_WIDTH)}" y="${num(band.top + band.height - 1)}" width="${num(full)}" height="1" fill="${look.laneSeparator}"/>`,
    `<rect x="${num(-LANE_HEADER_WIDTH)}" y="${num(band.top)}" width="${num(LANE_HEADER_WIDTH)}" height="${num(band.height - 1)}" fill="${pastel}"/>`,
    `<rect x="-4" y="${num(band.top)}" width="4" height="${num(band.height - 1)}" fill="${ink}"/>`,
  ];
  // One pixel more than the margin holds, as the measure of the labels: the browser does not wrap a name the measure fits.
  const names = wrapLines(band.name, 151, measure, NAME);
  const stream = band.stream === null ? [] : [
    { text: band.stream.headcount === null ? t('stream.lane.headcount_missing') : t('stream.lane.headcount', { count: band.stream.headcount }), missing: band.stream.headcount === null },
    { text: band.stream.unknown ? t('stream.lane.fte_unknown') : t(band.stream.incomplete ? 'stream.lane.fte_minimum' : 'stream.lane.fte', { fte: band.stream.fte }), missing: false },
  ];
  const total = names.length * 17 + 5 + normal(12) + (stream.length > 0 ? 5 + 15 * stream.length : 0);
  let top = band.top + 12 + (band.height - 1 - 24 - total) / 2;
  const left = -LANE_HEADER_WIDTH + 14;
  names.forEach((line, index) => out.push(lineOf(left, top + index * 17, 17, { font: NAME, fill: ink }, esc(line))));
  top += names.length * 17 + 5;
  out.push(lineOf(left, top, normal(12), { font: SMALL, fill: ink }, esc(t(`map.lane_types.${band.lane.type}`))));
  top += normal(12) + 5;
  stream.forEach((line, index) => out.push(lineOf(left, top + index * 15, 15, { font: line.missing ? { size: 12, weight: 600 } : SMALL, fill: line.missing ? look.warningText : look.textMuted }, esc(line.text))));
  return out.join('');
}

/** A line of the label of a flow, the share of its cases in its weight and colour when the line ends with it. */
function labelLine(pen: SvgOptions, figure: FlowFigure, line: string): string {
  const share = figure.share;
  const italic = figure.untranslated ? ' font-style="italic"' : '';
  // The lines are wrapped at their spaces and joined by plain ones: the share is found with its spaces made plain (« 15 % »).
  const shareText = share === null ? '' : share.text.replace(/\s+/g, ' ');
  if (share === null || !line.endsWith(shareText)) return italic === '' ? esc(line) : `<tspan${italic}>${esc(line)}</tspan>`;
  const before = line.slice(0, line.length - shareText.length);
  const weight = share.unknown ? 700 : share.remainder ? 500 : 600;
  const style = `font-weight="${String(weight)}" fill="${share.unknown ? pen.look.warningText : pen.look.text}"${share.remainder ? ' font-style="italic"' : ''}`;
  return `${before === '' ? '' : `<tspan${italic}>${esc(before)}</tspan>`}<tspan ${style}>${esc(shareText)}</tspan>`;
}

/** The label of a flow in the box the routing placed: its text wrapped as measured, centred, the icon of its documents and their count after it. */
function flowLabelOf(pen: SvgOptions, figure: FlowFigure): string {
  const box = figure.route.label;
  if (box === null) return '';
  const { look, measure, icons } = pen;
  const text = labelWithShare(figure.text, figure.share).trim();
  const documents = figure.documents.length;
  const frame = 2 * EDGE_LABEL.paddingX + 2 * EDGE_LABEL.border;
  const badge = documents === 0 ? 0 : EDGE_LABEL.iconSize + EDGE_LABEL.iconGap + measure(String(documents), EDGE_FONT);
  const gap = text !== '' && documents > 0 ? EDGE_LABEL.documentsGap : 0;
  const room = EDGE_LABEL.maxWidth - frame - badge - gap;
  const lines = text === '' ? [] : wrapLines(text, room, measure, EDGE_FONT);
  const textWidth = Math.min(room, Math.max(0, ...lines.map((line) => measure(line, EDGE_FONT))));
  const start = box.x + (box.width - textWidth - gap - badge) / 2;
  const top = box.y + (box.height - lines.length * EDGE_LABEL.lineHeight) / 2;
  const out = [rectOf(box.x + 0.5, box.y + 0.5, box.width - 1, box.height - 1, 5.5, `fill="${look.surface}" stroke="${look.border}"`)];
  lines.forEach((line, index) => out.push(lineOf(start + textWidth / 2, top + index * EDGE_LABEL.lineHeight, EDGE_LABEL.lineHeight, { font: EDGE_FONT, fill: look.textMuted, anchor: 'middle' }, labelLine(pen, figure, line))));
  if (documents > 0) {
    const x = start + textWidth + gap;
    const middle = box.y + box.height / 2;
    out.push(iconOf(icons, 'documents', x, middle - EDGE_LABEL.iconSize / 2, EDGE_LABEL.iconSize, look.textMuted, 2.25));
    out.push(lineOf(x + EDGE_LABEL.iconSize + EDGE_LABEL.iconGap, middle - EDGE_LABEL.lineHeight / 2, EDGE_LABEL.lineHeight, { font: EDGE_FONT, fill: look.textMuted }, String(documents)));
  }
  return out.join('');
}

/** The bounds of the image: the lanes with their margin, as the PNG takes them, the margins round it and the line under it. */
export function svgSize(layout: MapLayout): { readonly width: number; readonly height: number } {
  return { width: Math.ceil(layout.width + LANE_HEADER_WIDTH + 2 * MARGIN), height: Math.ceil(layout.height + 2 * MARGIN + EXTRA + CAPTION) };
}

/**
 * The image SVG of a map from its layout, painted in the order of the canvas: lanes, cartouches of the value stream,
 * flows and their heads, their labels, steps; the line of the classification under it. The same layout gives the same text.
 */
export function mapSvg(layout: MapLayout, pen: SvgOptions): string {
  const { look } = pen;
  const { width, height } = svgSize(layout);
  const body = [
    ...layout.lanes.map((band) => `<g class="lane">${laneOf(pen, band, layout.width)}</g>`),
    ...(pen.stream ? layout.steps.flatMap((figure) => (figure.stream === null ? [] : [`<g class="stream">${streamOf(pen, figure, figure.stream)}</g>`])) : []),
    // A route of no finite number (a flow of a step to itself, which the host never keeps) is left out rather than written broken.
    ...layout.flows.filter((figure) => !/NaN|Infinity/.test(figure.route.path)).map((figure) => flowOf(pen, figure)),
    ...layout.flows.flatMap((figure) => (figure.route.label === null ? [] : [`<g class="flow-label">${flowLabelOf(pen, figure)}</g>`])),
    ...layout.steps.map((figure) => stepOf(pen, figure)),
    `<g class="caption">${lineOf(-LANE_HEADER_WIDTH, layout.height + EXTRA, CAPTION, { font: { size: 12, weight: 600 }, fill: look.text }, esc(pen.caption))}</g>`,
  ];
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" width="${String(width)}" height="${String(height)}" viewBox="0 0 ${String(width)} ${String(height)}" font-family="${SANS}">`,
    `<title>${esc(pen.title)}</title>`,
    ...(pen.copy === null ? [] : [pen.copy]),
    ...(pen.fonts === null ? [] : [`<defs><style><![CDATA[${pen.fonts.replace(/\]\]>/g, '')}]]></style></defs>`]),
    `<rect width="${String(width)}" height="${String(height)}" fill="${look.surface}"/>`,
    `<g transform="translate(${num(MARGIN + LANE_HEADER_WIDTH)} ${num(MARGIN)})">${body.join('')}</g>`,
    '</svg>',
    '',
  ].join('\n');
}
