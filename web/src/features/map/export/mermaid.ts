// mermaid.ts
// The diagrams of a map written in Mermaid by the code, never by a model, so that their syntax always holds (plan 3.1, L3 and L4): one font fixed for the measure and the display, texts cut into lines and never cut short, short titles for the boxes of fixed size, no identifier read from a text.

import type { Schema } from '../../../api/client.ts';
import { flowText, laneName, stepLabel } from '../model/mapLayout.ts';
import { laneRanks, readingOrder } from '../model/readingOrder.ts';
import { arialMeasure, graphemes, WIDE_CHARACTER } from './arialWidth.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;

/** The longest line of a text in a box of a diagram: the box is never too narrow for it (rendu Mermaid, exigence 1). */
export const LINE_MAX = 24;

/** The height a line of a title takes in a drawing, for the margin under the titles of the lanes. */
const TITLE_LINE = 22;

/** The font of every diagram, present on every computer: a box is measured with the font it is shown in. */
const FONT_FAMILY = 'Arial, Segoe UI, sans-serif';

/**
 * The header of every diagram: a neutral theme and the font of the diagrams; under the titles of the lanes, a margin for
 * as many lines as the longest one has, which Mermaid does not reserve itself, so that no step covers a title.
 */
export const mermaidInit = (titleLines = 1): string =>
  `%%{init: {"theme": "neutral", "themeVariables": {"fontFamily": "${FONT_FAMILY}"}, "flowchart": {"htmlLabels": true, "subGraphTitleMargin": {"top": 6, "bottom": ${String(14 + TITLE_LINE * Math.max(0, titleLines - 1))}}}}}%%`;

/** The colours of a step with a problem, read from the tokens of the charter at the export (spec 9.2); null leaves them out. */
export interface MermaidLook {
  readonly problem: { readonly fill: string; readonly stroke: string } | null;
}

/** A text in lines of `max` characters at most, cut at the spaces; a word longer than a line cut with a hyphen; nothing dropped. */
export function lines(text: string, max = LINE_MAX): string[] {
  const out: string[] = [];
  let line = '';
  for (const whole of text.replace(/\s+/g, ' ').trim().split(' ').filter((word) => word !== '')) {
    let word = whole;
    while (word.length > max) {
      if (line !== '') out.push(line);
      line = '';
      out.push(`${word.slice(0, max - 1)}-`);
      word = word.slice(max - 1);
    }
    if (line === '') line = word;
    else if (line.length + 1 + word.length <= max) line = `${line} ${word}`;
    else {
      out.push(line);
      line = word;
    }
  }
  if (line !== '') out.push(line);
  return out;
}

/**
 * A text inside the quotes of a label: the signs Mermaid reads written as its entity codes, the sign of the codes first;
 * the percent too, whose double opens a directive even inside quotes (`%%{`).
 */
export const escaped = (text: string): string =>
  text.replace(/#/g, '#35;').replace(/%/g, '#37;').replace(/"/g, '#quot;').replace(/</g, '#lt;').replace(/>/g, '#gt;').replace(/`/g, '#96;');

/** A label in quotes, its lines joined by a break. */
export const quoted = (parts: readonly string[]): string => `"${parts.map(escaped).join('<br/>')}"`;

/** The shape of each type of step: a task a box, a gateway a diamond, a start a stadium, an end a double circle, a wait a flag. */
const SHAPES: Readonly<Record<StepView['type'], readonly [string, string]>> = {
  task: ['[', ']'], decision: ['{', '}'], parallel: ['{', '}'], inclusive: ['{', '}'], wait: ['>', ']'], start: ['([', '])'], end: ['(((', ')))'],
};

/** The sign of a parallel or an inclusive gateway, as BPMN draws it. */
const SIGNS: Readonly<Partial<Record<StepView['type'], string>>> = { parallel: '+', inclusive: 'O' };

/** A node: its code, then its label in lines; a detailed task in a box with double sides. */
function nodeOf(id: string, step: StepView, language: string): string {
  const [open, close] = step.type === 'task' && step.sub_map_id !== null ? ['[[', ']]'] : SHAPES[step.type];
  const sign = SIGNS[step.type];
  const parts = [...(sign === undefined ? [] : [sign]), step.code, ...lines(stepLabel(step, language))];
  return `${id}${open}${quoted(parts)}${close}`;
}

/** What a flowchart shows: the steps of an extract (all when absent), the steps with a problem, coloured. */
export interface FlowchartOptions {
  readonly only?: ReadonlySet<string>;
  readonly marked?: ReadonlySet<string>;
  readonly look?: MermaidLook;
}

/** The identifiers of the nodes, by the reading order of the steps shown: S1, S2... */
function nodesOf(map: MapView, only: ReadonlySet<string> | undefined) {
  const ranks = laneRanks(map.lanes);
  const steps = map.steps.filter((step) => ranks.has(step.lane_id) && (only === undefined || only.has(step.id))).sort(readingOrder(ranks));
  return { ranks, steps, ids: new Map(steps.map((step, index) => [step.id, `S${String(index + 1)}`])) };
}

/** The flows between two steps shown, in the reading order of their sources, then of their targets. */
function edgesOf(map: MapView, language: string, steps: readonly StepView[], ids: ReadonlyMap<string, string>): string[] {
  const rank = new Map(steps.map((step, index) => [step.id, index]));
  const byId = new Map(steps.map((step) => [step.id, step]));
  return map.flows
    .filter((flow) => ids.has(flow.source_step_id) && ids.has(flow.target_step_id))
    .sort((a, b) => (rank.get(a.source_step_id) ?? 0) - (rank.get(b.source_step_id) ?? 0) || (rank.get(a.target_step_id) ?? 0) - (rank.get(b.target_step_id) ?? 0))
    .map((flow) => {
      const [from, to] = [ids.get(flow.source_step_id) ?? '', ids.get(flow.target_step_id) ?? ''];
      const source = byId.get(flow.source_step_id);
      const text = source === undefined ? '' : flowText(flow, source, language);
      return text === '' ? `  ${from} --> ${to}` : `  ${from} -->|${quoted(lines(text))}| ${to}`;
    });
}

/**
 * The map, or an extract of it, as a flowchart from left to right: a subgraph per lane in the order of the lanes, its
 * steps in the reading order, its flows with their labels; the steps with a problem coloured as the charter colours a
 * fault. The same map gives the same text.
 */
export function flowchartOf(map: MapView, language: string, options: FlowchartOptions = {}): string {
  const { ranks, steps, ids } = nodesOf(map, options.only);
  const shown = [...map.lanes].sort((a, b) => (ranks.get(a.id) ?? 0) - (ranks.get(b.id) ?? 0)).filter((lane) => steps.some((step) => step.lane_id === lane.id));
  const titles = new Map(shown.map((lane) => [lane.id, lines(laneName(lane, language))]));
  const out = [mermaidInit(Math.max(1, ...[...titles.values()].map((title) => title.length))), 'flowchart LR'];
  for (const lane of shown) {
    const own = steps.filter((step) => step.lane_id === lane.id);
    out.push(`  subgraph L${String((ranks.get(lane.id) ?? 0) + 1)}[${quoted(titles.get(lane.id) ?? [])}]`);
    for (const step of own) out.push(`    ${nodeOf(ids.get(step.id) ?? '', step, language)}`);
    out.push('  end');
  }
  out.push(...edgesOf(map, language, steps, ids));
  const marked = steps.filter((step) => options.marked?.has(step.id) === true).map((step) => ids.get(step.id) ?? '');
  const problem = options.look?.problem ?? null;
  if (marked.length > 0 && problem !== null) {
    out.push(`  classDef issue fill:${problem.fill},stroke:${problem.stroke},stroke-width:2px`);
    out.push(`  class ${marked.join(',')} issue`);
  }
  return out.join('\n');
}

/** The longest title of a box of fixed size (journey, timeline, quadrantChart): the whole text follows in a table. */
export const SHORT_MAX = 25;

/**
 * A title for a box of fixed size, unquoted: the signs that end or split an entry of journey, timeline, quadrantChart or
 * pie become spaces; cut at its bound with an ellipsis, the whole text being in the table that follows the diagram.
 */
export function plain(text: string, max = SHORT_MAX): string {
  const safe = cleaned(text);
  return safe.length <= max ? safe : `${safe.slice(0, max - 1).trimEnd()}…`;
}

/** A text without the signs that end or split an entry of these diagrams; a word that opens one of their lines in quotes. */
function cleaned(text: string): string {
  const clean = text.replace(/[:;,#%"'[\]{}<>`|\n\r\t]/g, ' ').replace(/\s+/g, ' ').trim();
  // A word that opens a line of these diagrams (« section », « title »…) would be read as one: in quotes it is a text.
  return KEYWORDS.test(clean) ? `«${clean}»` : clean;
}

/** The words that open a line of journey, timeline, quadrantChart or pie, whatever their case. */
const KEYWORDS = /^(section|title|journey|timeline|quadrantchart|x-axis|y-axis|quadrant-[1-4]|pie|showdata|acctitle|accdescr|classdef|class|style)(\b|$)/i;

/** A score from 1 to 5, as journey and the matrix of the actions read it. */
const score = (value: number): number => Math.min(5, Math.max(1, Math.round(value)));

/** The title line of a diagram; none for an empty title, the heading above the diagram naming it already. */
const titleLine = (title: string): string[] => (title.trim() === '' ? [] : [`  title ${plain(title, 60)}`]);

/** A section of a journey: its title and its steps, each with its score from 1 to 5. */
export interface JourneySection {
  readonly title: string;
  readonly tasks: readonly { readonly title: string; readonly score: number }[];
}

/**
 * The header of a journey: the font of the diagrams at 13 px, its lines 1.25 high whatever the page around it, a margin
 * of 6 px inside each box (the width of a line measured below); under the 16 px and the lines 1.6 high of a preview of
 * Markdown, two lines no longer held in a box of Mermaid 11.4.
 */
const JOURNEY_INIT = `%%{init: {"theme": "neutral", "themeVariables": {"fontFamily": "${FONT_FAMILY}", "fontSize": "13px"}, "themeCSS": "foreignObject div { line-height: 1.25; } foreignObject div div { padding: 0 6px; }"}}%%`;

/** The font of the texts of a journey, as its header sets it. */
const BOX_FONT = { size: 13, weight: 400 };

/** The width a text takes in a box of a journey: the box (150 px, the default of Mermaid) less a margin on each side. */
const BOX_LINE = 136;

/** The width of a box and the space between two boxes of a journey, as Mermaid draws them: a section spans its steps. */
const [BOX_WIDTH, BOX_GAP] = [150, 50];

/** The most lines of a text in a box of a journey: two hold in its 50 px whatever the height of the lines of the page. */
const BOX_ROWS = 2;

/** A run of characters of a wide script, one by one, or of other characters, whole. */
const RUNS = new RegExp(`${WIDE_CHARACTER.source}|(?:(?!${WIDE_CHARACTER.source}).)+`, 'gu');

/** The pieces a line may break between: a word, a word too wide cut after a hyphen, each character of a wide script alone. */
function piecesOf(text: string, width: number): { readonly piece: string; readonly glue: string }[] {
  const out: { piece: string; glue: string }[] = [];
  for (const word of text.split(' ').filter((item) => item !== ''))
    (word.match(RUNS) ?? []).forEach((run, index) => {
      let [rest, glue] = [run, index === 0 ? ' ' : ''];
      while (arialMeasure(rest, BOX_FONT) > width) {
        let head = graphemes(rest).slice(0, -1).join('');
        while (graphemes(head).length > 1 && arialMeasure(`${head}-`, BOX_FONT) > width) head = graphemes(head).slice(0, -1).join('');
        out.push({ piece: `${head}-`, glue });
        [rest, glue] = [rest.slice(head.length), ''];
      }
      out.push({ piece: rest, glue });
    });
  return out;
}

/**
 * A text for a box of a journey, measured with the widths of Arial: in two lines at most, as a browser breaks them at
 * its spaces, after a hyphen and between two characters of a wide script; a word wider than a line cut with a hyphen;
 * beyond two lines, the end replaced by an ellipsis, the whole text being in the table under the diagram.
 */
export function boxText(text: string, width = BOX_LINE): string {
  const fits = (line: string) => arialMeasure(line, BOX_FONT) <= width;
  let [done, last, rows] = ['', '', 1];
  for (const { piece, glue } of piecesOf(cleaned(text), width)) {
    if (last === '' || fits(`${last}${glue}${piece}`)) last = last === '' ? piece : `${last}${glue}${piece}`;
    else if (rows < BOX_ROWS) [done, last, rows] = [`${done}${last}${glue}`, piece, rows + 1];
    else {
      let kept = last.replace(/[\s-]+$/u, '');
      while (kept !== '' && !fits(`${kept}…`)) kept = graphemes(kept).slice(0, -1).join('').replace(/[\s-]+$/u, '');
      return `${done}${kept}…`;
    }
  }
  return `${done}${last}`;
}

/**
 * The steps as they are lived (journey): a section per chapter, a task per step and its score; an empty section left out.
 * No actor: their legend is drawn left of the boxes, in a margin Mermaid 11.4 does not widen, where a long name runs under
 * the first section; the lane of each step is in the table under the diagram.
 */
export function journeyOf(title: string, sections: readonly JourneySection[]): string {
  const out = [JOURNEY_INIT, 'journey', ...titleLine(title)];
  sections.filter((item) => item.tasks.length > 0).forEach((section, index) => {
    const span = section.tasks.length * (BOX_WIDTH + BOX_GAP) - BOX_GAP - (BOX_WIDTH - BOX_LINE);
    // A title made empty by its signs: the section numbered, never a line « section » alone.
    out.push(`  section ${boxText(section.title, span) || String(index + 1)}`);
    for (const task of section.tasks) out.push(`    ${boxText(task.title) || '-'}: ${String(score(task.score))}`);
  });
  return out.join('\n');
}

/** A point of the matrix of the actions: its short name and its scores from 1 to 5. */
export interface QuadrantPoint {
  readonly name: string;
  readonly effort: number;
  readonly impact: number;
}

/** The texts of the matrix: its title, its axes, then its quadrants, top right, top left, bottom left, bottom right. */
export interface QuadrantTexts {
  readonly title: string;
  readonly x: readonly [string, string];
  readonly y: readonly [string, string];
  readonly quadrants: readonly [string, string, string, string];
}

/** The places of the scores 1 to 5 on each axis: on the height, away from the band of the titles of the quadrants. */
const ACROSS = [0.1, 0.3, 0.55, 0.75, 0.9];
const UP = [0.1, 0.25, 0.6, 0.72, 0.84];

/**
 * A text of the matrix in quotes: unquoted, a Mermaid before 11.5 (11.4 in Markdown Preview Enhanced) reads there the ASCII
 * letters only and stops on « À », « É » or any letter of Cyrillic, Greek or Chinese; in quotes every version reads it.
 */
const inQuotes = (text: string, max = SHORT_MAX): string => `"${plain(text, max) || '-'}"`;

/**
 * The matrix of the actions (quadrantChart) by effort across and impact up; the actions of the same scores one point named
 * by all of them, so that no name covers another (rendu Mermaid, exigence 2); the names short, the actions themselves in the table.
 */
export function quadrantOf(texts: QuadrantTexts, points: readonly QuadrantPoint[]): string {
  const out = [mermaidInit(), 'quadrantChart', ...titleLine(texts.title), `  x-axis ${inQuotes(texts.x[0])} --> ${inQuotes(texts.x[1])}`,
    `  y-axis ${inQuotes(texts.y[0])} --> ${inQuotes(texts.y[1])}`, ...texts.quadrants.map((text, index) => `  quadrant-${String(index + 1)} ${inQuotes(text)}`)];
  // The actions of the same scores are one point, named by all of them: two points never fall on one another.
  const cells = new Map<string, { effort: number; impact: number; names: string[] }>();
  for (const point of points) {
    const [effort, impact] = [score(point.effort), score(point.impact)];
    const cell = cells.get(`${String(effort)}:${String(impact)}`) ?? { effort, impact, names: [] };
    cell.names.push(plain(point.name, 10));
    cells.set(`${String(effort)}:${String(impact)}`, cell);
  }
  for (const cell of cells.values())
    out.push(`  ${inQuotes(cell.names.join(' '), 40)}: [${(ACROSS[cell.effort - 1] ?? 0.5).toFixed(2)}, ${(UP[cell.impact - 1] ?? 0.5).toFixed(2)}]`);
  return out.join('\n');
}

/** The plan of the actions (timeline): a period per wave, its actions by their short names; a period without action left out. */
export function timelineOf(title: string, periods: readonly { readonly title: string; readonly events: readonly string[] }[]): string {
  const out = [mermaidInit(), 'timeline', ...titleLine(title)];
  for (const period of periods.filter((item) => item.events.length > 0)) out.push(`  ${plain(period.title)} : ${period.events.map((event) => plain(event)).join(' : ')}`);
  return out.join('\n');
}

/** The most parts of a pie: beyond, the smallest go together under one label (rendu Mermaid, exigence 2: a legend that fits). */
const PARTS_MAX = 6;

/** A pie of the parts given, the largest first, six at most; the parts of nothing left out. */
export function pieOf(title: string, parts: readonly { readonly label: string; readonly value: number }[], others: string): string {
  const sorted = parts.filter((part) => part.value > 0).sort((a, b) => b.value - a.value);
  const kept = sorted.length <= PARTS_MAX ? sorted
    : [...sorted.slice(0, PARTS_MAX - 1), { label: others, value: sorted.slice(PARTS_MAX - 1).reduce((sum, part) => sum + part.value, 0) }];
  // Mermaid keeps one part of a label: two labels the same once cut are told apart by a number.
  const used = new Set<string>();
  const unique = (label: string): string => {
    let text = plain(label) || '-';
    for (let number = 2; used.has(text); number++) text = `${plain(label, SHORT_MAX - 5) || '-'} (${String(number)})`;
    used.add(text);
    return text;
  };
  return [mermaidInit(), 'pie showData', `  title ${plain(title, 60)}`, ...kept.map((part) => `  "${unique(part.label)}" : ${String(part.value)}`)].join('\n');
}