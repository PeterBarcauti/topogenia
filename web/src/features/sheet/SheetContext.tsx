// SheetContext.tsx
// What the parts of the sheet panel share: the step and the project given by the map, the sheet read, the draft typed, the queue of its saving, the writes of the documents.

import { createContext, useContext } from 'react';
import type { Schema } from '../../api/client.ts';
import type { Notifier } from '../../shared/ui/toasts.ts';
import type { SheetDraft } from './model/draft.ts';
import type { AutoSave } from './state/autoSave.ts';

type SheetView = Schema<'SheetView'>;

/** The step whose sheet is open, as the map shows it: its code, its label and the name of its lane in the language shown. */
export interface SheetStep {
  readonly id: string;
  readonly code: string;
  readonly label: string;
  readonly laneName: string;
  readonly assigneeUserId: string | null;
}

/** The project read by the map: its members, to name the people, and the label of its sharing group (B-4-69). */
export interface SheetProject {
  readonly members: readonly Schema<'MemberView'>[];
  readonly sharingGroupLabel: string | null;
}

/** The sections that fold, in the order of the sheet (AR-91, essai J10): the key facts, the inputs, the procedure, the outputs, the tools. */
export type SheetSection = 'facts' | 'inputs' | 'procedure' | 'outputs' | 'tools';

/** The sections in the order they are drawn. */
export const SHEET_SECTIONS: readonly SheetSection[] = ['facts', 'inputs', 'procedure', 'outputs', 'tools'];

export interface SheetPanelProps {
  readonly step: SheetStep;
  /** Null while it is read, or when it could not be. */
  readonly project: SheetProject | null;
  /** The code of a step of the map, null for a step the map does not show. */
  readonly codeOf: (stepId: string) => string | null;
  readonly folded: Readonly<Record<SheetSection, boolean>>;
  readonly onFold: (section: SheetSection, folded: boolean) => void;
  /** Folds every section, or unfolds them all: the choice is kept for the sheets opened next, while the map stays open (essai J10). */
  readonly onFoldAll: (folded: boolean) => void;
  readonly onClose: () => void;
  /** The gauge of the step on the map, from each sheet read, without reading the map (B-4-50). */
  readonly onSummary: (summary: Schema<'SheetSummary'>) => void;
  /** The documents changed the flows, the pictograms and the gauges of other steps: the map is read again. */
  readonly onMapChanged: () => void;
  /** Gives the sheet to a person, or to nobody in particular; true once the map took it (B-4-15). */
  readonly onAssign: (userId: string | null) => Promise<boolean>;
  readonly notify: Notifier;
  /** The application runs on the computer of the user (desktop mode): the line of a document offers to show its folder (ADR 104); elsewhere the host has no such route. */
  readonly desktop?: boolean | undefined;
  /**
   * Opens the sheet of a step that receives a document of this one, its inputs unfolded for that opening, the caret in
   * the pain point of that document (F-118: a pain point is noted where it is suffered); none where the sheet stands alone.
   */
  readonly onReport?: ((stepId: string, documentId: string) => void) | undefined;
  /**
   * The document whose pain point takes the caret as the sheet is drawn, asked from the outputs of its producer; none
   * otherwise. Asked, the sheet opens on its form, where the field is, even when the free text is its only content (R4-01).
   */
  readonly painFocus?: string | null | undefined;
  /** The sheet is drawn, the caret given or no field there to take it: the request is forgotten (R4-10). */
  readonly onPainFocused?: (() => void) | undefined;
}

export interface SheetValue {
  readonly props: SheetPanelProps;
  readonly view: SheetView;
  readonly draft: SheetDraft;
  /** The typing changed: shown at once, saved by the queue. */
  readonly edit: (draft: SheetDraft) => void;
  /** A sheet received: shown, its version kept, its gauge given to the map; the typing is left alone. */
  readonly apply: (view: SheetView) => void;
  readonly save: AutoSave;
  /**
   * A write of the documents, one at a time: it leaves once the one before is answered, its sheet applied, then the map
   * read again; false when refused.
   */
  readonly runDocument: (call: () => Promise<SheetView>) => Promise<boolean>;
  /** A write of the documents asked and not answered yet: the ticks and the buttons of the documents wait. */
  readonly documentBusy: boolean;
  /**
   * The sheet last received, drawn or not yet: a write that waited for another one reads its version there when it
   * leaves, the version that one gave back (audit 3.1 V3, U1).
   */
  readonly received: () => SheetView;
  /** The name of a person, « un ancien membre » for one who left the project. */
  readonly nameOf: (userId: string) => string;
}

export const SheetContext = createContext<SheetValue | null>(null);

/**
 * Whether the map opens the sheet of a step, to note a pain point there (F-118): not for an event or a gateway that
 * joins, which holds no sheet whatever it used before its type changed (R4-11), nor for a sheet the person may not fill
 * (audit 3.2). Given by the map from above the panel; every step where the sheet stands alone.
 */
export const ReportTargets = createContext<(stepId: string) => boolean>(() => true);

export function useSheet(): SheetValue {
  const value = useContext(SheetContext);
  if (value === null) throw new Error('useSheet outside SheetContext.');
  return value;
}
