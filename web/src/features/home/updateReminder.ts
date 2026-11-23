// updateReminder.ts
// The rule of the reminder of the home page (ADR 111): a version 90 days old is worth a look at the page of the versions; shown to the administrator alone, never for the Store version nor without links, closed it comes back after a new delay; the days counted in the local calendar of the computer.

/** The age of a version, and the time a reminder closed stays closed, in days (a quarter). */
export const UPDATE_REMINDER_DAYS = 90;

const DAY_MS = 86_400_000;
const DAY_FORMAT = /^(\d{4})-(\d{2})-(\d{2})$/;

/** The number of a day of the calendar, without hour or zone; null when the text is no real day (`yyyy-mm-dd`). */
function dayNumber(day: string): number | null {
  const parts = DAY_FORMAT.exec(day);
  if (parts === null) return null;
  const [year, month, date] = [Number(parts[1]), Number(parts[2]), Number(parts[3])];
  const utc = new Date(Date.UTC(year, month - 1, date));
  return utc.getUTCFullYear() === year && utc.getUTCMonth() === month - 1 && utc.getUTCDate() === date ? utc.getTime() / DAY_MS : null;
}

/** The day of an instant in the local calendar of the computer, as `yyyy-mm-dd`. */
export function dayKey(now: Date): string {
  const two = (value: number) => String(value).padStart(2, '0');
  return `${String(now.getFullYear()).padStart(4, '0')}-${two(now.getMonth() + 1)}-${two(now.getDate())}`;
}

export interface ReminderInput {
  /** The day of the release (`yyyy-mm-dd`), written in `package.json`. */
  readonly releaseDay: string;
  readonly now: Date;
  /** The day the reminder was closed, as the browser kept it; anything unreadable counts as none. */
  readonly closedDay: string | null;
  readonly isAdmin: boolean;
  /** The Store version, which the Store updates by itself. */
  readonly store: boolean;
  /** The host gives the links to the site (SiteLinks not false). */
  readonly hasLinks: boolean;
}

/** Whether the reminder is shown: the user and the version allow it, the version is old enough, and no closing is recent. */
export function reminderDue({ releaseDay, now, closedDay, isAdmin, store, hasLinks }: ReminderInput): boolean {
  if (!isAdmin || store || !hasLinks) return false;
  const release = dayNumber(releaseDay);
  const today = dayNumber(dayKey(now));
  if (release === null || today === null || today - release < UPDATE_REMINDER_DAYS) return false;
  const closed = closedDay === null ? null : dayNumber(closedDay);
  return closed === null || closed > today || today - closed >= UPDATE_REMINDER_DAYS;
}
