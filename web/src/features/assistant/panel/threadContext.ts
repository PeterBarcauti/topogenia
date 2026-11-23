// threadContext.ts
// What the thread of the panel of the assistant may do around it: its store, the state of the panel, the home page or a map, the way to show objects and to create a project from a proposal (spec 3.0 sections 3.2, 3.4).

import type { StoreApi } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import type { AssistantReply, AssistantState, AssistantStatus, Translation } from './assistantStore.ts';

export type AssistantObject = AssistantReply['objects'][number];

/** A summary kept after review (plan 3.1, lot R): its map, its sentence, the version of the summary it replaces (0 for none). */
export interface SummaryKept {
  readonly mapId: string;
  readonly text: string;
  readonly version: number;
}

/**
 * A comment of presentation written by the model and kept after review (F-130): its step or its flow, its text as
 * corrected, the version of the original it replaces (0 for none), and whether the person accepted to replace one typed by hand.
 */
export interface CommentKept {
  readonly stepId: string | null;
  readonly flowId: string | null;
  readonly text: string;
  readonly version: number;
  readonly replace: boolean;
}

export interface ThreadContext {
  readonly store: StoreApi<AssistantState>;
  readonly status: AssistantStatus | null;
  /** The panel of the home page: a project yet to create, no map to show objects on. */
  readonly home: boolean;
  /** Selects the objects named and shows them on their map. */
  readonly reveal?: (objects: readonly AssistantObject[]) => void;
  /** Saves the report of the map open as a Markdown file (plan 3.1, L4), with the model that wrote it. */
  readonly saveReport?: (report: Schema<'AssistantReportView'>, model: string) => Promise<void>;
  /** Saves the summaries kept after review (plan 3.1, lot R), with the model that wrote them; true when they are saved. */
  readonly saveSummaries?: (summaries: readonly SummaryKept[], model: string | null) => Promise<boolean>;
  /** Saves the comments of presentation kept after review (F-130); true when they are saved. */
  readonly saveComments?: (comments: readonly CommentKept[]) => Promise<boolean>;
  /** Opens the creation of a project from the `.pmap` of a proposal (an administrator). */
  readonly createProject?: (pmap: string) => void;
  /** A proposal on the map open, for the facilitator (spec 3.0 section 3.1-5 to 7): its preview, its application, its marks taken off. */
  readonly proposals?: {
    /** The preview of a proposal on the map open, or of a translation on its root map (ADR 083: only the translations into its language). */
    readonly preview: (pmap: string, translation?: Translation, comments?: TranslatedComments) => Promise<Schema<'ProposalPreviewView'>>;
    /** Applies the proposal; a translation writes with it the translations of the comments of presentation its reply brought (ADR 099 revised). */
    readonly apply: (pmap: string, version: number, translation?: Translation, comments?: TranslatedComments) => Promise<boolean>;
    readonly unmark: () => void;
  };
}

/**
 * The translations of the comments of presentation a reply brought beside its `.pmap` (ADR 099 revised): those of the
 * steps, and those of the flows (lot J10), each flow named by the codes of its two steps.
 */
export interface TranslatedComments {
  readonly steps: readonly Schema<'AssistantCommentView'>[];
  readonly flows: readonly Schema<'AssistantFlowCommentView'>[];
}

/** The comments a reply brought, as its preview and its application take them. */
export function translatedComments(reply: Pick<AssistantReply, 'comments' | 'flow_comments'>): TranslatedComments {
  return { steps: reply.comments ?? [], flows: reply.flow_comments ?? [] };
}

/** The kind of a comment of presentation of a step among the texts of the preview of a translation: it is not a text shown on the map. */
export const COMMENT_KIND = 'presentation_comment';

/** The kind of the comment of presentation of a flow: its identifier is the one of its flow, whose label is a text of the map of another kind. */
export const FLOW_COMMENT_KIND = 'flow_presentation_comment';

/** True for a text of the preview of a translation that is a comment of presentation, said in a presentation and never shown on the map. */
export function isCommentKind(kind: string): boolean {
  return kind === COMMENT_KIND || kind === FLOW_COMMENT_KIND;
}

