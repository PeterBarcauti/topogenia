// formatDate.ts
// A day written for the person, in the language of the interface (the trash, the history of the statuses, the due date of an action).

/** The day of an instant, `dd/mm/yyyy` in French, in the local time of the browser. */
export function formatDate(iso: string, language: string): string {
  return new Date(iso).toLocaleDateString(language, { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/** The day of an instant in its long form (`21 novembre 2026`), in the language of the interface and the local time of the browser. */
export function formatLongDate(instant: Date, language: string): string {
  return new Intl.DateTimeFormat(language, { dateStyle: 'long' }).format(instant);
}

/** A day as a date field holds it (`yyyy-mm-dd`, no hour), written as {@link formatDate} writes it: the same day wherever the browser is. */
export function formatDay(day: string, language: string): string {
  return formatDate(`${day}T00:00:00`, language);
}

/**
 * Today as the person reading sees it: the calendar day of the browser, `yyyy-mm-dd`, in local time (ADR 112 point 4).
 * Written from the local parts of the date, never by `toISOString`, which gives the day in universal time (the day
 * before at 0:30 in Paris). A due date is compared to it as a string, never read by `new Date('2026-12-15')`.
 */
export function localDay(now: Date = new Date()): string {
  const two = (value: number): string => String(value).padStart(2, '0');
  return `${now.getFullYear()}-${two(now.getMonth() + 1)}-${two(now.getDate())}`;
}
