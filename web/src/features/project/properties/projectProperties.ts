// projectProperties.ts
// The properties of the project open on a map: its detail read for the header, the dialog open or not, and what a Save sends (spec 4.1, 10).

import { createStore } from 'zustand/vanilla';
import { api, type Schema } from '../../../api/client.ts';
import type { Classification } from '../classification.ts';

export type ProjectDetail = Schema<'ProjectDetail'>;

export interface PropertiesState {
  /** The detail of the project of the header: its classification shown by the header; null while it is read or when it could not be. */
  readonly detail: ProjectDetail | null;
  /** The dialog of the properties is open over the screen. */
  readonly open: boolean;
}

export const projectProperties = createStore<PropertiesState>()(() => ({ detail: null, open: false }));

/** Opens the properties of the project of the header: from its classification in the header, or from the File menu of the map. */
export function openProjectProperties(): void {
  projectProperties.setState({ open: true });
}

export function closeProjectProperties(): void {
  projectProperties.setState({ open: false });
}

/** The project read or changed by the dialog becomes the one the header shows. */
export function showDetail(detail: ProjectDetail): void {
  projectProperties.setState({ detail });
}

/**
 * Reads the detail of the project the header shows; gives what stops it when the header leaves the project. A failure
 * leaves the detail empty: the header then shows no classification, the map stays usable.
 */
export function followProject(projectId: string): () => void {
  const controller = new AbortController();
  projectProperties.setState({ detail: null });
  api.get('/api/projects/{id}', { path: { id: projectId }, signal: controller.signal }).then((detail) => {
    projectProperties.setState({ detail });
  }, () => undefined);
  return () => {
    controller.abort();
    projectProperties.setState({ detail: null, open: false });
  };
}

/** What the facilitator chose in the dialog: the level, the label, the working day and the hours of a full-time equivalent as typed. */
export interface PropertiesChoice {
  readonly classification: Classification;
  readonly label: string;
  readonly workday: string;
  readonly fte: string;
}

/** A number of hours typed, null for an empty field or no number: the value of the project is kept (spec 14.2, S6). */
export const hoursOf = (typed: string): number | null => (typed.trim() === '' || Number.isNaN(Number(typed)) ? null : Number(typed));

/** The work time typed, each value that is no number or no change left out. */
const workTimeOf = (detail: ProjectDetail, choice: PropertiesChoice) => {
  const [workday, fte] = [hoursOf(choice.workday), hoursOf(choice.fte)];
  return { workday_hours: workday === detail.workday_hours ? null : workday, fte_hours_per_month: fte === detail.fte_hours_per_month ? null : fte };
};

/** The label sent: the text trimmed, or null to remove it (the host trims it too and keeps 100 characters at most). */
export const labelOf = (typed: string): string | null => (typed.trim() === '' ? null : typed.trim());

/** Whether the choice differs from the project read. */
export function changed(detail: ProjectDetail, choice: PropertiesChoice): boolean {
  const time = workTimeOf(detail, choice);
  return choice.classification !== detail.classification || labelOf(choice.label) !== detail.sharing_group_label
    || time.workday_hours !== null || time.fte_hours_per_month !== null;
}

/**
 * Sends what changed, each setting by its own route with the version of the project read (If-Match): the classification,
 * then the label of the sharing group, then the work time, each with the version the reply before gave. `onStep` gets
 * the project after each change, so that a refusal leaves the dialog with the version of the change before it.
 */
export async function saveProperties(detail: ProjectDetail, choice: PropertiesChoice, onStep: (detail: ProjectDetail) => void): Promise<ProjectDetail> {
  let current = detail;
  if (choice.classification !== current.classification) {
    current = await api.put('/api/projects/{id}/classification', { path: { id: current.id }, body: { classification: choice.classification }, version: current.version });
    onStep(current);
  }
  const label = labelOf(choice.label);
  if (label !== current.sharing_group_label) {
    current = await api.put('/api/projects/{id}/sharing-group-label', { path: { id: current.id }, body: { label }, version: current.version });
    onStep(current);
  }
  const time = workTimeOf(current, choice);
  if (time.workday_hours !== null || time.fte_hours_per_month !== null) {
    current = await api.put('/api/projects/{id}/work-time', { path: { id: current.id }, body: time, version: current.version });
    onStep(current);
  }
  return current;
}
