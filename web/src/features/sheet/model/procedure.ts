// procedure.ts
// The lines of the procedure as typed: at least one shown, a line inserted, removed or moved (annex B 4, AR-89).

import type { ProcedureLine } from './draft.ts';

export const EMPTY_LINE: ProcedureLine = { text: '', tool: '', nature: null };

/** The lines shown: always one at least, to start typing (B-4-29). */
export function shownLines(lines: readonly ProcedureLine[]): readonly ProcedureLine[] {
  return lines.length === 0 ? [EMPTY_LINE] : lines;
}

export function insertLine(lines: readonly ProcedureLine[], at: number): ProcedureLine[] {
  return [...lines.slice(0, at), EMPTY_LINE, ...lines.slice(at)];
}

export function removeLine(lines: readonly ProcedureLine[], at: number): ProcedureLine[] {
  return lines.filter((_, index) => index !== at);
}

/** The line at `from` taken to the place `to` of the list (a button moves it by one, the handle by several, AR-89). */
export function moveLine(lines: readonly ProcedureLine[], from: number, to: number): readonly ProcedureLine[] {
  if (from === to || from < 0 || to < 0 || from >= lines.length || to >= lines.length) return lines;
  const next = [...lines];
  const [moved] = next.splice(from, 1);
  if (moved !== undefined) next.splice(to, 0, moved);
  return next;
}

export function isBlank(line: ProcedureLine): boolean {
  return line.text === '' && line.tool === '' && line.nature === null;
}
