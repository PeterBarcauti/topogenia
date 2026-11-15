// raci.ts
// The RACI of a map: the R is the lane of the step, the other letters go to other lanes; the roles of a step, the missing approver, the badges, the matrix copied to Excel or PowerPoint (annex B 2.11, A-2.13).

import type { Schema } from '../../../api/client.ts';
import { laneRanks, lanesInOrder, readingOrder } from './readingOrder.ts';
import { hasContent } from './stepFacts.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;
type LaneView = Schema<'LaneView'>;
export type RaciRole = Schema<'RaciRole'>;

/** A letter of a lane for a step: the R of the lane of the step, or a role given (B-2.11-01). */
export type RaciLetter = RaciRole | 'R';

type RaciMap = Pick<MapView, 'raci'>;
type RaciStep = Pick<StepView, 'id' | 'lane_id' | 'type'>;

/** The start and the end are events, the parallel and inclusive gateways join: no actor, no RACI (B-2.11-03, AR-23). */
export const concernedByRaci = (step: Pick<StepView, 'type'>): boolean => hasContent(step.type);

/** The role of each lane for a step, the R of its own lane first; a role left on the lane of the step gives way to the R (A-2.13). */
export function rolesOf(map: RaciMap, step: RaciStep): Map<string, RaciLetter> {
  const roles = new Map<string, RaciLetter>([[step.lane_id, 'R']]);
  for (const role of map.raci) if (role.step_id === step.id && role.lane_id !== step.lane_id) roles.set(role.lane_id, role.role);
  return roles;
}

/** A step concerned that no lane approves: the badge « A ? » (B-2.11-07). */
export const missingApprover = (map: RaciMap, step: RaciStep): boolean =>
  concernedByRaci(step) && !map.raci.some((role) => role.step_id === step.id && role.role === 'A' && role.lane_id !== step.lane_id);

const ROLES: readonly RaciRole[] = ['A', 'S', 'C', 'I'];

/** The letters the map offers besides the R, in the order of its setting, each once: « ACI » or « ASCI » (B-2.11-02). */
export const raciLetters = (map: Pick<MapView, 'raci_letters'>): RaciRole[] =>
  [...new Set(map.raci_letters.toUpperCase())].filter((letter): letter is RaciRole => (ROLES as readonly string[]).includes(letter));

/** A badge under a step: a role and the name of its lane. */
export interface RaciBadge {
  readonly role: RaciRole;
  readonly laneId: string;
  readonly name: string;
}

/** The roles of a step other than R, in the order A, S, C, I, then the order of the lanes (B-2.11-05, B-2.11-06). */
export function raciBadges(map: Pick<MapView, 'raci' | 'lanes'>, step: RaciStep, laneName: (lane: LaneView) => string): RaciBadge[] {
  const lanes = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const ranks = laneRanks(map.lanes);
  return map.raci
    .filter((role) => role.step_id === step.id && role.lane_id !== step.lane_id && lanes.has(role.lane_id))
    .sort((a, b) => ROLES.indexOf(a.role) - ROLES.indexOf(b.role) || (ranks.get(a.lane_id) ?? 0) - (ranks.get(b.lane_id) ?? 0))
    .map((role) => ({ role: role.role, laneId: role.lane_id, name: laneName(lanes.get(role.lane_id) as LaneView) }));
}

/** The formal matrix of a map: its lanes in columns, its steps concerned in rows with the letter of each lane (B-2.11-14). */
export interface RaciMatrix {
  readonly columns: readonly { readonly id: string; readonly name: string }[];
  readonly rows: readonly { readonly id: string; readonly code: string; readonly label: string; readonly cells: readonly (RaciLetter | '')[] }[];
}

/** The matrix of a map: the steps concerned in the reading order (B-8.1-29), the lanes in their order, in the language shown. */
export function raciMatrix(map: MapView, laneName: (lane: LaneView) => string, stepLabel: (step: StepView) => string): RaciMatrix {
  const order = lanesInOrder(map.lanes);
  const lanes = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const steps = map.steps.filter(concernedByRaci).sort(readingOrder(laneRanks(map.lanes)));
  return {
    columns: order.map((id) => ({ id, name: laneName(lanes.get(id) as LaneView) })),
    rows: steps.map((step) => {
      const roles = rolesOf(map, step);
      return { id: step.id, code: step.code, label: stepLabel(step), cells: order.map((id) => roles.get(id) ?? '') };
    }),
  };
}

/** The headers of the two first columns of the matrix, in the language shown. */
export interface MatrixHeaders {
  readonly code: string;
  readonly step: string;
}

const cleaned = (text: string): string => text.replace(/[\t\r\n]+/g, ' ');

/**
 * A cell that a spreadsheet would read as a formula (first character `=`, `+`, `-` or `@`) gets an apostrophe in
 * front, which Excel takes as the mark of a text and does not show: a label received in a package never runs (AR-130).
 */
const inert = (text: string): string => (/^[=+\-@]/.test(text) ? `'${text}` : text);

/** The matrix as tabulated text, pasted as a table in Excel (B-2.11-16). */
export function matrixText(matrix: RaciMatrix, headers: MatrixHeaders): string {
  return [[headers.code, headers.step, ...matrix.columns.map((column) => column.name)], ...matrix.rows.map((row) => [row.code, row.label, ...row.cells])]
    .map((line) => line.map((text) => inert(cleaned(text))).join('\t'))
    .join('\r\n');
}

/**
 * The look of the copied table, read from the tokens of the charter when it is copied: a document pasted in Word or
 * PowerPoint does not read the variables of the page. A value missing leaves its property out.
 */
export interface TableLook {
  readonly border: string;
  readonly header: string;
  readonly font: string;
}

const escaped = (text: string): string => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const css = (properties: readonly (readonly [string, string])[]): string =>
  properties.filter(([, value]) => value.trim() !== '').map(([name, value]) => `${name}:${escaped(value.trim())};`).join('');

/**
 * The matrix as an HTML table, pasted as a real table in Word, PowerPoint and Outlook (B-2.11-15). Every cell has the
 * text format of Excel (`mso-number-format:\@`, which the other programs ignore): pasted in Excel, a label that starts
 * like a formula stays a text (AR-130).
 */
export function matrixHtml(matrix: RaciMatrix, headers: MatrixHeaders, look: TableLook): string {
  const border = look.border.trim() === '' ? '' : `1px solid ${look.border.trim()}`;
  const cell = (text: string, tag: 'td' | 'th', letter = false) => {
    const style = css([['border', border], ['padding', '4px 8px'], ['background', tag === 'th' ? look.header : ''], ['text-align', letter ? 'center' : ''],
      ['font-weight', letter || tag === 'th' ? 'bold' : ''], ['mso-number-format', '\\@']]);
    return `<${tag} style="${style}">${escaped(text)}</${tag}>`;
  };
  const head = `<tr>${[headers.code, headers.step, ...matrix.columns.map((column) => column.name)].map((text) => cell(text, 'th')).join('')}</tr>`;
  const body = matrix.rows.map((row) => `<tr>${cell(row.code, 'td')}${cell(row.label, 'td')}${row.cells.map((letter) => cell(letter, 'td', true)).join('')}</tr>`).join('');
  return `<table style="${css([['border-collapse', 'collapse'], ['font-family', look.font], ['font-size', '11pt']])}">${head}${body}</table>`;
}
