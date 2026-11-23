// pmap.ts
// The .pmap document as the interface reads it (format 1.5, ADR 105), written once: the shape the host writes and the published schema describes, held equal to that schema by a test.

import { PRODUCT_NAME } from './i18n/i18n.ts';

/** The value of the field `format` of every .pmap: the product name in lower case, written once (spec 3.1). */
export const PMAP_FORMAT = PRODUCT_NAME.toLowerCase();

/** The texts of an object in the other languages, by language. */
export type PmapTranslations = Readonly<Record<string, string>>;

/** The comment of presentation of a step or a flow (1.5): its line in the language of origin, and its other lines. */
export interface PmapComment {
  readonly text?: string;
  readonly translations?: PmapTranslations;
}

export interface PmapProject {
  readonly code?: string;
  readonly name: string;
  readonly language?: string;
  readonly description?: string;
  readonly translations?: PmapTranslations;
  readonly workday_hours?: number;
  readonly fte_hours_per_month?: number;
  readonly glossary?: string;
}

export interface PmapLane {
  readonly id: string;
  readonly name: string;
  readonly type?: 'person' | 'department' | 'system';
  readonly color?: string;
  readonly headcount?: number;
  readonly translations?: PmapTranslations;
}

export type PmapStepType = 'task' | 'decision' | 'parallel' | 'inclusive' | 'wait' | 'start' | 'end';

export interface PmapDocumentEntry {
  readonly id?: string;
  readonly name: string;
  readonly location?: string;
  readonly share_confirmed?: boolean;
  readonly wait_hours?: number;
  readonly medium?: 'paper' | 'email' | 'spreadsheet' | 'file' | 'software' | 'other';
}

export interface PmapUse {
  readonly document: string;
  readonly wait_hours?: number;
  readonly percent_complete_accurate?: number;
  readonly pain_point?: string;
  readonly pain_point_code?: string;
  readonly pain_point_status?: 'reported' | 'scored' | 'under_review' | 'in_progress' | 'implemented' | 'adopted' | 'dropped';
  readonly lost_hours_per_month?: number;
  readonly action?: string;
  readonly owner?: string;
  readonly due_date?: string;
}

export interface PmapTask {
  readonly text?: string;
  readonly tool?: string;
  readonly nature?: 'extract' | 'copy_paste' | 're_enter' | 'format' | 'calculate' | 'check' | 'chase' | 'file' | 'send' | 'wait';
}

export interface PmapSheet {
  readonly actor?: string;
  readonly purpose?: string;
  readonly tools?: string;
  readonly frequency?: 'daily' | 'weekly' | 'monthly' | 'month_end_close' | 'quarterly' | 'yearly' | 'occasional' | 'per_case';
  readonly duration_minutes?: number;
  readonly duration_min_minutes?: number;
  readonly duration_max_minutes?: number;
  readonly lead_time?: { readonly value: number; readonly unit: 'minute' | 'hour' | 'day' };
  readonly procedure?: readonly PmapTask[];
  readonly free_text?: string;
  readonly outputs?: readonly PmapDocumentEntry[];
  readonly inputs?: readonly PmapDocumentEntry[];
  readonly uses?: readonly PmapUse[];
}

export interface PmapStep {
  readonly id: string;
  readonly lane: string;
  readonly label: string;
  readonly type?: PmapStepType;
  readonly column?: number;
  readonly row?: number;
  readonly automation?: 'manual' | 'in_progress' | 'automated';
  readonly value_class?: 'value_added' | 'necessary' | 'waste';
  readonly raci?: Readonly<Record<string, 'A' | 'S' | 'C' | 'I' | 'R'>>;
  readonly sub_map?: PmapMap;
  readonly translations?: PmapTranslations;
  readonly comment?: PmapComment;
  readonly sheet?: PmapSheet;
  readonly volume?: { readonly quantity: number; readonly period: 'day' | 'week' | 'month' | 'year' };
}

export type PmapPassage =
  | { readonly aisle: number }
  | { readonly column: number }
  | { readonly lane: string; readonly row: number; readonly edge?: 'top' | 'bottom' };

export interface PmapFlow {
  readonly source: string;
  readonly target: string;
  readonly label?: string;
  readonly share?: number;
  readonly route?: { readonly from?: 'top' | 'bottom' | 'left' | 'right'; readonly to?: 'top' | 'bottom' | 'left' | 'right'; readonly via?: PmapPassage };
  readonly translations?: PmapTranslations;
  readonly comment?: PmapComment;
}

export interface PmapMap {
  readonly lanes: readonly PmapLane[];
  readonly steps?: readonly PmapStep[];
  readonly flows?: readonly PmapFlow[];
  readonly summary?: { readonly text: string; readonly translations?: PmapTranslations };
}

export interface PmapDocument {
  readonly $schema?: string;
  /** Always {@link PMAP_FORMAT}. */
  readonly format: string;
  readonly version: string;
  readonly generated_by?: string;
  readonly generated_at?: string;
  readonly project: PmapProject;
  readonly map: PmapMap;
  readonly uncertainties?: readonly string[];
}

/** A text the host wrote, read as a document: the host checked it by its rules, the interface only gives it its type. */
export const pmapOf = (text: string): PmapDocument => JSON.parse(text) as PmapDocument;
