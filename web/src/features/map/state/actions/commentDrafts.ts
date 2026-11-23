// commentDrafts.ts
// The comments of presentation written by the AI (3.3, lot 3, F-130): asked of the assistant for the steps and the flows of the map shown and of its detailed maps, and kept after review, written at once as the assistant's.

import { api } from '../../../../api/client.ts';
import { messageOf } from '../../../../shared/i18n/errorMessage.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { MapEditor } from '../editor.ts';

const t = i18next.t.bind(i18next);

/** A comment kept after review: its step or its flow, its text, the version of the original it replaces (0 for none), whether a typed one may be replaced. */
export interface CommentDraftWrite {
  readonly stepId: string | null;
  readonly flowId: string | null;
  readonly text: string;
  readonly version: number;
  readonly replace: boolean;
}

/**
 * Writes the comments kept, told when it is done; true when they are written, the host's refusal told otherwise. The map shown names
 * the project: the comments may be on any of its levels, the review saved from another level than the one it was asked on.
 */
export async function writeCommentDrafts(editor: MapEditor, writes: readonly CommentDraftWrite[]): Promise<boolean> {
  const map = editor.store.getState().map;
  if (map === null || writes.length === 0) return false;
  const comments = writes.map((write) => ({ step_id: write.stepId, flow_id: write.flowId, text: write.text, version: write.version, replace: write.replace }));
  try {
    const result = await api.post('/api/maps/{id}/presentation-comments', { path: { id: map.id }, body: { comments } });
    editor.notify(t('assistant.comments.done', { n: result.written }), 'success');
    return true;
  } catch (failure) {
    editor.notify(messageOf(failure), 'error');
    return false;
  }
}

/**
 * The comments asked of the AI (F-130): the panel of the assistant opened, its practice `comments` sent on the map shown;
 * the panel shows the progress, the copy and paste when no model is called, then the comments to review.
 */
export async function askComments(editor: MapEditor): Promise<void> {
  const map = editor.store.getState().map;
  if (map === null) return;
  const panel = editor.assistant.getState();
  panel.setOpen(true);
  if (panel.status === null) await panel.loadStatus();
  await editor.assistant.getState().send(t('assistant.comments.request'), 'comments', []);
}
