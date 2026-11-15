// textSize.ts
// The size of the texts the routing must keep clear: the label box of a flow and the label written under a gateway (B-3.4-35, AR-57).

import { EDGE_LABEL, GATEWAY_LABEL } from './geometry/constants.ts';
import type { Size } from './geometry/routing/model.ts';

/** A font as the measure needs it: its size in pixels and its weight. */
export interface TextFont {
  readonly size: number;
  readonly weight: number;
}

/** The width of a text written in a font, in pixels. */
export type TextMeasure = (text: string, font: TextFont) => number;

/** The average width of a character, in ems, where no canvas can measure (a page not drawn, the unit tests). */
const AVERAGE_CHARACTER = 0.56;

export const estimateMeasure: TextMeasure = (text, font) => text.length * font.size * AVERAGE_CHARACTER;

/** The family of the texts of the canvas, as the style sheets write it (tokens.css `--font-sans`). */
const FONT_FAMILY = "'Inter Variable', 'Segoe UI Variable', system-ui, sans-serif";

/**
 * The measure of the browser: a canvas writes the text in the font of the page; where the canvas is not available, the
 * estimate. The widths are kept, the same texts coming back at each drawing of the map.
 */
export function canvasMeasure(): TextMeasure {
  let context: CanvasRenderingContext2D | null;
  try {
    context = document.createElement('canvas').getContext('2d');
  } catch {
    context = null;
  }
  if (context === null) return estimateMeasure;
  const known = new Map<string, number>();
  const pen = context;
  return (text, font) => {
    const key = `${String(font.weight)}/${String(font.size)}/${text}`;
    const cached = known.get(key);
    if (cached !== undefined) return cached;
    pen.font = `${String(font.weight)} ${String(font.size)}px ${FONT_FAMILY}`;
    const width = pen.measureText(text).width;
    known.set(key, width);
    return width;
  };
}

/** A word wider than the width, cut into pieces that hold in it, as the style sheet breaks it (`overflow-wrap: anywhere`). */
function pieces(word: string, width: number, measure: TextMeasure, font: TextFont): string[] {
  if (measure(word, font) <= width) return [word];
  const cut: string[] = [];
  let piece = '';
  for (const character of word) {
    if (piece !== '' && measure(piece + character, font) > width) {
      cut.push(piece);
      piece = character;
    } else piece += character;
  }
  if (piece !== '') cut.push(piece);
  return cut;
}

/** The lines of a text wrapped at a width, word by word; a word longer than the width is cut over several lines. */
export function wrapLines(text: string, width: number, measure: TextMeasure, font: TextFont): string[] {
  const words = text.trim().split(/\s+/).filter((word) => word !== '').flatMap((word) => pieces(word, width, measure, font));
  const lines: string[] = [];
  let line = '';
  for (const word of words) {
    const candidate = line === '' ? word : `${line} ${word}`;
    // Every piece holds in the width: only a line already started can be too long with one more.
    if (measure(candidate, font) > width) {
      lines.push(line);
      line = word;
    } else line = candidate;
  }
  if (line !== '') lines.push(line);
  return lines;
}

const EDGE_FONT: TextFont = { size: EDGE_LABEL.fontSize, weight: EDGE_LABEL.fontWeight };
const GATEWAY_FONT: TextFont = { size: GATEWAY_LABEL.fontSize, weight: GATEWAY_LABEL.fontWeight };

/** One pixel more than measured, so that the browser does not wrap a text the measure put on one line. */
const SLACK = 1;

/**
 * The box of the label of a flow: its text wrapped in 110 px at most, then the document icon and its count when the flow
 * carries documents (B-3.4-35, B-3.4-38); null without a text nor documents.
 */
export function edgeLabelSize(text: string, documents: number, measure: TextMeasure): Size | null {
  const words = text.trim();
  if (words === '' && documents === 0) return null;
  const frame = 2 * EDGE_LABEL.paddingX + 2 * EDGE_LABEL.border;
  const badge = documents === 0 ? 0 : EDGE_LABEL.iconSize + EDGE_LABEL.iconGap + measure(String(documents), EDGE_FONT);
  const gap = words !== '' && documents > 0 ? EDGE_LABEL.documentsGap : 0;
  const room = EDGE_LABEL.maxWidth - frame - badge - gap;
  const lines = words === '' ? [] : wrapLines(words, room, measure, EDGE_FONT);
  const textWidth = Math.min(room, Math.max(0, ...lines.map((line) => measure(line, EDGE_FONT))));
  const width = Math.min(EDGE_LABEL.maxWidth, Math.ceil(frame + textWidth + gap + badge + SLACK));
  const height = Math.max(1, lines.length) * EDGE_LABEL.lineHeight + EDGE_LABEL.paddingTop + EDGE_LABEL.paddingBottom + 2 * EDGE_LABEL.border;
  return { width, height };
}

/** The label written under a gateway: two lines at most in 200 px (AR-57); undefined without a label. */
export function gatewayLabelSize(text: string, measure: TextMeasure): Size | undefined {
  const words = text.trim();
  if (words === '') return undefined;
  const room = GATEWAY_LABEL.maxWidth - 2 * GATEWAY_LABEL.paddingX;
  const lines = wrapLines(words, room, measure, GATEWAY_FONT).slice(0, GATEWAY_LABEL.lines);
  const textWidth = Math.min(room, Math.max(...lines.map((line) => measure(line, GATEWAY_FONT))));
  return { width: Math.min(GATEWAY_LABEL.maxWidth, Math.ceil(textWidth + 2 * GATEWAY_LABEL.paddingX + SLACK)), height: lines.length * GATEWAY_LABEL.lineHeight };
}
