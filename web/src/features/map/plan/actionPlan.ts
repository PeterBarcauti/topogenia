// actionPlan.ts
// The action plan as data (ADR 112): one entry a line of the host, built once and read by the panel and by the sheet of the export; the late mark on the local day, the sorts, the filters and the counts. No screen, no store: the project in PDF reads it too.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';

type ActionPlanView = Schema<'ActionPlanView'>;
type ActionPlanLine = Schema<'ActionPlanLine'>;
type NamedText = Schema<'NamedText'>;

/** The value of a cell of the sheet. */
export type PlanCell = string | number;

/** A line of the plan with the values the sheet writes (eleven cells) and the two facts the panel filters on. */
export interface PlanEntry {
  readonly line: ActionPlanLine;
  /** The row of the sheet: map, step code, step, lane, pain point, description, action, owner, due date, status, hours lost. */
  readonly cells: readonly PlanCell[];
  /** The host says it: neither implemented, nor adopted, nor dropped, as the map shows it. */
  readonly open: boolean;
  /** No action is written (an action of blanks is none). */
  readonly noAction: boolean;
}

/** The sorts of the panel: the order of the project (that of the host), the hours lost, the due date. */
export type PlanSort = 'project' | 'hours' | 'due';

/** The filters of the panel, which narrow together. */
export interface PlanFilters {
  readonly open: boolean;
  readonly late: boolean;
  readonly noAction: boolean;
}

/** The numbers at the head of the panel. */
export interface PlanCounts {
  readonly total: number;
  readonly open: number;
  readonly late: number;
  readonly noAction: number;
}

/** A name of the plan in the language shown, else as it was entered. */
const shown = (named: NamedText | null | undefined, language: string): string =>
  named == null ? '' : localizedName({ name: named.text, source_language: named.source_language, name_translations: named.translations }, language);

/** The entries of a plan, in the order of the host (the map, then the levels below it, the reading order, the codes). */
export function planEntries(plan: ActionPlanView, language: string, t: Translate): readonly PlanEntry[] {
  return plan.lines.map((line) => ({
    line,
    cells: [
      shown(line.map, language), line.step_code, shown(line.step, language), shown(line.lane, language), line.code, line.description,
      line.action ?? '', line.owner ?? '', line.due_date ?? '', t(`sheet.pain_point.statuses.${line.status}`), line.lost_hours_per_month ?? '',
    ],
    open: line.open,
    noAction: (line.action ?? '').trim() === '',
  }));
}

/**
 * Late: open, a due date given, and that day strictly before `today` (`yyyy-mm-dd`, the local day of the person: see
 * `localDay`). A due date of today is not late; a pain point dealt with never is. Two strings compared, no date read.
 */
export function isLate(entry: PlanEntry, today: string): boolean {
  const due = entry.line.due_date;
  return entry.open && due !== null && due < today;
}

/** A sorted copy. The empty values come last; the order of the host holds among equals (the sort is stable). */
export function sortEntries(entries: readonly PlanEntry[], sort: PlanSort): readonly PlanEntry[] {
  if (sort === 'project') return entries;
  const copy = [...entries];
  if (sort === 'hours') {
    return copy.sort((a, b) => {
      const left = a.line.lost_hours_per_month;
      const right = b.line.lost_hours_per_month;
      if (left === null || right === null) return left === right ? 0 : left === null ? 1 : -1;
      return right - left;
    });
  }
  return copy.sort((a, b) => {
    const left = a.line.due_date;
    const right = b.line.due_date;
    if (left === null || right === null) return left === right ? 0 : left === null ? 1 : -1;
    return left < right ? -1 : left > right ? 1 : 0;
  });
}

/** The entries that pass every filter turned on, in their order. */
export function filterEntries(entries: readonly PlanEntry[], filters: PlanFilters, today: string): readonly PlanEntry[] {
  return entries.filter((entry) => (!filters.open || entry.open) && (!filters.late || isLate(entry, today)) && (!filters.noAction || entry.noAction));
}

/** How many entries are open, late and without action, of how many. */
export function planCounts(entries: readonly PlanEntry[], today: string): PlanCounts {
  return {
    total: entries.length,
    open: entries.filter((entry) => entry.open).length,
    late: entries.filter((entry) => isLate(entry, today)).length,
    noAction: entries.filter((entry) => entry.noAction).length,
  };
}
