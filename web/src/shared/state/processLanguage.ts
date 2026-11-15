// processLanguage.ts
// The language a process is shown in (ADR 083): one per project, chosen in the header and kept while the page lives; at the opening, the language of the interface when the project is translated into it for the most part, else its language of origin.

import { createStore } from 'zustand/vanilla';

/** The languages of a project, as the map gives them: its language of origin, its number of texts, how many are written in each language. */
export interface ProjectLanguages {
  readonly origin: string;
  readonly total: number;
  readonly written: Readonly<Record<string, number>>;
}

/** The language chosen for each project open in the page, by its identifier; never kept in the browser. */
export const processLanguage = createStore<{ readonly chosen: Readonly<Record<string, string>> }>()(() => ({ chosen: {} }));

/** Shows a project in a language, every map of it, until another is chosen. */
export function chooseProcessLanguage(projectId: string, language: string): void {
  processLanguage.setState((state) => ({ chosen: { ...state.chosen, [projectId]: language } }));
}

/**
 * The language a project is shown in: the one chosen for it, else the language of the interface when the project is
 * written in it (its language of origin, or translated into it for half of its texts at least: a name or two translated
 * do not lock the map of its author), else its language of origin.
 */
export function shownLanguage(chosen: string | undefined, languages: ProjectLanguages, interfaceLanguage: string): string {
  if (chosen !== undefined) return chosen;
  const translated = languages.total > 0 && 2 * (languages.written[interfaceLanguage] ?? 0) >= languages.total;
  return interfaceLanguage === languages.origin || translated ? interfaceLanguage : languages.origin;
}

/** The share of the texts of the project written in a language, as a whole percentage (100 for its language of origin). */
export function coverageOf(languages: ProjectLanguages, language: string): number {
  if (language === languages.origin || languages.total === 0) return 100;
  return Math.floor((100 * (languages.written[language] ?? 0)) / languages.total);
}
