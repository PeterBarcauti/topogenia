// useSearch.ts
// The sending of a search to the host (ADR 110 point 4): 300 ms after the last key from two characters, at once on Enter, again at each opening of the panel; a new text cancels the search before it; the word in the body of a POST, never in the address.

import { api } from '../../../api/client.ts';
import { graphemes } from '../export/arialWidth.ts';
import type { MapEditor } from '../state/editor.ts';
import { EMPTY_SEARCH, searchStore } from './searchStore.ts';

/** What a search asks besides its text: the project, the language the map is shown in, the map open (put first). */
export interface SearchAsk {
  readonly projectId: string;
  readonly mapId: string;
  readonly language: string;
}

/** The pause after the last key, and the characters a text needs to be sent without Enter (one ideogram is a word). */
export const SEARCH_DELAY_MS = 300;
const MIN_CHARACTERS = 2;

/** The sending under way: the pause waited for, the call that a new text aborts, its end. */
const pending: { timer: ReturnType<typeof setTimeout> | null; controller: AbortController | null; running: Promise<void> | null } = {
  timer: null, controller: null, running: null,
};

/** The characters of a text without its spaces at the edges, as a person counts them (a letter and its accent, an ideogram: one each). */
export const charactersOf = (text: string): number => graphemes(text.trim()).length;

/** The ask of the map an editor shows, in the language it is shown in (its language of origin before the header tells it); null while it is read. */
export function askOf(editor: MapEditor): SearchAsk | null {
  const { map, shown } = editor.store.getState();
  return map === null ? null : { projectId: map.project_id, mapId: map.id, language: shown?.language ?? map.languages.origin };
}

/** Stops the pause and aborts the call under way: what they would bring is not taken. */
function stop(): void {
  if (pending.timer !== null) clearTimeout(pending.timer);
  pending.controller?.abort();
  pending.timer = null;
  pending.controller = null;
  pending.running = null;
}

/** Sends the text, the reply before it forgotten; its reply taken only while no other search replaced it. */
function send(ask: SearchAsk, text: string): Promise<void> {
  stop();
  const controller = new AbortController();
  searchStore.setState({ status: 'searching', reply: null, error: null, index: -1 });
  const body = { text: text.trim(), language: ask.language, map_id: ask.mapId };
  const running = api.post('/api/projects/{id}/search', { path: { id: ask.projectId }, body, signal: controller.signal }).then((reply) => {
    if (!controller.signal.aborted) searchStore.setState({ reply, status: 'idle' });
  }, (error: unknown) => {
    if (!controller.signal.aborted) searchStore.setState({ status: 'error', error });
  }).finally(() => {
    if (pending.controller !== controller) return;
    pending.controller = null;
    pending.running = null;
  });
  pending.controller = controller;
  pending.running = running;
  return running;
}

/** A text typed: the search before it cancelled, this one sent after the pause when it is long enough. */
export function typeSearch(text: string, ask: SearchAsk): void {
  stop();
  const enough = charactersOf(text) >= MIN_CHARACTERS;
  searchStore.setState({ text, reply: null, error: null, index: -1, status: enough ? 'waiting' : 'idle' });
  if (!enough) return;
  pending.timer = setTimeout(() => {
    pending.timer = null;
    void send(ask, text);
  }, SEARCH_DELAY_MS);
}

/** The reply for the text, at once (Enter): the one there, the one under way, else a search sent now; nothing for a blank text. */
export function searchNow(ask: SearchAsk): Promise<void> {
  const { text, reply } = searchStore.getState();
  if (reply !== null) return Promise.resolve();
  if (pending.running !== null) return pending.running;
  return charactersOf(text) === 0 ? Promise.resolve() : send(ask, text);
}

/**
 * Opens the panel, the field focused and its text selected: another project empties the search first; an opening reads
 * the text again (the reply does not follow the writes); the panel already open only takes the focus back.
 */
export function openSearch(ask: SearchAsk): void {
  if (searchStore.getState().projectId !== ask.projectId) {
    stop();
    searchStore.setState({ ...EMPTY_SEARCH, projectId: ask.projectId });
  }
  const { open, text, reply } = searchStore.getState();
  searchStore.setState({ open: true, focusField: true });
  if (!open && (reply !== null || charactersOf(text) >= MIN_CHARACTERS)) void send(ask, text);
}

/** Closes the panel: the search under way is cancelled, the text kept for the next opening. */
export function closeSearch(): void {
  const searching = pending.timer !== null || pending.controller !== null;
  stop();
  searchStore.setState(searching ? { open: false, focusField: false, refocus: false, status: 'idle' } : { open: false, focusField: false, refocus: false });
}

/** The search of another project than the one shown is forgotten (ADR 110 point 5: emptied when another project opens). */
export function leaveOtherProject(projectId: string): void {
  const kept = searchStore.getState().projectId;
  if (kept === null || kept === projectId) return;
  stop();
  searchStore.setState({ ...EMPTY_SEARCH });
}
