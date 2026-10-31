// draft.ts
// The sheet as it is typed: the draft read from its content, what is sent of it, the mode at the loading, the summary the map shows, the tools offered (annex B 4).

import type { Schema } from '../../../api/client.ts';

type SheetContent = Schema<'SheetContent'>;
type SaveSheetBody = Schema<'SaveSheetBody'>;
type SheetFieldBody = Schema<'SheetFieldBody'>;

export type FrequencyCode = NonNullable<Schema<'Frequency'>>;
export type Nature = NonNullable<Schema<'OperationNature'>>;
export type LeadTimeUnit = Schema<'LeadTimeUnit'>;

/** The frequencies in the order of their list (B-4-26), « À chaque dossier » last (spec 14.2, S5). */
export const FREQUENCIES: readonly FrequencyCode[] = ['daily', 'weekly', 'monthly', 'month_end_close', 'quarterly', 'yearly', 'occasional', 'per_case'];

/** The units of a lead time (spec 14.2, S3): minutes, hours, working days. */
export const LEAD_TIME_UNITS: readonly LeadTimeUnit[] = ['minute', 'hour', 'day'];

/** The natures of an operation in the order of their list (B-4-32). */
export const NATURES: readonly Nature[] = ['extract', 'copy_paste', 're_enter', 'format', 'calculate', 'check', 'chase', 'file', 'send', 'wait'];

/** A line of the procedure as typed; an empty tool is none. */
export interface ProcedureLine {
  readonly text: string;
  readonly tool: string;
  readonly nature: Nature | null;
}

/**
 * The sheet as typed: the texts of its fields, the duration and the lead time as written in their fields with the unit
 * of the lead time (spec 14.2, S3), the procedure and the free text.
 */
export interface SheetDraft {
  readonly actor: string;
  readonly purpose: string;
  readonly tools: string;
  readonly frequency: FrequencyCode | null;
  readonly duration: string;
  readonly leadTime: string;
  readonly leadTimeUnit: LeadTimeUnit;
  readonly procedure: readonly ProcedureLine[];
  readonly freeText: string;
}

export const EMPTY_DRAFT: SheetDraft = {
  actor: '', purpose: '', tools: '', frequency: null, duration: '', leadTime: '', leadTimeUnit: 'hour', procedure: [], freeText: '',
};

export function draftOf(content: SheetContent): SheetDraft {
  return {
    actor: content.actor ?? '', purpose: content.purpose ?? '', tools: content.tools ?? '', frequency: content.frequency ?? null,
    duration: content.duration_minutes === null ? '' : String(content.duration_minutes),
    leadTime: content.lead_time == null ? '' : String(content.lead_time.value), leadTimeUnit: content.lead_time?.unit ?? 'hour',
    procedure: content.procedure.map((task) => ({ text: task.text, tool: task.tool ?? '', nature: task.nature ?? null })),
    freeText: content.free_text ?? '',
  };
}

/** A number typed in a field, null when the field is empty or holds no number. */
export function numberOf(text: string): number | null {
  if (text.trim() === '') return null;
  const value = Number(text);
  return Number.isNaN(value) ? null : value;
}

/** What is sent (B-4-47 to B-4-49): the texts trimmed, the empty fields and the operations without text left out, an empty free text as null. */
export function bodyOf(draft: SheetDraft): SaveSheetBody {
  const fields: Record<string, SheetFieldBody> = {};
  for (const [key, text] of [['actor', draft.actor], ['purpose', draft.purpose], ['tools', draft.tools]] as const) {
    if (text.trim() !== '') fields[key] = { value: text.trim(), number: null };
  }
  if (draft.frequency !== null) fields.frequency = { value: draft.frequency, number: null };
  const duration = numberOf(draft.duration);
  if (duration !== null) fields.duration_minutes = { value: null, number: duration };
  // The lead time with its unit, both or none (spec 14.2, S3).
  const lead = numberOf(draft.leadTime);
  if (lead !== null) fields.lead_time = { value: draft.leadTimeUnit, number: lead };
  const procedure = draft.procedure.filter((line) => line.text.trim() !== '')
    .map((line) => ({ text: line.text.trim(), tool: line.tool.trim() === '' ? null : line.tool.trim(), nature: line.nature }));
  return { fields, procedure, free_text: draft.freeText.trim() === '' ? null : draft.freeText.trim() };
}

/** The free text is the only content of the sheet: the panel opens on it (B-4-19). */
export function onlyFreeText(content: SheetContent): boolean {
  const fields = [content.actor, content.purpose, content.tools, content.frequency, content.duration_minutes, content.lead_time ?? null];
  return (content.free_text ?? '') !== '' && fields.every((field) => field === null) && content.procedure.length === 0;
}

/** The summary the step shows on the map, from the sheet read: its gauge, its frequency, its duration, its lead time (B-4-50, spec 14.2). */
export function summaryOf(view: Schema<'SheetView'>): Schema<'SheetSummary'> {
  return {
    status: view.status, completeness: view.completeness, can_fill: view.permissions.fill, frequency: view.content.frequency ?? null,
    duration_minutes: view.content.duration_minutes, lead_time: view.content.lead_time ?? null,
  };
}

/** The tools cited in the field, split on commas, semicolons and line breaks (B-4-24). */
export function citedTools(text: string): string[] {
  return text.split(/[,;\n]/).map((tool) => tool.trim()).filter((tool) => tool !== '');
}

/** The suggested tools the field does not cite yet, whatever their case (B-4-24). */
export function offeredTools(suggestions: readonly string[], tools: string): string[] {
  const cited = new Set(citedTools(tools).map((tool) => tool.toLocaleLowerCase()));
  return suggestions.filter((tool) => !cited.has(tool.toLocaleLowerCase()));
}

/** The field with a tool added after the others. */
export function withTool(tools: string, tool: string): string {
  const current = tools.trim();
  return current === '' ? tool : `${current.replace(/[,;]\s*$/, '')}, ${tool}`;
}
