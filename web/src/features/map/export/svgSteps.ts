// svgSteps.ts
// The steps in the image SVG of a map (plan 3.1, L7), as the canvas draws them in its state of export: the card of each type in the ink of its lane, the diamond of a gateway and its marker, the code, the label cut as on the screen, the pictograms, the warning, the badges of a detailed step and its frequency, the RACI badges; under the layer « Analysis », the band of its class, the dashes of a break of medium and the handoffs of its detail; the cartouches of the value stream under them.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import type { AnalysisMarks } from '../model/analysis.ts';
import { GATEWAY_LABEL, STEP_HEIGHT, STEP_WIDTH, UNDER_STEP } from '../model/geometry/constants.ts';
import type { StepFigure } from '../model/mapLayout.ts';
import { readableDuration } from '../model/stepFacts.ts';
import { wrapLines, type TextFont, type TextMeasure } from '../model/textSize.ts';
import type { StreamCase, StreamFigure } from '../model/valueStream.ts';
import { hasPictos } from '../nodes/Pictos.tsx';
import { clamped, esc, iconOf, lineOf, MONO, num, rectOf, stripeOf } from './svgDraw.ts';
import type { SvgIconName, SvgIcons } from './svgIcons.ts';

/** The colours of the image, read from the tokens of the charter at the export (spec 9.2: no colour is written in the code). */
export interface SvgLook {
  readonly surface: string;
  readonly laneOdd: string;
  readonly laneEven: string;
  readonly laneSeparator: string;
  readonly border: string;
  readonly text: string;
  readonly textMuted: string;
  readonly accentStrong: string;
  readonly edge: string;
  readonly frameLabelBg: string;
  readonly dangerMuted: string;
  readonly warningSoft: string;
  readonly warningText: string;
  readonly warningGraphic: string;
  readonly raci: Readonly<Record<string, string>>;
  /** The pastels and the inks of the eight tints of the lanes, the first one at 0. */
  readonly pastels: readonly string[];
  readonly inks: readonly string[];
}

/** The colours of the marks of the layer « Analysis », read from the tokens of the charter at the export (plan 3.1, Makigami M1, M3). */
export interface AnalysisLook {
  readonly handoff: string;
  readonly badgeBg: string;
  readonly badgeFg: string;
  readonly classes: Readonly<Record<NonNullable<Schema<'ValueClass'>>, string>>;
  readonly mediaBreak: string;
}

/** The marks of the layer « Analysis » drawn on the image, as the canvas draws them without a tile in focus, with their colours. */
export interface SvgAnalysis {
  readonly marks: AnalysisMarks;
  readonly look: AnalysisLook;
}

/** What the steps are drawn with: the colours, the texts, the measure of the canvas, the icons, the preferences of the person. */
export interface StepPen {
  readonly look: SvgLook;
  readonly t: Translate;
  readonly measure: TextMeasure;
  readonly icons: SvgIcons;
  readonly codes: boolean;
  readonly pictos: boolean;
  readonly raci: boolean;
  readonly stream: boolean;
  /** The marks of the layer « Analysis »: with the layer on, or asked by the poster of the map (Makigami M9); none else. */
  readonly analysis?: SvgAnalysis | null;
}

const LABEL: TextFont = { size: 13, weight: 500 };
const CODE: TextFont = { size: 10.5, weight: 400 };
const MICRO: TextFont = { size: 10, weight: 500 };
const BADGE: TextFont = { size: 10.5, weight: 600 };
const GATEWAY: TextFont = { size: GATEWAY_LABEL.fontSize, weight: GATEWAY_LABEL.fontWeight };
const GATEWAYS: Readonly<Partial<Record<string, SvgIconName>>> = { decision: 'decision', parallel: 'parallel', inclusive: 'inclusive' };

const inkOf = (pen: StepPen, figure: StepFigure): string => pen.look.inks[figure.tint - 1] ?? pen.look.text;

/** The card of a task, a wait, a start or an end: its ground, its left stripe of ink, its sides; the second card of a detailed step behind. */
function cardOf(pen: StepPen, figure: StepFigure): string {
  const { box, step } = figure;
  const { look } = pen;
  const pill = step.type === 'start' || step.type === 'end';
  const [radius, side] = [pill ? STEP_HEIGHT / 2 : 10, step.type === 'end' ? 3 : 1];
  const out: string[] = [];
  if (figure.macro) out.push(rectOf(box.x + 3.5, box.y + 3.5, STEP_WIDTH - 1, STEP_HEIGHT - 1, 9.5, `fill="${look.surface}" stroke="${look.border}"`));
  out.push(rectOf(box.x, box.y, STEP_WIDTH, STEP_HEIGHT, radius, `fill="${look.surface}"`));
  // A double border of 3 px for an end: two lines of 1 px with 1 px between them, in the colour of the flows (AR-59); dashes for a wait.
  const lines = step.type === 'end' ? [0.5, 2.5] : [0.5];
  const stroke = step.type === 'end' || step.type === 'wait' ? look.edge : look.border;
  const dashed = step.type === 'wait' ? ' stroke-dasharray="3 3"' : '';
  for (const inset of lines) out.push(rectOf(box.x + inset, box.y + inset, STEP_WIDTH - 2 * inset, STEP_HEIGHT - 2 * inset, radius - inset, `fill="none" stroke="${stroke}"${dashed}`));
  out.push(`<path d="${stripeOf(box.x, box.y, STEP_HEIGHT, radius, side)}" fill="${inkOf(pen, figure)}"/>`);
  return out.join('');
}

/** The code and the label of a card, centred in its content as the flex column centres them, the label cut at three lines. */
function cardTextOf(pen: StepPen, figure: StepFigure): string {
  const { box, step } = figure;
  const pill = step.type === 'start' || step.type === 'end';
  const [left, right] = [box.x + (pill ? 19 : 14), box.x + (pill ? 151 : 159)];
  const [top, bottom] = [box.y + 3, box.y + (pen.pictos && hasPictos(step) ? 49 : 61)];
  const lineHeight = step.type === 'end' ? 14 : 15;
  const lines = clamped(wrapLines(figure.label, right - left, pen.measure, LABEL), 3, right - left, pen.measure, LABEL);
  const codeHeight = pen.codes ? 12 : 0;
  const group = codeHeight + lines.length * lineHeight;
  let y = group <= bottom - top ? top + (bottom - top - group) / 2 : top;
  const [x, anchor] = pill ? [(left + right) / 2, 'middle' as const] : [left, 'start' as const];
  const out: string[] = [];
  if (pen.codes) out.push(lineOf(x, y, 12, { font: CODE, fill: pen.look.textMuted, anchor, family: MONO }, esc(step.code)));
  y += codeHeight;
  const fill = figure.untranslated ? pen.look.textMuted : pen.look.text;
  lines.forEach((line, index) => out.push(lineOf(x, y + index * lineHeight, lineHeight, { font: LABEL, fill, anchor, italic: figure.untranslated }, esc(line))));
  return out.join('');
}

/** A gateway: its diamond and its marker in the ink, its code above, its label under it on the ground of its lane, two lines at most. */
function gatewayOf(pen: StepPen, figure: StepFigure, marker: SvgIconName): string {
  const { box, step } = figure;
  const { look, measure } = pen;
  const ink = inkOf(pen, figure);
  const points = [[1, 32], [85, 1], [169, 32], [85, 63]].map(([x = 0, y = 0]) => `${num(box.x + x)},${num(box.y + y)}`).join(' ');
  const out = [`<polygon points="${points}" fill="${look.surface}" stroke="${ink}" stroke-width="1.5" stroke-linejoin="round"/>`, iconOf(pen.icons, marker, box.x + 77, box.y + 24, 16, ink, 2.5)];
  if (pen.codes) out.push(lineOf(box.x, box.y - 13, 12, { font: CODE, fill: look.textMuted, family: MONO }, esc(step.code)));
  // Aside of the axis, the label has its padding on one side only (map.css, is-under-left and is-under-right).
  const padding = (figure.under === 'centre' ? 2 : 1) * GATEWAY_LABEL.paddingX;
  const room = GATEWAY_LABEL.maxWidth - padding;
  const lines = clamped(wrapLines(figure.label, room, measure, GATEWAY), GATEWAY_LABEL.lines, room, measure, GATEWAY);
  if (lines.length === 0) return out.join('');
  const width = Math.min(GATEWAY_LABEL.maxWidth, Math.ceil(Math.max(...lines.map((line) => measure(line, GATEWAY))) + padding + 1));
  const top = box.y + STEP_HEIGHT + UNDER_STEP.gatewayLabel;
  const centre = box.x + STEP_WIDTH / 2;
  const [left, x, anchor] = figure.under === 'right' ? [centre + UNDER_STEP.shift, centre + UNDER_STEP.shift, 'start' as const]
    : figure.under === 'left' ? [centre - UNDER_STEP.shift - width, centre - UNDER_STEP.shift, 'end' as const] : [centre - width / 2, centre, 'middle' as const];
  out.push(`<rect x="${num(left)}" y="${num(top)}" width="${num(width)}" height="${num(lines.length * GATEWAY_LABEL.lineHeight)}" fill="${figure.parity === 'odd' ? look.laneOdd : look.laneEven}"/>`);
  const fill = step.type === 'decision' ? look.text : look.textMuted;
  lines.forEach((line, index) => out.push(lineOf(x, top + index * GATEWAY_LABEL.lineHeight, GATEWAY_LABEL.lineHeight, { font: GATEWAY, fill, anchor, italic: figure.untranslated }, esc(line))));
  return out.join('');
}

/** The pictograms of a step: its documents, its open pain points in muted red, the duration of its sheet; right of a card, centred under a diamond. */
function pictosOf(pen: StepPen, figure: StepFigure): string {
  const { box, step } = figure;
  const { look, measure, icons } = pen;
  const duration = step.sheet.duration_minutes ?? 0;
  const items: { icon: SvgIconName; text: string; fill: string }[] = [
    ...(step.markers.documents.length > 0 ? [{ icon: 'documents' as const, text: String(step.markers.documents.length), fill: look.textMuted }] : []),
    ...(step.markers.pain_points.length > 0 ? [{ icon: 'painPoints' as const, text: String(step.markers.pain_points.length), fill: look.dangerMuted }] : []),
    ...(duration > 0 ? [{ icon: 'duration' as const, text: readableDuration(duration), fill: look.textMuted }] : []),
  ];
  const widths = items.map((item) => 10 + 3 + measure(item.text, MICRO));
  const total = widths.reduce((sum, width) => sum + width, 0) + 8 * Math.max(0, items.length - 1);
  const gateway = GATEWAYS[step.type] !== undefined;
  const top = box.y + (gateway ? 45 : 48);
  let x = gateway ? box.x + STEP_WIDTH / 2 - total / 2 : box.x + 160 - total;
  const out = gateway ? [rectOf(x - 4, top - 1, total + 8, 12, 6, `fill="${look.surface}"`)] : [];
  items.forEach((item, index) => {
    out.push(iconOf(icons, item.icon, x, top, 10, item.fill, 2.25), lineOf(x + 13, top, 10, { font: MICRO, fill: item.fill }, esc(item.text)));
    x += (widths[index] ?? 0) + 8;
  });
  return out.join('');
}

/** The « ! » of a warning of the layout, in the top left corner of a card or on the upper left side of a diamond. */
function warningOf(pen: StepPen, figure: StepFigure): string {
  const { box } = figure;
  const [cx, cy] = GATEWAYS[figure.step.type] === undefined ? [box.x + 1.5, box.y + 0.5] : [box.x + 42.5, box.y + 16];
  const { look } = pen;
  return `<circle cx="${num(cx)}" cy="${num(cy)}" r="9.5" fill="${look.surface}"/><circle cx="${num(cx)}" cy="${num(cy)}" r="8.5" fill="${look.warningGraphic}"/>${
    lineOf(cx, cy - 8.5, 17, { font: { size: 11, weight: 700 }, fill: look.text, anchor: 'middle' }, '!')}`;
}

/** A badge of a pill: its icon and its text, from a left side, at a top, on a ground; an outline of the surface round it when asked. */
function pillOf(pen: StepPen, at: { left: number; top: number }, content: { icon: SvgIconName; text: string; ground: string; ink: string; padding: readonly [number, number] }, outline: boolean): string {
  const width = content.padding[0] + 11 + 4 + pen.measure(content.text, BADGE) + content.padding[1];
  const out = outline ? [rectOf(at.left - 2, at.top - 2, width + 4, 22, 11, `fill="${pen.look.surface}"`)] : [];
  out.push(rectOf(at.left, at.top, width, 18, 9, `fill="${content.ground}"`), iconOf(pen.icons, content.icon, at.left + content.padding[0], at.top + 3.5, 11, content.ink, 2.25));
  out.push(lineOf(at.left + content.padding[0] + 15, at.top, 18, { font: BADGE, fill: content.ink }, esc(content.text)));
  return out.join('');
}

/** The width of a badge of a pill, to place it from its right side or its middle. */
const pillWidth = (pen: StepPen, text: string, padding: readonly [number, number]): number => padding[0] + 15 + pen.measure(text, BADGE) + padding[1];

/** A detailed step: « Détaillée » over its top right corner (spec 9.4). */
function detailedOf(pen: StepPen, figure: StepFigure): string {
  const { box } = figure;
  const { look, t } = pen;
  const text = t('map.detailed');
  return pillOf(pen, { left: box.x + 159 - pillWidth(pen, text, [5, 7]), top: box.y - 8 }, { icon: 'detailed', text, ground: look.frameLabelBg, ink: look.accentStrong, padding: [5, 7] }, true);
}

/** The frequency of the flow of a detailed step under it when its sheet has one, under the layer « Analysis » too (audit 3.1 V3, U6). */
function frequencyOf(pen: StepPen, figure: StepFigure): string {
  const { box, step } = figure;
  const { look, t } = pen;
  if (step.sheet.frequency === null) return '';
  const frequency = t(`map.frequencies.${step.sheet.frequency}`);
  const width = pillWidth(pen, frequency, [6, 8]);
  const left = figure.under === 'right' ? box.x + 96 : figure.under === 'left' ? box.x + 76 - width : box.x + 86 - width / 2;
  return pillOf(pen, { left, top: box.y + 68 }, { icon: 'frequency', text: frequency, ground: look.pastels[5] ?? look.surface, ink: look.inks[5] ?? look.text, padding: [6, 8] }, false);
}

/** The RACI badges under a step: « A ? » first when nobody approves, then each role, in rows centred, or aside of the axis (AR-58). */
function raciOf(pen: StepPen, figure: StepFigure): string {
  const { box, raci } = figure;
  if (raci === null || (raci.badges.length === 0 && !raci.missingApprover)) return '';
  const { look, measure, t } = pen;
  const bold: TextFont = { size: 10, weight: 700 };
  const plain: TextFont = { size: 10, weight: 400 };
  const badges = [
    ...(raci.missingApprover ? [{ role: t('raci.missing_badge'), name: '', ground: look.warningSoft, edge: look.warningGraphic, ink: look.warningText }] : []),
    ...raci.badges.map((badge) => ({ role: badge.role, name: ` ${badge.name}`, ground: look.raci[badge.role] ?? look.surface, edge: look.border, ink: look.text })),
  ].map((badge) => {
    let name = badge.name;
    while (name.length > 1 && 12 + measure(badge.role, bold) + measure(name, plain) > 120) name = `${name.slice(0, -1).replace(/[\s\S]$/u, '').trimEnd()}…`;
    return { ...badge, name, width: Math.min(120, 12 + measure(badge.role, bold) + measure(name, plain)) };
  });
  const gateway = GATEWAYS[figure.step.type] !== undefined;
  const [from, to] = gateway ? [box.x - 14, box.x + 184] : [box.x - 11, box.x + 183];
  const centre = box.x + (gateway ? 85 : 86);
  const [left, right] = figure.under === 'right' ? [centre + 10, to] : figure.under === 'left' ? [from, centre - 10] : [from, to];
  const rows: (typeof badges)[] = [[]];
  for (const badge of badges) {
    const row = rows.at(-1) ?? [];
    if (row.length > 0 && row.reduce((sum, item) => sum + item.width + 3, 0) + badge.width > right - left) rows.push([badge]);
    else row.push(badge);
  }
  const out: string[] = [];
  rows.forEach((row, index) => {
    const used = row.reduce((sum, item) => sum + item.width, 0) + 3 * Math.max(0, row.length - 1);
    let x = figure.under === 'right' ? left : figure.under === 'left' ? right - used : left + (right - left - used) / 2;
    const top = box.y + (gateway ? 64 : 63) + figure.raciTop + index * 20;
    for (const badge of row) {
      out.push(rectOf(x + 0.5, top + 0.5, badge.width - 1, 16, 5.5, `fill="${badge.ground}" stroke="${badge.edge}"`));
      out.push(lineOf(x + 6, top + 1, 15, { font: plain, fill: badge.ink }, `<tspan font-weight="700">${esc(badge.role)}</tspan>${esc(badge.name)}`));
      x += badge.width + 3;
    }
  });
  return out.join('');
}

/** The dashes of a break of medium round a step, 4 units out as the outline of the canvas (2 wide, 3 away). */
function mediaBreakOf(analysis: SvgAnalysis, figure: StepFigure): string {
  const { box, step } = figure;
  const radius = GATEWAYS[step.type] !== undefined ? 0 : step.type === 'start' || step.type === 'end' ? STEP_HEIGHT / 2 : 10;
  return rectOf(box.x - 4, box.y - 4, STEP_WIDTH + 8, STEP_HEIGHT + 8, radius + 4, `fill="none" stroke="${analysis.look.mediaBreak}" stroke-width="2" stroke-dasharray="6 4"`);
}

/** The band of the class of a step along its top, inside its sides, its lower corners rounded as the canvas draws it. */
function valueStripeOf(analysis: SvgAnalysis, figure: StepFigure, value: NonNullable<Schema<'ValueClass'>>): string {
  const { box, step } = figure;
  const card = GATEWAYS[step.type] === undefined;
  const [x, y, width] = card ? [box.x + 9, box.y + 1, STEP_WIDTH - 16] : [box.x + 6, box.y, STEP_WIDTH - 12];
  const path = `M${num(x)} ${num(y)}H${num(x + width)}V${num(y + 2)}A2 2 0 0 1 ${num(x + width - 2)} ${num(y + 4)}H${num(x + 2)}A2 2 0 0 1 ${num(x)} ${num(y + 2)}Z`;
  return `<path class="value" d="${path}" fill="${analysis.look.classes[value]}"/>`;
}

/** The handoffs of the detail of a step, where the badge of a detailed step stands. */
function handoffBadgeOf(pen: StepPen, analysis: SvgAnalysis, figure: StepFigure, count: number): string {
  const text = String(count);
  const { box } = figure;
  return pillOf(pen, { left: box.x + 159 - pillWidth(pen, text, [5, 7]), top: box.y - 8 },
    { icon: 'handoff', text, ground: analysis.look.badgeBg, ink: analysis.look.badgeFg, padding: [5, 7] }, true);
}

/**
 * A step and what hangs on it, in the order the canvas paints them; under the layer « Analysis », the dashes of a break
 * of medium first, the band of its class on the card, the handoffs of its detail in the place of « Détaillée », its
 * frequency kept under it.
 */
export function stepOf(pen: StepPen, figure: StepFigure): string {
  const marker = GATEWAYS[figure.step.type];
  const analysis = pen.analysis ?? null;
  const id = figure.step.id;
  const out = analysis !== null && analysis.marks.breaks.has(id) ? [mediaBreakOf(analysis, figure)] : [];
  out.push(...(marker === undefined ? [cardOf(pen, figure), cardTextOf(pen, figure)] : [gatewayOf(pen, figure, marker)]));
  const value = analysis?.marks.classes[id];
  if (analysis !== null && value !== undefined) out.push(valueStripeOf(analysis, figure, value));
  if (pen.pictos && hasPictos(figure.step)) out.push(pictosOf(pen, figure));
  if (figure.warnings.length > 0) out.push(warningOf(pen, figure));
  const handoffs = analysis?.marks.detailed[id];
  if (analysis !== null && handoffs !== undefined) out.push(handoffBadgeOf(pen, analysis, figure, handoffs));
  else if (figure.macro && !pen.stream) out.push(detailedOf(pen, figure));
  if (figure.macro && !pen.stream) out.push(frequencyOf(pen, figure));
  if (pen.raci) out.push(raciOf(pen, figure));
  return `<g class="step">${out.join('')}</g>`;
}

/** A case of a cartouche: its icon, its value in the accent when computed and underlined dotted when of a draft, its unit. */
function caseOf(pen: StepPen, item: StreamCase, x: number, top: number): string {
  const { look, measure, icons } = pen;
  const out = [iconOf(icons, item.kind, x + 5, top + 3, 11, look.textMuted)];
  if (item.quantity === null) return out.join('') + lineOf(x + 19, top, 17, { font: { size: 10, weight: 400 }, fill: look.textMuted }, '–');
  const value: TextFont = { size: 11, weight: 600 };
  const width = measure(item.quantity.value, value);
  out.push(lineOf(x + 19, top, 17, { font: value, fill: item.computed ? look.accentStrong : look.text }, esc(item.quantity.value)));
  if (item.draft) out.push(`<line x1="${num(x + 19)}" y1="${num(top + 15)}" x2="${num(x + 19 + width)}" y2="${num(top + 15)}" stroke="${look.warningGraphic}" stroke-dasharray="1 2"/>`);
  out.push(lineOf(x + 22 + width, top, 17, { font: { size: 10, weight: 400 }, fill: look.textMuted }, esc(item.quantity.unit)));
  return out.join('');
}

/** What the value stream hangs under a step, under the flows: its cartouche of four cases, or the line of the entry of a start, of the exit of an end. */
export function streamOf(pen: StepPen, figure: StepFigure, stream: StreamFigure): string {
  const { look, measure, icons, t } = pen;
  const [x, y] = [figure.box.x + 10, figure.box.y + STEP_HEIGHT + figure.streamTop];
  if (stream.kind === 'card') {
    const out = [rectOf(x + 0.5, y + 0.5, 149, 35, 5.5, `fill="${look.surface}" stroke="${look.border}"`)];
    out.push(`<path d="M${num(x + 75)} ${num(y + 1)}V${num(y + 35)}M${num(x + 1)} ${num(y + 18)}H${num(x + 149)}" stroke="${look.border}"/>`);
    stream.cases.forEach((item, index) => out.push(caseOf(pen, item, x + 1 + (index % 2) * 74, y + 1 + Math.floor(index / 2) * 17)));
    return out.join('');
  }
  const quantity = stream.quantity;
  const missing = quantity === null;
  const label = t(stream.kind === 'entry' ? (missing ? 'stream.entry_missing' : 'stream.entry') : missing ? 'stream.exit_unknown' : 'stream.exit');
  const font: TextFont = { size: 10, weight: missing ? 600 : 400 };
  const value: TextFont = { size: 11, weight: 600 };
  const parts = quantity === null ? [] : [quantity.value, quantity.unit];
  const width = 11 + 4 + measure(label, font) + parts.reduce((sum, part, index) => sum + 4 + measure(part, index === 0 ? value : font), 0);
  const ink = missing ? look.warningText : look.textMuted;
  let at = x + 75 - width / 2;
  const out = [rectOf(x + 0.5, y + 0.5, 149, 17, 5.5, `fill="${missing ? look.warningSoft : look.surface}" stroke="${missing ? look.warningGraphic : look.border}"`)];
  out.push(iconOf(icons, stream.kind, at, y + 3.5, 11, ink), lineOf(at + 15, y, 18, { font, fill: ink }, esc(label)));
  at += 15 + measure(label, font);
  parts.forEach((part, index) => {
    out.push(lineOf(at + 4, y, 18, { font: index === 0 ? value : font, fill: index === 0 ? look.accentStrong : look.textMuted }, esc(part)));
    at += 4 + measure(part, index === 0 ? value : font);
  });
  return out.join('');
}
