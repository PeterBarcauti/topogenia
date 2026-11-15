// mermaidImport.ts
// A Mermaid flowchart opened as a process (plan 3.1, L6), read by a parser of its own, bounded, of the subset `flowchart` and `graph`: its shapes as types of step, its links and their labels as flows, its subgraphs as lanes; its styles set aside and said; what the export of the application writes read back as it was; every reading in one pass forward, in a time bounded by the length of the text.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { LINE_MAX } from '../../map/export/mermaid.ts';
import { bounded, doubtOf, FORMAT, plainText, TableError } from './tableImport.ts';

type StepType = Schema<'StepType'>;

/** A node as the text names it: its label and its shape; its lane, the innermost subgraph it was first named in. */
interface Node {
  readonly key: string;
  label: string | null;
  shape: string | null;
  lane: string | null;
}

/** A node as a statement gives it: its key, its shape and its label when given, whether a class was set on it. */
interface Named {
  readonly key: string;
  readonly shape: string | null;
  readonly label: string | null;
  readonly styled: boolean;
}

/** A statement and its line in the file. */
interface Statement {
  readonly text: string;
  readonly line: number;
}

/** The bounds of the reading: the lines and the statements read, the steps and the links of the format. */
const BOUNDS = { lines: 20_000, statement: 2000, steps: 5000, links: 20_000, lanes: 200, label: 150, flow: 60, lane: 150, project: 200 } as const;

/** The shapes of Mermaid, the longest opening first, each with its closings. */
const SHAPES: readonly (readonly [string, readonly string[]])[] = [
  ['(((', [')))']], ['((', ['))']], ['([', ['])']], ['[[', [']]']], ['[(', [')]']], ['{{', ['}}']], ['[/', ['/]', '\\]']], ['[\\', ['\\]', '/]']],
  ['(', [')']], ['[', [']']], ['{', ['}']], ['>', [']']],
];

/** The shapes named by Mermaid 11 (`A@{ shape: diam }`) as the opening of the former syntax they draw; any other a box. */
const NAMED_SHAPES: Readonly<Record<string, string>> = {
  diam: '{', diamond: '{', decision: '{', question: '{', stadium: '([', pill: '([', terminal: '([', circle: '((', circ: '((',
  'sm-circ': '((', 'small-circle': '((', start: '((', 'dbl-circ': '(((', 'double-circle': '(((', 'fr-circ': '(((', 'framed-circle': '(((',
  stop: '(((', subproc: '[[', subprocess: '[[', subroutine: '[[', 'fr-rect': '[[', 'framed-rectangle': '[[', odd: '>', delay: '>',
  hex: '{{', hexagon: '{{', prepare: '{{', rounded: '(', event: '(',
};

/** The words that only style a diagram: set aside, said once; `accTitle` and `accDescr` before their `:` or their `{`. */
const STYLES = ['classDef', 'class', 'style', 'linkStyle', 'click', 'accTitle', 'accDescr'];

/** What follows `direction` when it is the direction of a subgraph, as Mermaid 11 reads it; otherwise `direction` is a node. */
const DIRECTION = /^\s+(?:TB|TD|BT|RL|LR)\b/;

/** A code of a step, which the export of the application writes on its first line: read back, it is not of the label. */
const CODE_LINE = /^[A-Z][A-Z0-9]{1,15}-[A-Z][0-9]{2,6}$/;

/** The key of a node: letters, digits and underscores, joined by a hyphen or a dot. */
const KEY = /^[\p{L}\p{N}_]+(?:[-.][\p{L}\p{N}_]+)*/u;

/** A code point written `#60;` kept when it is a character a text may hold: no control character, no half of a pair. */
const characterOf = (code: number): string | null =>
  code < 32 || (code >= 127 && code < 160) || (code >= 0xd800 && code <= 0xdfff) || code > 0x10ffff ? null : String.fromCodePoint(code);

/** A text of Mermaid as it reads, in one pass: its quotes, the backticks of a text of Markdown, its line breaks, its entity codes. */
export function mermaidText(text: string): string {
  const codes: Record<string, string> = { quot: '"', lt: '<', gt: '>', amp: '&', nbsp: ' ', apos: '\'' };
  return text.replace(/^"([\s\S]*)"$/, '$1').replace(/^`([\s\S]*)`$/, '$1').replace(/<br\s*\/?>/gi, '\n')
    .replace(/#(\d{1,7}|[a-z]{2,6});/gi, (whole: string, code: string) => (/^\d/.test(code) ? characterOf(Number(code)) : codes[code.toLowerCase()]) ?? whole);
}

/** A part of a text with its line ends kept, its other characters left out. */
const blank = (part: string): string => part.replace(/[^\n]/g, '');

/**
 * A text with its lines kept blank where it only titles and sets a diagram: its front matter, its directives `%%{…}%%`;
 * each directive found by one search forward, never a search that comes back (a text of directives never closed).
 */
function blanked(text: string): string {
  const matter = /^\s*---\r?\n/.exec(text);
  const close = matter === null ? -1 : text.indexOf('\n---', matter[0].length - 1);
  let rest = close < 0 ? text : blank(text.slice(0, close + 4)) + text.slice(close + 4).replace(/^[^\n]*/, (line) => blank(line));
  let out = '';
  for (let start = rest.indexOf('%%{'); start >= 0; start = rest.indexOf('%%{')) {
    const end = rest.indexOf('}%%', start + 3);
    if (end < 0) break;
    out += rest.slice(0, start) + blank(rest.slice(start, end + 3));
    rest = rest.slice(end + 3);
  }
  return out + rest;
}

/** The diagram of a file and the number of lines before it: a Markdown gives its first block `mermaid` that is a flowchart, read line by line. */
export function diagramOf(text: string, markdown: boolean): { diagram: string; offset: number } {
  if (!markdown) return { diagram: blanked(text), offset: 0 };
  const lines = text.split('\n');
  for (let open = 0; open < lines.length; open++) {
    if (!(lines[open] ?? '').startsWith('```mermaid')) continue;
    let close = open + 1;
    while (close < lines.length && (lines[close] ?? '').trimEnd() !== '```') close++;
    if (close === lines.length) break;
    const diagram = blanked(lines.slice(open + 1, close).join('\n'));
    if (/^\s*(?:%%[^\n]*\n\s*)*(flowchart|graph)\b/.test(diagram)) return { diagram, offset: open + 1 };
    open = close;
  }
  throw new TableError('files.mermaid.no_block');
}

/** The statements of one line: split at the semicolons outside quotes and brackets, its comment `%%` left out. */
function splitLine(line: string, at: number): Statement[] {
  const out: Statement[] = [];
  let depth = 0;
  let quoted = false;
  let start = 0;
  for (let index = 0; index <= line.length; index++) {
    const char = line[index];
    const comment = !quoted && char === '%' && line[index + 1] === '%';
    if (char === '"') quoted = !quoted;
    else if (!quoted && (char === '[' || char === '(' || char === '{')) depth++;
    else if (!quoted && (char === ']' || char === ')' || char === '}')) depth = Math.max(0, depth - 1);
    if (index === line.length || comment || (char === ';' && !quoted && depth === 0)) {
      const text = line.slice(start, index).trim();
      if (text !== '') out.push({ text, line: at });
      if (comment) break;
      start = index + 1;
    }
  }
  return out;
}

/** The lines of a text written back as one: a word the export cut at the end of a full line joined again without its hyphen. */
function joined(lines: readonly string[]): string {
  return lines.reduce((text, line, index) => {
    if (index === 0) return line;
    const before = lines[index - 1] ?? '';
    return before.length === LINE_MAX && before.endsWith('-') ? `${text.slice(0, -1)}${line}` : `${text} ${line}`;
  }, '');
}

/** The lines of a text as the host takes them, their spaces folded, the empty ones left out. */
const linesOf = (text: string): string[] => plainText(text).split('\n').map((line) => line.replace(/\s+/g, ' ').trim()).filter((line) => line !== '');

/** The fields of a shape of Mermaid 11 (`shape: diam, label: "Conforme ?"`): its parts between the commas outside quotes, each `name: value`, in one pass. */
function fieldsOf(body: string): Map<string, string> {
  const fields = new Map<string, string>();
  let quoted = false;
  let start = 0;
  for (let index = 0; index <= body.length; index++) {
    const char = body[index];
    if (char === '"') quoted = !quoted;
    if (index < body.length && (char !== ',' || quoted)) continue;
    const part = body.slice(start, index);
    const colon = part.indexOf(':');
    if (colon > 0) fields.set(part.slice(0, colon).trim().toLowerCase(), part.slice(colon + 1).trim());
    start = index + 1;
  }
  return fields;
}

/** The reading of one statement: a cursor over its text. */
class Cursor {
  at = 0;

  constructor(readonly text: string) {}

  space(): void {
    while (this.text[this.at] === ' ' || this.text[this.at] === '\t') this.at++;
  }

  /** Whether the statement is read to its end, its spaces skipped. */
  done(): boolean {
    this.space();
    return this.at >= this.text.length;
  }

  /** A node: its key, its shape and label when the text gives them, its class skipped; null when none starts here. */
  node(): Named | null {
    this.space();
    const key = KEY.exec(this.text.slice(this.at))?.[0];
    if (key === undefined) return null;
    this.at += key.length;
    const shaped = this.text.startsWith('@{', this.at) ? this.named() : this.shape();
    if (shaped === null) return null;
    const styled = /^:::[\p{L}\p{N}_-]*/u.exec(this.text.slice(this.at))?.[0] ?? '';
    this.at += styled.length;
    return { key, ...shaped, styled: styled !== '' };
  }

  /** The shape of the former syntax and its label (`A["Saisir"]`); no shape when none opens here, null when it never closes. */
  private shape(): { shape: string | null; label: string | null } | null {
    for (const [open, closes] of SHAPES) {
      if (!this.text.startsWith(open, this.at)) continue;
      const body = this.at + open.length;
      const quoted = this.text[body] === '"' ? this.text.indexOf('"', body + 1) : body - 1;
      const close = closes.map((closing) => ({ closing, index: this.text.indexOf(closing, Math.max(body, quoted + 1)) })).filter((found) => found.index >= 0)
        .sort((a, b) => a.index - b.index)[0];
      if (close === undefined) return null;
      const label = this.text.slice(body, close.index);
      this.at = close.index + close.closing.length;
      return { shape: open, label: mermaidText(label.trim()) };
    }
    return { shape: null, label: null };
  }

  /** The shape of Mermaid 11 and its label (`A@{ shape: diam, label: "Conforme ?" }`); null when the braces never close. */
  private named(): { shape: string | null; label: string | null } | null {
    const body = /^@\{((?:"[^"]*"|[^"}])*)\}/.exec(this.text.slice(this.at));
    if (body === null) return null;
    this.at += body[0].length;
    const fields = fieldsOf(body[1] ?? '');
    const label = fields.get('label');
    return { shape: NAMED_SHAPES[(fields.get('shape') ?? '').toLowerCase()] ?? '[', label: label === undefined ? null : mermaidText(label) };
  }

  /** A link: its label, whether it runs both ways or is hidden; null when none starts here. */
  link(): { label: string; both: boolean; hidden: boolean } | null {
    this.space();
    const rest = this.text.slice(this.at);
    const worded = this.worded(rest);
    const piped = /^(<)?(-{2,}|={2,}|-\.+-|~{3,})(>|o|x)?\s*(?:\|("[^"]*"|[^|]*)\|)?/.exec(rest);
    if (worded !== null && (piped === null || worded.length > piped[0].length)) {
      this.at += worded.length;
      return { label: mermaidText(worded.label.trim()), both: worded.both, hidden: false };
    }
    if (piped === null) return null;
    this.at += piped[0].length;
    return { label: mermaidText((piped[4] ?? '').trim()), both: piped[1] === '<', hidden: (piped[2] ?? '').startsWith('~') };
  }

  /** A link with its label between its two halves (`-- oui -->`), its closing searched once: never a search that comes back. */
  private worded(rest: string): { length: number; label: string; both: boolean } | null {
    const open = /^(<)?(--|==|-\.)\s/.exec(rest);
    if (open === null) return null;
    const closing = /\s(-{2,}|={2,}|\.-+)(>|o|x)?(?=\s|[\p{L}\p{N}_]|$)/gu;
    closing.lastIndex = open[0].length;
    const found = closing.exec(rest);
    if (found === null) return null;
    return { length: found.index + found[0].length, label: rest.slice(open[0].length, found.index), both: open[1] === '<' };
  }
}

/** A link read: its two keys, its label, whether it ran both ways or was hidden, its line. */
interface Link {
  readonly from: string;
  readonly to: string;
  readonly label: string;
  readonly both: boolean;
  readonly hidden: boolean;
  readonly line: number;
}

/** What the reading keeps: the nodes in the order they come, the links, the keys of the subgraphs, the uncertainties. */
class Reading {
  readonly nodes = new Map<string, Node>();
  readonly links: Link[] = [];
  readonly groups = new Set<string>();
  readonly doubts: string[] = [];
  private readonly styled = new Set<string>();

  constructor(readonly t: Translate) {}

  doubt(key: string, values: Record<string, string | number> = {}): void {
    if (this.doubts.length < 50) this.doubts.push(doubtOf(this.t, key, values));
  }

  /** A word of style: said the first time only. */
  style(keyword: string, line: number): void {
    if (!this.styled.has(keyword)) this.doubt('files.mermaid.ignored', { line, keyword });
    this.styled.add(keyword);
  }

  /** A node named: its shape and label the first time they are given, its lane the first subgraph it is named in. */
  name(node: Named, lane: string | null): void {
    const known = this.nodes.get(node.key);
    if (known === undefined) {
      if (this.nodes.size === BOUNDS.steps) throw new TableError('files.table.too_many', { max: BOUNDS.steps });
      this.nodes.set(node.key, { key: node.key, shape: node.shape, label: node.label, lane });
      return;
    }
    if (known.shape === null && node.shape !== null) [known.shape, known.label] = [node.shape, node.label];
    known.lane ??= lane;
  }

  /** A link between two nodes; past the bound of the links, the diagram is refused. */
  join(link: Link): void {
    if (this.links.length === BOUNDS.links) throw new TableError('files.mermaid.too_long', { lines: BOUNDS.lines, links: BOUNDS.links });
    this.links.push(link);
  }
}

/** A subgraph opened: its key when it has one, and its title, `id[Titre]`, `id ["Titre"]`, `"Titre"` or its words. */
function subgraphOf(rest: string): { key: string | null; title: string } {
  const text = rest.trim();
  const bracket = /^([\p{L}\p{N}_-]*)\s*\[(.*)\]$/u.exec(text);
  const key = bracket === null ? (KEY.exec(text)?.[0] === text ? text : null) : bracket[1] === '' ? null : bracket[1] ?? null;
  return { key, title: joined(linesOf(mermaidText((bracket?.[2] ?? text).trim()))) };
}

/**
 * One statement after the header, which is skipped by its place (audit 3.1 V3, U10: a node `flowchart-1` is a node): a
 * subgraph opened or closed, a direction or a style set aside, or a chain of nodes and links (`A & B --> C`).
 */
function readStatement(statement: Statement, stack: string[], reading: Reading): void {
  const { text, line } = statement;
  // The whole first token, as the key of a node: a node named `class1` or `subgraph_2` is not a keyword (audit 3.1 V2, 5.2).
  const word = KEY.exec(text)?.[0] ?? '';
  // A keyword glued to a shape is the key of a node, `direction[Sens]` or `click(Clic)` (audit 3.1 V4, A2).
  const rest = text.slice(word.length);
  const apart = rest === '' || /^\s/.test(rest);
  if (word === 'subgraph' && apart) {
    const { key, title } = subgraphOf(rest);
    if (key !== null) reading.groups.add(key);
    stack.push(title);
  } else if (text === 'end') stack.pop();
  else if (word === 'direction' && DIRECTION.test(rest)) return;
  else if (STYLES.includes(word) && (word.startsWith('acc') ? /^\s*[:{]/.test(rest) : apart)) reading.style(word, line);
  else readChain(new Cursor(text), line, stack.at(-1) ?? null, reading);
}

/** A chain of groups of nodes joined by links, each node of a group linked to each of the next. */
function readChain(cursor: Cursor, line: number, lane: string | null, reading: Reading): void {
  let previous: string[] = [];
  let link: ReturnType<Cursor['link']> = null;
  while (!cursor.done()) {
    const group: string[] = [];
    for (;;) {
      const node = cursor.node();
      if (node === null) {
        reading.doubt('files.mermaid.unreadable', { line });
        return;
      }
      if (node.styled) reading.style(':::', line);
      reading.name(node, lane);
      group.push(node.key);
      cursor.space();
      if (cursor.text[cursor.at] !== '&') break;
      cursor.at++;
    }
    const joining = link;
    if (joining !== null) for (const from of previous) for (const to of group) reading.join({ from, to, ...joining, line });
    previous = group;
    if (cursor.done()) return;
    link = cursor.link();
    if (link === null) {
      reading.doubt('files.mermaid.unreadable', { line });
      return;
    }
  }
}

/** The label of a node in one line: the code the export of the application writes first, and the sign of a gateway, are not of it. */
function labelOf(node: Node): { label: string; sign: string | null } {
  const lines = linesOf(node.label ?? '');
  const sign = node.shape === '{' && (lines[0] === '+' || lines[0] === 'O') ? lines.shift() ?? null : null;
  if (lines.length > 1 && CODE_LINE.test(lines[0] ?? '')) lines.shift();
  return { label: joined(lines), sign };
}

/** The type of a node by its shape; a circle or a stadium a start without flow in, an end without flow out. */
function typeOf(node: Node, sign: string | null, reached: boolean, leaves: boolean, reading: Reading): StepType {
  if (node.shape === '{') return sign === '+' ? 'parallel' : sign === 'O' ? 'inclusive' : 'decision';
  if (node.shape === '(((') return 'end';
  if (node.shape === '((' || node.shape === '([') return !reached ? 'start' : !leaves ? 'end' : 'task';
  if (node.shape === '>') return 'wait';
  if (node.shape === '[[') reading.doubt('files.mermaid.subprocess', { key: node.key });
  return 'task';
}

/** The flows of the links: the hidden ones, those of a node to itself, those to a whole subgraph and the second of a pair left out. */
function flowsOf(reading: Reading, ids: ReadonlyMap<string, string>): { source: string; target: string; label?: string }[] {
  const flows: { source: string; target: string; label?: string }[] = [];
  const seen = new Set<string>();
  for (const link of reading.links) {
    const [source, target] = [ids.get(link.from), ids.get(link.to)];
    if (link.hidden) reading.doubt('files.mermaid.hidden', { line: link.line });
    else if (source === undefined || target === undefined) reading.doubt('files.mermaid.group_link', { line: link.line });
    else if (link.both) reading.doubt('files.mermaid.both_ways', { line: link.line });
    if (link.hidden || source === undefined || target === undefined || source === target || seen.has(`${source}>${target}`)) continue;
    seen.add(`${source}>${target}`);
    const text = joined(linesOf(link.label));
    const label = text === '' ? null : bounded(text, BOUNDS.flow, () => { reading.doubt('files.mermaid.flow_label_cut', { line: link.line }); });
    flows.push({ source, target, ...(label === null ? {} : { label }) });
  }
  return flows;
}

/** The steps of the nodes, each in the lane of its subgraph, and the lanes in the order they come. */
function stepsOf(reading: Reading, nodes: readonly Node[], ids: ReadonlyMap<string, string>, flows: ReturnType<typeof flowsOf>) {
  const lanes = new Map<string, string>();
  const titles = new Map<string | null, string>();
  const laneOf = (title: string | null): string => {
    let shown = titles.get(title);
    if (shown === undefined) {
      shown = bounded(title === null || title === '' ? reading.t('files.table.default_lane') : title, BOUNDS.lane, () => { reading.doubt('files.mermaid.lane_cut'); });
      titles.set(title, shown);
    }
    if (!lanes.has(shown)) {
      if (lanes.size === BOUNDS.lanes) throw new TableError('files.mermaid.too_many_lanes', { max: BOUNDS.lanes });
      lanes.set(shown, `c${String(lanes.size + 1)}`);
    }
    return lanes.get(shown) ?? '';
  };
  const [reached, leaving] = [new Set(flows.map((flow) => flow.target)), new Set(flows.map((flow) => flow.source))];
  const steps = nodes.map((node) => {
    const id = ids.get(node.key) ?? '';
    const { label, sign } = labelOf(node);
    const type = typeOf(node, sign, reached.has(id), leaving.has(id), reading);
    const text = bounded(label === '' ? node.key : label, BOUNDS.label, () => { reading.doubt('files.mermaid.label_cut', { key: node.key }); });
    return { id, lane: laneOf(node.lane), label: text, ...(type === 'task' ? {} : { type }) };
  });
  return { lanes: [...lanes].map(([name, id]) => ({ id, name })), steps };
}

/**
 * A Mermaid flowchart, or the first one of a Markdown, as the text of a `.pmap` 1.2 without columns: a lane per subgraph
 * (the innermost), the nodes outside one in the lane by default, the shapes as types, the links and their labels as flows;
 * the styles, the statements not understood, the links hidden, both ways or to a whole subgraph said as uncertainties.
 * TableError for another diagram, a Markdown without one, none of its nodes, or a diagram past the bounds.
 */
export function mermaidToPmap(text: string, markdown: boolean, name: string, t: Translate): string {
  const { diagram, offset } = diagramOf(text, markdown);
  const lines = diagram.split(/\r?\n/);
  if (lines.length > BOUNDS.lines) throw new TableError('files.mermaid.too_long', { lines: BOUNDS.lines, links: BOUNDS.links });
  const reading = new Reading(t);
  const statements = lines.flatMap((line, index) => {
    if (line.length <= BOUNDS.statement) return splitLine(line, index + 1 + offset);
    if (!line.trimStart().startsWith('%%')) reading.doubt('files.mermaid.unreadable', { line: index + 1 + offset });
    return [];
  });
  if (!/^(flowchart|graph)\b/.test(statements[0]?.text ?? '')) throw new TableError('files.mermaid.not_flowchart');
  const stack: string[] = [];
  // The first statement is the header, checked above: `flowchart-elk TD` as well as `graph LR`.
  for (const statement of statements.slice(1)) readStatement(statement, stack, reading);
  const nodes = [...reading.nodes.values()].filter((node) => node.shape !== null || !reading.groups.has(node.key));
  if (nodes.length === 0) throw new TableError('files.mermaid.empty');
  const ids = new Map(nodes.map((node, index) => [node.key, `s${String(index + 1)}`]));
  const flows = flowsOf(reading, ids);
  const { lanes, steps } = stepsOf(reading, nodes, ids, flows);
  const file = {
    format: FORMAT, version: '1.2',
    project: { name: bounded(plainText(name).trim() === '' ? t('files.table.default_project') : plainText(name).trim(), BOUNDS.project, () => undefined) },
    map: { lanes, steps, flows }, ...(reading.doubts.length === 0 ? {} : { uncertainties: reading.doubts }),
  };
  return `${JSON.stringify(file, null, 2)}\n`;
}
