// searchStore.ts
// The search in the project (ADR 110 point 5), a store of the page and not of the editor, so that it outlives the change of map (each map mounts a new editor): the project, the panel open, the text, the reply, the result current, the state of the sending, the focus to give to the field; never kept in the browser.

import { createStore, type StoreApi } from 'zustand/vanilla';
import type { Schema } from '../../../api/client.ts';

/** Nothing under way; a text waiting for the pause after the last key; a search sent; a refusal of the host or a failure. */
export type SearchStatus = 'idle' | 'waiting' | 'searching' | 'error';

export interface SearchState {
  /** The project searched, null before the first opening; another project opened empties the search. */
  readonly projectId: string | null;
  readonly open: boolean;
  readonly text: string;
  /** The reply for the text, null while none is there. */
  readonly reply: Schema<'ProjectSearchView'> | null;
  readonly status: SearchStatus;
  readonly error: unknown;
  /** The rank of the result current in the list of all the results, -1 for none. */
  readonly index: number;
  /** The field takes the focus with its text selected (Ctrl+F, the button): asked once, taken by the panel shown. */
  readonly focusField: boolean;
  /** The field had the focus as a result led to another map: the panel of that map gives it back once shown. */
  readonly refocus: boolean;
}

/** The search of a page before its first opening, and of a project left. */
export const EMPTY_SEARCH: SearchState = {
  projectId: null, open: false, text: '', reply: null, status: 'idle', error: null, index: -1, focusField: false, refocus: false,
};

export const searchStore: StoreApi<SearchState> = createStore<SearchState>()(() => ({ ...EMPTY_SEARCH }));
