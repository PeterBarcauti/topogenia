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

/** The sections that fold (AR-91). */
export type SheetSection = 'inputs' | 'procedure' | 'outputs';

export interface SheetPanelProps {
  readonly step: SheetStep;
  /** Null while it is read, or when it could not be. */
  readonly project: SheetProject | null;
  /** The code of a step of the map, null for a step the map does not show. */
  readonly codeOf: (stepId: string) => string | null;
  readonly folded: Readonly<Record<SheetSection, boolean>>;
  readonly onFold: (section: SheetSection, folded: boolean) => void;
  readonly onClose: () => void;
  /** The gauge of the step on the map, from each sheet read, without reading the map (B-4-50). */
  readonly onSummary: (summary: Schema<'SheetSummary'>) => void;
  /** The documents changed the flows, the pictograms and the gauges of other steps: the map is read again. */
  readonly onMapChanged: () => void;
  /** Gives the sheet to a person, or to nobody in particular; true once the map took it (B-4-15). */
  readonly onAssign: (userId: string | null) => Promise<boolean>;
  readonly notify: Notifier;
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
  /** A write of the documents, one at a time: its sheet applied, then the map read again; false when refused. */
  readonly runDocument: (call: () => Promise<SheetView>) => Promise<boolean>;
  readonly documentBusy: boolean;
  /** The name of a person, « un ancien membre » for one who left the project. */
  readonly nameOf: (userId: string) => string;
}

export const SheetContext = createContext<SheetValue | null>(null);

export function useSheet(): SheetValue {
  const value = useContext(SheetContext);
  if (value === null) throw new Error('useSheet outside SheetContext.');
  return value;
}
