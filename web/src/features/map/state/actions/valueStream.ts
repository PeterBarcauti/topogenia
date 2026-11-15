// valueStream.ts
// The writes of the entries of the value stream on the map (spec 14.2, 14.6, ADR 085): the incoming volume of a start, the share of the cases of an exit of a gateway, the headcount of a lane; each one operation of the log (one Ctrl+Z), nothing sent when nothing changes.

import { api, type Schema } from '../../../../api/client.ts';
import { projectProperties } from '../../../project/properties/projectProperties.ts';
import type { MapEditor } from '../editor.ts';

type VolumePeriod = Schema<'VolumePeriod'>;

/** An incoming volume: a quantity and its period, or null to clear it (S1). */
export interface VolumeEntry {
  readonly quantity: number;
  readonly period: VolumePeriod;
}

/** The volume of a start step; refused by the host on any other type (VALEUR_INVALIDE, `volume`). */
export async function setVolume(editor: MapEditor, stepId: string, volume: VolumeEntry | null): Promise<void> {
  await editor.write((map) => {
    const step = map.steps.find((candidate) => candidate.id === stepId);
    if (step === undefined || (step.volume?.quantity ?? null) === (volume?.quantity ?? null) && (step.volume?.period ?? null) === (volume?.period ?? null)) return null;
    return api.patch('/api/steps/{id}', { path: { id: stepId }, body: { volume }, version: step.version });
  });
}

/** The share of an exit of a decision or an inclusive gateway, in percent, or null to clear it (S2). */
export async function setShare(editor: MapEditor, flowId: string, share: number | null): Promise<void> {
  await editor.write((map) => {
    const flow = map.flows.find((candidate) => candidate.id === flowId);
    if (flow === undefined || (flow.share_percent ?? null) === share) return null;
    return api.put('/api/flows/{id}/share', { path: { id: flowId }, body: { share }, version: flow.version });
  });
}

/** The headcount of a lane of people, or null to clear it; refused by the host on a lane of software (S4). */
export async function setHeadcount(editor: MapEditor, laneId: string, headcount: number | null): Promise<void> {
  await editor.write((map) => {
    const lane = map.lanes.find((candidate) => candidate.id === laneId);
    if (lane === undefined || (lane.headcount ?? null) === headcount) return null;
    return api.patch('/api/lanes/{id}', { path: { id: laneId }, body: { headcount }, version: lane.version });
  });
}

/**
 * The figures follow the work time of the project (spec 14.2, S6): changed in its properties, the map is read again and
 * the core computes them anew. Gives what stops the watch.
 */
export function watchWorkTime(editor: MapEditor): () => void {
  return projectProperties.subscribe(({ detail }) => {
    const map = editor.store.getState().map;
    if (map === null || detail === null || detail.id.toLowerCase() !== map.project_id.toLowerCase()) return;
    const time = map.value_stream.work_time;
    if (detail.workday_hours !== time.workday_hours || detail.fte_hours_per_month !== time.fte_hours_per_month) void editor.reload();
  });
}
