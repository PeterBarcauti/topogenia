// formatDate.ts
// A day written for the person, in the language of the interface (the trash, the history of the statuses, the due date of an action).

/** The day of an instant, `dd/mm/yyyy` in French, in the local time of the browser. */
export function formatDate(iso: string, language: string): string {
  return new Date(iso).toLocaleDateString(language, { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/** A day as a date field holds it (`yyyy-mm-dd`, no hour), written as {@link formatDate} writes it: the same day wherever the browser is. */
export function formatDay(day: string, language: string): string {
  return formatDate(`${day}T00:00:00`, language);
}
