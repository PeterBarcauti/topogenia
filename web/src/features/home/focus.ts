// focus.ts
// The project the home page puts forward: the last one opened, remembered in the profile, else the first by code (AR-49).

import type { Schema } from '../../api/client.ts';

export type ProjectItem = Schema<'ProjectListItem'>;

/**
 * The project of the main button and of « Who takes part » (B-1.3-02, B-1.3-24): the last project opened when the user
 * still sees it, else the first of the list by code (the 1.x took the first of the list); none without a project.
 */
export function focusProject(projects: readonly ProjectItem[], lastProjectId: string | null): ProjectItem | undefined {
  const last = lastProjectId === null ? undefined : projects.find((project) => project.id.toLowerCase() === lastProjectId.toLowerCase());
  return last ?? [...projects].sort((a, b) => (a.code < b.code ? -1 : a.code > b.code ? 1 : 0))[0];
}
