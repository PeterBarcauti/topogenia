// report.ts
// The report of an import: the rows left aside grouped by reason with their lines, the keys the workbook did not write counted with the items not found (B-5-23).

import type { Schema } from '../../../api/client.ts';

/** The reasons of a row left aside, in the order of the report (A-2.14-18 to A-2.14-21). */
export const REASONS = ['OBJET_INTROUVABLE', 'TEXTE_MODIFIE', 'TRADUCTION_MANUELLE', 'SANS_TRADUCTION'] as const;

export type Reason = (typeof REASONS)[number];

export interface ReasonLines {
  readonly reason: Reason;
  /** The lines of the workbook, in increasing order. */
  readonly lines: readonly number[];
}

/**
 * The rows the host left aside and the lines whose key could not be sent, by reason in the order of {@link REASONS}; a
 * reason without a line is left out, a reason the interface does not know is counted with the items not found.
 */
export function linesByReason(ignored: readonly Schema<'IgnoredRow'>[], unknownKeys: readonly number[]): ReasonLines[] {
  const known = (reason: string): Reason => REASONS.find((candidate) => candidate === reason) ?? 'OBJET_INTROUVABLE';
  const all = [...ignored.map((row) => ({ reason: known(row.reason), line: row.line })), ...unknownKeys.map((line) => ({ reason: 'OBJET_INTROUVABLE' as const, line }))];
  return REASONS
    .map((reason) => ({ reason, lines: all.filter((row) => row.reason === reason).map((row) => row.line).sort((a, b) => a - b) }))
    .filter((group) => group.lines.length > 0);
}
