// documents.ts
// The documents of a sheet: the form of a document and what is sent of it, the inputs in their groups, the use of a ticked input and its pain point, the consumers of an output (annex B 4).

import type { Schema } from '../../../api/client.ts';
import { numberOf } from './draft.ts';

type SheetView = Schema<'SheetView'>;
type ProposedInput = Schema<'ProposedInput'>;
type PainPoint = Schema<'PainPoint'>;
type DocumentUseBody = Schema<'DocumentUseBody'>;

export type Direction = Schema<'DocumentDirection'>;

/** A document as typed in its form: new (no identifier) or changed with the version read. */
export interface DocumentEntry {
  readonly id: string | null;
  readonly version: number;
  readonly name: string;
  readonly location: string;
  readonly wait: string;
  readonly shared: boolean;
}

export const NEW_DOCUMENT: DocumentEntry = { id: null, version: 0, name: '', location: '', wait: '', shared: false };

export function entryOf(document: Schema<'Document'>): DocumentEntry {
  return {
    id: document.id, version: document.version, name: document.name, location: document.location ?? '',
    wait: document.wait_hours === null ? '' : String(document.wait_hours), shared: document.share_confirmed,
  };
}

/**
 * The address a document opens in a new tab: an http(s) URL as the host types it (B-4-67); a path, or any address the
 * host did not type as a URL, is shown as a text, never as a link (the scheme checked here as well: no `javascript:`).
 */
export function webAddressOf(document: Schema<'Document'>): string | null {
  const location = document.location;
  return location !== null && document.location_type === 'url' && /^https?:\/\//i.test(location) ? location : null;
}

/** What is sent of a document: its texts trimmed, no address when empty, the wait for an input from outside only. */
export function documentBody(direction: Direction, entry: DocumentEntry): Schema<'DocumentBody'> {
  return {
    direction, name: entry.name.trim(), location: entry.location.trim() === '' ? null : entry.location.trim(),
    wait_hours: direction === 'input' ? numberOf(entry.wait) : null, share_confirmed: entry.shared,
  };
}

/**
 * The inputs shown: the outputs of the steps linked by an incoming flow, then those ticked from elsewhere, then the inputs
 * from outside (B-4-62, B-4-64); in reading, only the inputs used of the first two groups (B-4-77).
 */
export function inputGroups(inputs: readonly ProposedInput[], fill: boolean): ProposedInput[] {
  const shown = (input: ProposedInput) => fill || input.used;
  return [
    ...inputs.filter((input) => input.producer !== null && input.near && shown(input)),
    ...inputs.filter((input) => input.producer !== null && !input.near && shown(input)),
    ...inputs.filter((input) => input.producer === null),
  ];
}

export function painPointOf(view: SheetView, id: string | null): PainPoint | null {
  return id === null ? null : view.pain_points.find((pain) => pain.id === id) ?? null;
}

/** The use of a ticked input as typed: the wait, the share usable as is (%C&A), the pain point and the time it costs. */
export interface UseEntry {
  readonly wait: string;
  readonly percent: string;
  readonly painPoint: string;
  readonly lost: string;
}

const textOf = (value: number | null | undefined) => (value === null || value === undefined ? '' : String(value));

export function entryOfUse(input: ProposedInput, painPoint: PainPoint | null): UseEntry {
  return {
    wait: textOf(input.use?.wait_hours), percent: textOf(input.use?.percent_complete_accurate), painPoint: painPoint?.description ?? '',
    lost: textOf(painPoint?.lost_hours_per_month),
  };
}

/** The use as the host holds it, to send nothing when nothing changed (B-4-75). */
export function currentUse(input: ProposedInput, painPoint: PainPoint | null): DocumentUseBody {
  return {
    wait_hours: input.use?.wait_hours ?? null, percent_complete_accurate: input.use?.percent_complete_accurate ?? null,
    pain_point: painPoint?.description ?? null, lost_hours_per_month: painPoint?.lost_hours_per_month ?? null,
  };
}

/** What is sent of a use: the percentage rounded, the pain point trimmed, the time lost with a pain point only. */
export function bodyOfUse(entry: UseEntry): DocumentUseBody {
  const percent = numberOf(entry.percent);
  const painPoint = entry.painPoint.trim() === '' ? null : entry.painPoint.trim();
  return {
    wait_hours: numberOf(entry.wait), percent_complete_accurate: percent === null ? null : Math.round(percent), pain_point: painPoint,
    lost_hours_per_month: painPoint === null ? null : numberOf(entry.lost),
  };
}

/** The pain point is asked under 100 %, or once one is typed, or when the use has one (B-4-73). */
export function showsPainPoint(entry: UseEntry, painPoint: PainPoint | null): boolean {
  const percent = numberOf(entry.percent);
  return (percent !== null && percent < 100) || entry.painPoint.trim() !== '' || painPoint !== null;
}

/** A step that uses an output: its code when the map knows it, its share usable as is, its pain point (B-4-79, B-4-80). */
export interface Consumer {
  readonly code: string | null;
  readonly percent: number | null;
  readonly painPoint: PainPoint | null;
}

export function consumersOf(output: Schema<'StepOutput'>, view: SheetView, codeOf: (stepId: string) => string | null): Consumer[] {
  return output.used_by.map((use) => ({ code: codeOf(use.step_id), percent: use.percent_complete_accurate, painPoint: painPointOf(view, use.pain_point_id) }));
}
