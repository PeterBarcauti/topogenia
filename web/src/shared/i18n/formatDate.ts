// formatDate.ts
// A day written for the person, in the language of the interface (the trash, the history of the statuses).

/** The day of an instant, `dd/mm/yyyy` in French, in the local time of the browser. */
export function formatDate(iso: string, language: string): string {
  return new Date(iso).toLocaleDateString(language, { day: '2-digit', month: '2-digit', year: 'numeric' });
}
