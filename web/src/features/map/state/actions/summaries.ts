// summaries.ts
// The summaries of the maps (plan 3.1, lot R): written by hand or erased, kept from a reply of the AI after review, asked of the AI for the maps without one or for the map shown.

import { api } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { MapEditor } from '../editor.ts';

const t = i18next.t.bind(i18next);

/** The longest summary, one sentence (the host refuses beyond, TEXTE_TROP_LONG). */
export const SUMMARY_MAX = 200;

/** A summary to write: its map, its sentence (empty erases it), who wrote it, the model of the AI, the version of it read (0 for none). */
export interface SummaryWrite {
  readonly mapId: string;
  readonly text: string;
  readonly origin: 'ai' | 'manual';
  readonly model?: string | null;
  readonly version: number;
}

/** Writes summaries through the map shown, whose view the reply carries; true when they are written. */
export async function writeSummaries(editor: MapEditor, writes: readonly SummaryWrite[]): Promise<boolean> {
  if (writes.length === 0) return false;
  const summaries = writes.map((write) => ({ map_id: write.mapId, text: write.text, origin: write.origin, model: write.model ?? null, version: write.version }));
  const reply = await editor.write((map) => api.put('/api/maps/{id}/summaries', { path: { id: map.id }, body: { summaries } }));
  return reply !== null;
}

/** The summary of the map shown written by hand, or erased with an empty text. */
export async function writeSummary(editor: MapEditor, text: string): Promise<boolean> {
  const map = editor.store.getState().map;
  if (map === null) return false;
  return writeSummaries(editor, [{ mapId: map.id, text: text.trim(), origin: 'manual', version: map.summary?.version ?? 0 }]);
}

/**
 * The summaries asked of the AI (plan 3.1, lot R): the panel of the assistant opened, its practice `summaries` sent on the
 * root map, for the maps named, or for every map without a summary or whose summary is stale; the panel shows the
 * progress, the copy and paste when no model is called, then the sentences to review.
 */
export async function askSummaries(editor: MapEditor, maps: readonly string[] = []): Promise<void> {
  const map = editor.store.getState().map;
  if (map === null) return;
  const panel = editor.assistant.getState();
  panel.setOpen(true);
  if (panel.status === null) await panel.loadStatus();
  const root = map.breadcrumb[0]?.map_id ?? map.id;
  const request = maps.length === 0 ? t('assistant.summaries.request') : t('assistant.summaries.request_one');
  await editor.assistant.getState().send(request, 'summaries', [], { mapId: root, maps });
}
