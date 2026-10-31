// threadContext.ts
// What the thread of the panel of the assistant may do around it: its store, the state of the panel, the home page or a map, the way to show objects and to create a project from a proposal (spec 3.0 sections 3.2, 3.4).

import type { StoreApi } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import type { AssistantReply, AssistantState, AssistantStatus, Translation } from './assistantStore.ts';

export type AssistantObject = AssistantReply['objects'][number];

export interface ThreadContext {
  readonly store: StoreApi<AssistantState>;
  readonly status: AssistantStatus | null;
  /** The panel of the home page: a project yet to create, no map to show objects on. */
  readonly home: boolean;
  /** Selects the objects named and shows them on their map. */
  readonly reveal?: (objects: readonly AssistantObject[]) => void;
  /** Opens the creation of a project from the `.pmap` of a proposal (an administrator). */
  readonly createProject?: (pmap: string) => void;
  /** A proposal on the map open, for the facilitator (spec 3.0 section 3.1-5 to 7): its preview, its application, its marks taken off. */
  readonly proposals?: {
    /** The preview of a proposal on the map open, or of a translation on its root map (ADR 083: only the translations into its language). */
    readonly preview: (pmap: string, translation?: Translation) => Promise<Schema<'ProposalPreviewView'>>;
    readonly apply: (pmap: string, version: number, translation?: Translation) => Promise<boolean>;
    readonly unmark: () => void;
  };
}

/** Copies a text to the clipboard; false when the browser refuses. */
export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}
