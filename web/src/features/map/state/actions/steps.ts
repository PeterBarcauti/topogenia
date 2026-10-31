// steps.ts
// The writes of the steps: create in a cell, insert next to a step, rename or change the type, detail, delete a detailed step or a detail (annex B 2.3, 2.4, AR-152).

import { api, type Schema } from '../../../../api/client.ts';
import { paths } from '../../../../app/router.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { GridCell } from '../../model/geometry/grid.ts';
import { hasContent } from '../../model/stepFacts.ts';
import type { MapEditor } from '../editor.ts';

type StepType = Schema<'StepType'>;

/** Where a step is inserted next to another (`POST /api/steps/{id}/insert`). */
export type InsertPosition = 'before' | 'after' | 'below' | 'above' | 'parallel';

/** The longest label of a step (1.x `SaisieLibelle`, 150). */
export const STEP_LABEL_MAX = 150;

/** The label of a new step: « Nouvelle étape », or the name of its type for an event or a gateway that joins. */
const newLabel = (type: StepType): string => (hasContent(type) ? i18next.t('map.new_step') : i18next.t(`map.step_types.${type}`));

/**
 * A step created: its name typed at once, and the type of the next step back to « Étape », the most common one (AR-152);
 * a creation refused keeps the type chosen, to try again.
 */
function created(editor: MapEditor, id: string | null | undefined): void {
  if (id != null) editor.store.setState({ editing: id, newStepType: 'task' });
}

/** A new step of the type of the next step in a cell, its name typed at once (B-2.3-09, B-2.1-15, AR-152). */
export async function createStep(editor: MapEditor, cell: GridCell): Promise<void> {
  const type = editor.store.getState().newStepType;
  const body = { lane_id: cell.laneId, column: cell.column, row: cell.row, label: newLabel(type), type };
  const reply = await editor.write((map) => api.post('/api/maps/{id}/steps', { path: { id: map.id }, body }));
  created(editor, reply?.id);
}

/** A new step of the type of the next step before, after, under, above or alongside a step, in the flow (B-2.1-18, B-2.3-44, AR-152). */
export async function insertStep(editor: MapEditor, stepId: string, position: InsertPosition): Promise<void> {
  const type = editor.store.getState().newStepType;
  const reply = await editor.write(() => api.post('/api/steps/{id}/insert', { path: { id: stepId }, body: { position, label: newLabel(type), type } }));
  created(editor, reply?.id);
}

/** Renames a step or changes its type; nothing is sent when nothing changes. */
export async function updateStep(editor: MapEditor, stepId: string, changes: { readonly label?: string; readonly type?: StepType }): Promise<void> {
  await editor.write((map) => {
    const step = map.steps.find((candidate) => candidate.id === stepId);
    if (step === undefined) return null;
    const body = {
      ...(changes.label !== undefined && changes.label !== step.label ? { label: changes.label } : {}),
      ...(changes.type !== undefined && changes.type !== step.type ? { type: changes.type } : {}),
    };
    return Object.keys(body).length === 0 ? null : api.patch('/api/steps/{id}', { path: { id: stepId }, body, version: step.version });
  });
}

/** The warning of a map deeper than advised, after a detail (B-2.1-36). */
function warnDepth(editor: MapEditor, reply: Schema<'StructureReply'>, map: Schema<'MapView'>): void {
  if (reply.warnings.includes('PROFONDEUR_DEPASSEE')) editor.notify(i18next.t('map.depth', { level: map.depth + 1, max: map.recommended_depth }));
}

/** A step given a detailed map, opened at once (B-2.1-28). */
export async function detailStep(editor: MapEditor, stepId: string): Promise<void> {
  const map = editor.store.getState().map;
  const reply = await editor.write((current) => {
    const step = current.steps.find((candidate) => candidate.id === stepId);
    return step === undefined ? null : api.post('/api/steps/{id}/detail', { path: { id: stepId }, version: step.version });
  });
  if (reply === null || map === null) return;
  warnDepth(editor, reply, map);
  if (reply.id !== null) editor.navigate(paths.map(reply.id));
}

/** Several steps gathered in one detailed step « Sous-processus », which is chosen and named at once (B-2.1-29, B-2.3-30, B-2.9-09, B-2.9-10). */
export async function groupSteps(editor: MapEditor, stepIds: readonly string[]): Promise<void> {
  const map = editor.store.getState().map;
  const reply = await editor.write((current) => api.post('/api/maps/{id}/group', { path: { id: current.id }, body: { steps: [...stepIds], label: i18next.t('map.sub_process') } }));
  if (reply === null || map === null) return;
  warnDepth(editor, reply, map);
  if (reply.id !== null) editor.store.setState({ selection: { steps: [reply.id], flows: [] }, editing: reply.id });
}

/** A detailed step deleted with its map, or its map given to another step; the flow is stitched (B-2.3-39). */
export async function deleteMacro(editor: MapEditor, stepId: string, choice: 'delete' | 'reattach', to?: string): Promise<void> {
  await editor.write((map) => {
    const step = map.steps.find((candidate) => candidate.id === stepId);
    if (step === undefined) return null;
    return api.delete('/api/steps/{id}', { path: { id: stepId }, query: { sub_map: choice, to, stitch: true }, version: step.version });
  });
}

/** The detail of a step deleted: its map read for its version, then deleted with its whole descent (B-2.4-17, B-2.4-18). */
export async function deleteDetail(editor: MapEditor, stepId: string): Promise<void> {
  await editor.write((map) => {
    const subMap = map.steps.find((candidate) => candidate.id === stepId)?.sub_map_id ?? null;
    if (subMap === null) return null;
    return api.get('/api/maps/{id}', { path: { id: subMap } }).then((read) => api.delete('/api/maps/{id}', { path: { id: subMap }, version: read.map.version }));
  });
}
