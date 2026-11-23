// collection.ts
// The lines of a collection of templates (.pmaps) as the dialog shows them: how a text is known for a collection, the lines read from the preview, their ticks, their codes and their state, all pure (3.4, lot 6, ADR 114).

import type { Schema } from '../../../api/client.ts';
import { PMAP_FORMAT } from '../../../shared/pmap.ts';
import { cleanContent, PROJECT_CODE } from './pmapText.ts';

/** The largest file the opening reads, in bytes: the request the host reads for the opening of a file (`PmapApi.OpeningLimit`). */
export const OPENED_BYTES_MAX = 10 * 1024 * 1024;

/** The `format` an envelope of a collection declares. */
const COLLECTION_FORMAT = `${PMAP_FORMAT}-collection`;

/** A text that declares the format of a collection, wherever its key stands. */
const DECLARES_COLLECTION = new RegExp(`"format"\\s*:\\s*"${COLLECTION_FORMAT}"`);

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);

/**
 * Whether a text is a collection: its `format` read from the JSON; for a text the JSON parser refuses (comments, trailing
 * commas, which the host accepts), the line that declares it. A `.pmap` that only mentions the word is not one.
 */
export function isCollection(text: string): boolean {
  const clean = cleanContent(text);
  if (!clean.includes(COLLECTION_FORMAT)) return false;
  try {
    const document: unknown = JSON.parse(clean);
    return isRecord(document) && document.format === COLLECTION_FORMAT;
  } catch {
    return DECLARES_COLLECTION.test(clean);
  }
}

/** Where a line stands: waiting, being created, created, refused. */
export type RowState = 'idle' | 'creating' | 'created' | 'failed';

/** The refusal of a line: its message, the code of the error, the faults of the file the host lists. */
export interface Refusal {
  readonly message: string;
  readonly code: string | null;
  readonly faults: readonly { readonly path: string; readonly message: string }[];
}

/** The refusal of a line: the refusal, and the code the line was tried with, which the reason names while another is typed. */
export interface LineRefusal extends Refusal {
  readonly triedCode: string;
}

/** The code of the refusal of a line given the code of another line, told before anything is sent for it. */
export const DUPLICATE_CODE = 'DUPLICATE_CODE';

/** One process of the collection: what the preview says of it, and what the person chose. */
export interface CollectionRow {
  readonly index: number;
  readonly name: string | null;
  readonly language: string | null;
  readonly description: string | null;
  readonly counts: Schema<'PmapCounts'> | null;
  readonly uncertainties: readonly string[];
  /** The addresses of its documents that reach another machine (web, network path): said before a link is followed. */
  readonly remoteLocations: number;
  readonly faults: readonly Schema<'CollectionFault'>[];
  readonly content: string;
  readonly code: string;
  readonly ticked: boolean;
  readonly state: RowState;
  readonly refusal: LineRefusal | null;
  readonly opened: Schema<'PmapOpened'> | null;
}

/** The lines of a preview: those the host reads are ticked, those it refuses are not. */
export const rowsOf = (preview: Schema<'CollectionPreview'>): CollectionRow[] => preview.processes.map((process) => ({
  index: process.index, name: process.name, language: process.language, description: process.description, counts: process.counts, uncertainties: process.uncertainties,
  remoteLocations: process.remote_locations, faults: process.faults, content: process.content, code: process.proposed_code ?? '', ticked: process.faults.length === 0, state: 'idle', refusal: null, opened: null,
}));

/** A line the host reads: the only one that may be ticked. */
export const isReadable = (row: CollectionRow): boolean => row.faults.length === 0;

export const changeRow = (rows: readonly CollectionRow[], index: number, change: Partial<CollectionRow>): CollectionRow[] =>
  rows.map((row) => (row.index === index ? { ...row, ...change } : row));

/** The lines that can be ticked, and those that are. */
export const readableRows = (rows: readonly CollectionRow[]): CollectionRow[] => rows.filter(isReadable);
export const tickedRows = (rows: readonly CollectionRow[]): CollectionRow[] => rows.filter((row) => row.ticked && isReadable(row));

/** All the readable lines ticked, or none. */
export const tickAll = (rows: readonly CollectionRow[], ticked: boolean): CollectionRow[] => rows.map((row) => (isReadable(row) ? { ...row, ticked } : row));

/** The lines still to create: ticked and not created yet. */
export const pendingRows = (rows: readonly CollectionRow[]): CollectionRow[] => tickedRows(rows).filter((row) => row.state !== 'created');

/** Whether « Créer les projets » is possible: a line at least, and every line ticked with a code of the right form. */
export const mayCreate = (rows: readonly CollectionRow[]): boolean => {
  const pending = pendingRows(rows);
  return pending.length > 0 && pending.every((row) => PROJECT_CODE.test(row.code));
};

/**
 * The lines to create that would take a code already given: to a line created, or to a line before them in the list
 * sent. They are refused at their place, nothing sent for them; the first line of a code goes.
 */
export const duplicatesOf = (rows: readonly CollectionRow[], targets: readonly CollectionRow[]): Set<number> => {
  const given = new Set(rows.filter((row) => row.state === 'created').flatMap((row) => [row.code, row.opened?.code ?? row.code]));
  const twice = new Set<number>();
  for (const row of targets) {
    if (given.has(row.code)) twice.add(row.index);
    else given.add(row.code);
  }
  return twice;
};

/** What the host reads to create a line: its text as the preview gave it, its code, its name (the code when it has none). */
export const bodyOf = (row: CollectionRow) => ({ content: row.content, code: row.code, name: (row.name ?? '').trim() === '' ? row.code : (row.name ?? '').trim() });
