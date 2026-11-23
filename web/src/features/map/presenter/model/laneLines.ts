// laneLines.ts
// The number of lines a lane name may take in the overview of a presentation (F-209): as many as its band holds, three at most, none under one line.

/** The most lines a name takes, however high its band. */
const MAX_LINES = 3;

/** The size of the text, in px, of a lane name and of the one lit (the line height is 1.2 of it, see `presenter.css`). */
const NAME_SIZE = 15;
const ACTIVE_NAME_SIZE = 17;

/** The lines a name of the given band height may take: the whole lines that fit, three at most; 0 when not even one fits (the name is then masked). */
export function laneLines(height: number, active: boolean): number {
  const lineHeight = ((active ? ACTIVE_NAME_SIZE : NAME_SIZE) * 6) / 5;
  return Math.max(0, Math.min(MAX_LINES, Math.floor(height / lineHeight)));
}
