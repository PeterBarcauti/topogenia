// tableImport.ts
// A table opened as a process (plan 3.1, L5): its columns recognised in French and in English, by the names of Visio Data Visualizer and by those of the workbook the application exports, then written as a .pmap without columns, which the opening places itself; what is guessed or set aside said as uncertainties, every text as the host takes it.

import type { Schema } from '../../../api/client.ts';
import { PRODUCT_NAME, type Translate } from '../../../shared/i18n/i18n.ts';
import type { TableSheet } from './tableRead.ts';

type StepType = Schema<'StepType'>;
type Letter = 'A' | 'S' | 'C' | 'I';
type Role = 'id' | 'label' | 'lane' | 'next' | 'connector' | 'type' | 'time' | Letter | 'raci' | 'phase';

/** A refusal of a file converted, by its key in the files of the languages and its values; its cause when it comes of another failure. */
export class TableError extends Error {
  constructor(readonly key: string, readonly values: Record<string, string | number> = {}, options?: ErrorOptions) {
    super(key, options);
  }
}

/** A header written plainly, in any script: lower case, without accents, signs nor spaces. */
export const plainHeader = (text: string): string => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().replace(/[^\p{L}\p{N}]/gu, '');

/** The names of each column, written plainly: French, English, Visio Data Visualizer (in both languages). */
const NAMES: Readonly<Record<Role, readonly string[]>> = {
  id: ['id', 'identifiant', 'code', 'stepid', 'processstepid', 'iddetape', 'iddeletape', 'iddetapeduprocessus', 'iddeletapeduprocessus', 'numero'],
  label: ['etape', 'libelle', 'description', 'step', 'label', 'name', 'nom', 'processstepdescription', 'descriptiondeletapeduprocessus', 'descriptiondetapeduprocessus',
    'activite', 'activity', 'tache', 'task'],
  lane: ['couloir', 'fonction', 'function', 'swimlane', 'lane', 'acteur', 'actor', 'role', 'responsable', 'r', 'realise', 'responsible'],
  next: ['suivant', 'suivants', 'suivante', 'suivantes', 'etapesuivante', 'etapessuivantes', 'next', 'nextstep', 'nextstepid', 'iddeletapesuivante', 'iddetapesuivante'],
  connector: ['libellefleche', 'libelledelafleche', 'libelledulien', 'connectorlabel', 'etiquetteduconnecteur', 'etiquette', 'condition', 'flowlabel'],
  type: ['type', 'shapetype', 'typedeforme', 'forme', 'shape'],
  time: ['temps', 'delai', 'duree', 'leadtime', 'time', 'duration'],
  A: ['a', 'approuve', 'accountable'], S: ['s', 'soutient', 'supportive'], C: ['c', 'consulte', 'consulted'], I: ['i', 'informe', 'informed'],
  raci: ['raci', 'rasci'],
  phase: ['phase', 'jalon', 'milestone'],
};

/** The types of step by the words a table writes for them, plainly; `terminal` a start or an end by its flows. */
const TYPE_WORDS: ReadonlyMap<string, StepType | 'terminal'> = new Map<string, StepType | 'terminal'>([
  ...['task', 'tache', 'etape', 'process', 'processus', 'activite', 'activity', 'document', 'subprocess', 'sousprocessus', 'predefinedprocess'].map((word) => [word, 'task'] as const),
  ['decision', 'decision'], ...['parallel', 'parallele', 'et', 'and'].map((word) => [word, 'parallel'] as const),
  ...['inclusive', 'inclusif', 'ou', 'or'].map((word) => [word, 'inclusive'] as const), ...['wait', 'attente', 'delay'].map((word) => [word, 'wait'] as const),
  ...['start', 'debut', 'depart', 'begin'].map((word) => [word, 'start'] as const), ...['end', 'fin', 'stop'].map((word) => [word, 'end'] as const),
  ...['startend', 'debutfin', 'terminateur', 'terminator'].map((word) => [word, 'terminal'] as const),
]);

const STEP_TYPES: readonly StepType[] = ['task', 'decision', 'parallel', 'inclusive', 'wait', 'start', 'end'];
const LETTERS: readonly Letter[] = ['A', 'S', 'C', 'I'];

/** The names of the columns of the workbook of the application in the language of the interface: it opens again as it was written. */
function ownNames(t: Translate): Partial<Record<Role, string[]>> {
  const names: Partial<Record<Role, string[]>> = {
    id: [plainHeader(t('raci.column_code'))], label: [plainHeader(t('raci.column_step'))], type: [plainHeader(t('export.type'))],
    lane: [plainHeader(t('raci.names.R'))], next: [plainHeader(t('export.next'))],
    ...Object.fromEntries(LETTERS.map((letter) => [letter, [plainHeader(t(`raci.names.${letter}`))]])),
  };
  return Object.fromEntries(Object.entries(names).map(([role, list]) => [role, list.filter((name) => name !== '')]));
}

/** The role of each column of a header row: the first role that knows its name; a column twice named keeps the first; an empty name none. */
function rolesOf(header: readonly string[], t: Translate): Map<Role, number> {
  const own = ownNames(t);
  const roles = new Map<Role, number>();
  header.forEach((cell, index) => {
    const plain = plainHeader(cell);
    if (plain === '') return;
    const role = (Object.keys(NAMES) as Role[]).find((name) => NAMES[name].includes(plain) || (own[name]?.includes(plain) ?? false));
    if (role !== undefined && !roles.has(role)) roles.set(role, index);
  });
  return roles;
}

/** The table of the steps: the first sheet, among its first five rows, whose header names a label with an identifier or a lane. */
function stepsTable(sheets: readonly TableSheet[], t: Translate) {
  for (const sheet of sheets)
    for (let row = 0; row < Math.min(5, sheet.rows.length); row++) {
      const roles = rolesOf(sheet.rows[row] ?? [], t);
      if (roles.has('label') && (roles.has('id') || roles.has('lane'))) return { sheet, columns: roles, start: row + 1 };
    }
  throw new TableError('files.table.no_header');
}

/** The flows of the workbook of the application, from its sheet of links (from, to, label), when it has one. */
function linksTable(sheets: readonly TableSheet[], t: Translate): { from: string; to: string; label: string; line: number }[] | null {
  const [from = '', to = '', label = ''] = [t('export.from'), t('export.to'), t('export.label')].map(plainHeader);
  if (from === '' || to === '') return null;
  for (const sheet of sheets) {
    const header = (sheet.rows[0] ?? []).map(plainHeader);
    const [source, target, text] = [header.indexOf(from), header.indexOf(to), label === '' ? -1 : header.indexOf(label)];
    if (source >= 0 && target >= 0)
      return sheet.rows.slice(1).map((row, index) => ({ from: row[source] ?? '', to: row[target] ?? '', label: text >= 0 ? row[text] ?? '' : '', line: index + 2 }));
  }
  return null;
}

/** A step read from a row: its key, its label, its lane, its type as written, the keys and labels of its flows, its time, its roles and the parts of RACI not read. */
interface Row {
  readonly line: number;
  readonly key: string;
  readonly label: string;
  readonly lane: string;
  readonly type: string;
  readonly next: readonly string[];
  readonly connectors: readonly string[];
  readonly time: string;
  readonly given: readonly (readonly [string, Letter])[];
  readonly unread: readonly string[];
}

/** A list in a cell: its parts between commas, semicolons or line ends. */
const listOf = (cell: string): string[] => cell.split(/[,;\r\n]/).map((part) => part.trim()).filter((part) => part !== '');

/** The roles a row gives to other lanes, in the order of the table: one column per letter, or a column RACI written « A : Direction ; C : RH, Juridique ». */
function rolesOfRow(cells: readonly string[], roles: ReadonlyMap<Role, number>): { given: [string, Letter][]; unread: string[] } {
  const given: [string, Letter][] = [];
  const unread: string[] = [];
  for (const letter of LETTERS) for (const lane of listOf(cells[roles.get(letter) ?? -1] ?? '')) given.push([lane, letter]);
  for (const part of (cells[roles.get('raci') ?? -1] ?? '').split(/[;\r\n]+/).map((text) => text.trim()).filter((text) => text !== '')) {
    const named = /^([ASCI])\s*[:=]\s*([\s\S]+)$/i.exec(part);
    if (named?.[1] === undefined || named[2] === undefined) unread.push(part);
    else for (const lane of listOf(named[2])) given.push([lane, named[1].toUpperCase() as Letter]);
  }
  return { given, unread };
}

/** The rows of the table of the steps; a row without label nor key left out; each its line in the file. */
function rowsOf(table: ReturnType<typeof stepsTable>): Row[] {
  const { sheet, columns: roles, start } = table;
  const cell = (cells: readonly string[], role: Role) => (cells[roles.get(role) ?? -1] ?? '').trim();
  return sheet.rows.slice(start).map((cells, index) => ({
    line: start + index + 1, key: cell(cells, 'id'), label: cell(cells, 'label'), lane: cell(cells, 'lane'), type: cell(cells, 'type'),
    next: listOf(cell(cells, 'next')), connectors: cell(cells, 'connector').split(/[,;\r\n]/).map((part) => part.trim()), time: cell(cells, 'time'),
    ...rolesOfRow(cells, roles),
  })).filter((row) => row.label !== '' || row.key !== '');
}

/** The longest delay of the format, in its unit (format-pmap.md: `lead_time.value`, 0 to 100 000). */
const DELAY_MAX = 100_000;

/** A delay written « 2 », « 2,5 h », « 30 min » or « 1 j »: its value and its unit, the hour by default; null when it is none or out of the format. */
export function leadTimeOf(text: string): { value: number; unit: 'minute' | 'hour' | 'day' } | null {
  const found = /^(\d{1,9}(?:[.,]\d{1,6})?)\s*(min|mn|minutes?|h|hr?s?|heures?|hours?|j|jours?|d|days?)?$/i.exec(text.trim());
  if (found?.[1] === undefined) return null;
  const value = Number(found[1].replace(',', '.'));
  if (!Number.isFinite(value) || value > DELAY_MAX) return null;
  const unit = (found[2] ?? 'h').toLowerCase();
  return { value, unit: unit.startsWith('m') ? 'minute' : /^(j|d)/.test(unit) ? 'day' : 'hour' };
}

/**
 * A text as the host takes it (TextRules): its control characters but the line ends and the tabs left out first, then its
 * tags of HTML, then a « < » before a letter spaced so that it reads as no tag.
 */
export function plainText(text: string): string {
  return text.replace(/(?![\t\n\r])\p{Cc}/gu, '').replace(/<\/?[a-zA-Z!?][^<>]*>/g, '').replace(/<(?=[a-zA-Z!/?])/g, '< ');
}

/** The text of a field cut at its bound, said when it is cut; a character outside the first plane never cut in two. */
export function bounded(text: string, max: number, said: () => void): string {
  if (text.length <= max) return text;
  said();
  const end = /[\uD800-\uDBFF]/.test(text[max - 2] ?? '') ? max - 2 : max - 1;
  return `${text.slice(0, end).trimEnd()}…`;
}

/**
 * An uncertainty in the language of the interface, its values of the file cut at 60 characters and the whole at the 500 the
 * host takes (TextRules): a long text quoted never makes the file refused.
 */
export function doubtOf(t: Translate, key: string, values: Record<string, string | number>): string {
  const short = Object.fromEntries(Object.entries(values).map(([name, value]) => [name, typeof value === 'string' ? bounded(value, 60, () => undefined) : value]));
  return bounded(t(key, short), 500, () => undefined);
}

/** The marker of a file of the application, as the `.pmap` writes it. */
export const FORMAT = PRODUCT_NAME.toLowerCase();

/** The bounds of the format (format-pmap.md, sections 2.3 and 4). */
const BOUNDS = { steps: 5000, lanes: 200, label: 150, lane: 150, flow: 60, project: 200, doubts: 50 } as const;

/** What the building of the file keeps: its lanes by name, its steps by the key of the table, the uncertainties to say. */
class Building {
  readonly lanes = new Map<string, { id: string; name: string }>();
  readonly ids = new Map<string, string>();
  readonly doubts: string[] = [];

  constructor(private readonly t: Translate, readonly letters: ReadonlySet<string>) {}

  doubt(key: string, values: Record<string, string | number> = {}): void {
    if (this.doubts.length < BOUNDS.doubts) this.doubts.push(doubtOf(this.t, `files.table.${key}`, values));
  }

  /** The lane of a name, made at its first use, in the order of the table; the lane by default for a row without one. */
  lane(name: string, line: number): string {
    const shown = name === '' ? this.t('files.table.default_lane') : bounded(name, BOUNDS.lane, () => { this.doubt('lane_cut', { row: line }); });
    const known = this.lanes.get(shown);
    if (known !== undefined) return known.id;
    if (this.lanes.size === BOUNDS.lanes) throw new TableError('files.table.too_many_lanes', { max: BOUNDS.lanes });
    const lane = { id: `c${String(this.lanes.size + 1)}`, name: shown };
    this.lanes.set(shown, lane);
    return lane.id;
  }

  /** The type of a row: its word, in the language of the interface or among those of Visio; a start or an end by its flows; a task otherwise. */
  type(row: Row, reached: boolean): StepType {
    if (row.type === '') return 'task';
    const plain = plainHeader(row.type);
    const word = STEP_TYPES.find((type) => type === plain || plainHeader(this.t(`map.step_types.${type}`)) === plain) ?? TYPE_WORDS.get(plain);
    if (word === 'terminal') return reached ? 'end' : 'start';
    if (word !== undefined) return word;
    this.doubt('unknown_type', { row: row.line, type: row.type });
    return 'task';
  }

  /** The roles of a row the host takes: the letters of the installation only, one approver, the others said. */
  raciOf(row: Row, lane: string): Record<string, Letter> {
    const raci: Record<string, Letter> = {};
    const refused = new Set<string>();
    let approver = false;
    for (const [name, letter] of row.given) {
      if (!this.letters.has(letter)) {
        if (!refused.has(letter)) this.doubt('raci_letter', { row: row.line, letter });
        refused.add(letter);
        continue;
      }
      if (letter === 'A' && approver) continue;
      const other = this.lane(name, row.line);
      if (other === lane || other in raci) continue;
      approver ||= letter === 'A';
      raci[other] = letter;
    }
    if (row.given.filter(([, letter]) => letter === 'A').length > 1 && this.letters.has('A')) this.doubt('two_approvers', { row: row.line });
    for (const text of row.unread) this.doubt('raci_unread', { row: row.line, text });
    return raci;
  }
}

/** The flows of the table: from its sheet of links when it is the workbook of the application, from the column of the next steps otherwise. */
function flowsOf(rows: readonly Row[], links: ReturnType<typeof linksTable>, building: Building) {
  const pairs = links ?? rows.flatMap((row) => row.next.map((next, index) => ({ from: row.key, to: next, label: row.connectors[index] ?? '', line: row.line })));
  const flows: { source: string; target: string; label?: string }[] = [];
  const seen = new Set<string>();
  for (const pair of pairs) {
    const [source, target] = [building.ids.get(pair.from.trim()), building.ids.get(pair.to.trim())];
    const { line } = pair;
    if (source === undefined || target === undefined) {
      if (pair.to.trim() !== '') building.doubt('unknown_next', { row: line, key: source === undefined ? pair.from : pair.to });
      continue;
    }
    if (source === target || seen.has(`${source}>${target}`)) continue;
    seen.add(`${source}>${target}`);
    const label = pair.label.trim() === '' ? undefined : bounded(pair.label.trim(), BOUNDS.flow, () => { building.doubt('flow_label_cut', { row: line }); });
    flows.push({ source, target, ...(label === undefined ? {} : { label }) });
  }
  return flows;
}

/** A step of the file: its lane, label and type, its roles and its delay when its type may carry them (format-pmap.md, section 4, rule 4). */
function stepOf(row: Row, id: string, reached: boolean, building: Building) {
  const lane = building.lane(row.lane, row.line);
  const label = bounded(row.label === '' ? row.key : row.label, BOUNDS.label, () => { building.doubt('label_cut', { row: row.line }); });
  if (row.label === '') building.doubt('no_label', { row: row.line });
  const type = building.type(row, reached);
  const plain = type === 'task' || type === 'decision' || type === 'wait';
  const raci = building.raciOf(row, lane);
  const delay = row.time === '' ? null : leadTimeOf(row.time);
  if (row.time !== '' && delay === null) building.doubt('unknown_time', { row: row.line, time: row.time });
  if (!plain && (Object.keys(raci).length > 0 || delay !== null)) building.doubt('no_roles_for_type', { row: row.line });
  return {
    id, lane, label, ...(type === 'task' ? {} : { type }),
    ...(plain && Object.keys(raci).length > 0 ? { raci } : {}), ...(plain && delay !== null ? { sheet: { lead_time: delay } } : {}),
  };
}

/**
 * The table as the text of a `.pmap` 1.2 without columns: its lanes in the order they come, its steps, its flows, its
 * roles of the letters of the installation (`ACI` unless told) and its delays; the rows twice keyed, the next steps
 * unknown, the words not understood and the texts cut said as uncertainties, which the opening shows. TableError when no
 * table of steps is found, or when it exceeds the format.
 */
export function tableToPmap(sheets: readonly TableSheet[], name: string, t: Translate, letters = 'ACI'): string {
  const plain = sheets.map((sheet) => ({ name: sheet.name, rows: sheet.rows.map((cells) => cells.map(plainText)) }));
  const table = stepsTable(plain, t);
  const rows = rowsOf(table);
  if (rows.length === 0) throw new TableError('files.table.empty');
  if (rows.length > BOUNDS.steps) throw new TableError('files.table.too_many', { max: BOUNDS.steps });
  const building = new Building(t, new Set(letters.toUpperCase()));
  const kept = rows.filter((row, index) => {
    const key = row.key === '' ? `#${String(row.line)}` : row.key;
    if (building.ids.has(key)) {
      building.doubt('duplicate', { row: row.line, key });
      return false;
    }
    building.ids.set(key, `s${String(index + 1)}`);
    return true;
  }).map((row) => (row.key === '' ? { ...row, key: `#${String(row.line)}` } : row));
  const flows = flowsOf(kept, linksTable(plain, t), building);
  const reached = new Set(flows.map((flow) => flow.target));
  const steps = kept.map((row) => stepOf(row, building.ids.get(row.key) ?? '', reached.has(building.ids.get(row.key) ?? ''), building));
  if (table.columns.has('phase') && rows.some((row) => row.label !== '')) building.doubt('phase_ignored');
  const lanes = [...building.lanes.values()];
  const project = plainText(name).trim();
  const file = {
    format: FORMAT, version: '1.2', project: { name: bounded(project === '' ? t('files.table.default_project') : project, BOUNDS.project, () => undefined) },
    map: { lanes, steps, flows }, ...(building.doubts.length === 0 ? {} : { uncertainties: building.doubts }),
  };
  return `${JSON.stringify(file, null, 2)}\n`;
}
