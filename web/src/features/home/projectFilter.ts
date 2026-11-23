// projectFilter.ts
// The filter of the project list of the home page: a project is kept when its code, its name in the language shown or its name of origin holds each word typed, in any order, accents and capitals apart (retouch R9b: the rule of the search).

import { localizedName } from '../../shared/i18n/localizedName.ts';
import { foldedTerms, holdsTerms } from '../../shared/text/fold.ts';
import type { ProjectItem } from './focus.ts';

/** The projects that hold every word in one of their texts, in their order; the whole list for a text blank once folded. */
export function filterProjects(projects: readonly ProjectItem[], word: string, language: string): readonly ProjectItem[] {
  const terms = foldedTerms(word);
  if (terms === null) return projects;
  return projects.filter((project) => [project.code, localizedName(project, language), project.name].some((text) => holdsTerms(text, terms)));
}
