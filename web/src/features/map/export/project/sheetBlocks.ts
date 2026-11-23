// sheetBlocks.ts
// A sheet of the copy of the project as blocks of text for the PDF, in the sections and the order of the read view: the state, the filled facts, the inputs used, the procedure numbered, the outputs, the tools, the free text; the actor written as the team wrote it, never the location of a document (ADR 113).

import type { TFunction } from 'i18next';
import type { Schema } from '../../../../api/client.ts';
import type { PmapSheet } from '../../../../shared/pmap.ts';
import { citedTools } from '../../../sheet/model/draft.ts';
import { durationText, leadTimeText } from '../../../sheet/model/durationText.ts';

type SheetContent = Schema<'SheetContent'>;

/** What a block is: a section title, the state under a title, a fact with its name, an item of a list, a free text. */
export type BlockKind = 'heading' | 'note' | 'fact' | 'item' | 'text';

/** A block of text of a sheet: the name of a fact, the number or mark of an item, the muted detail under it. */
export interface Block {
  readonly kind: BlockKind;
  readonly text: string;
  readonly label?: string;
  readonly number?: string;
  readonly detail?: string;
}

/** The state of a sheet as its map shows it; a sheet never filled has none to say. */
export type SheetState = Schema<'SheetStatus'> | null;

/** A sheet to write: its content from the copy, its state, and the name of a document of the map by its identifier. */
export interface SheetInput {
  readonly sheet: PmapSheet;
  readonly status: SheetState;
  readonly documentName: (id: string) => string | undefined;
}

const BULLET = '•';

/** The content of a sheet of the copy as the read view gets it from the host, so that the texts of the facts are those of the sheet. */
function contentOf(sheet: PmapSheet): SheetContent {
  return {
    actor: sheet.actor ?? null, purpose: sheet.purpose ?? null, tools: sheet.tools ?? null, frequency: sheet.frequency ?? null,
    duration_minutes: sheet.duration_minutes ?? null, duration_min_minutes: sheet.duration_min_minutes ?? null, duration_max_minutes: sheet.duration_max_minutes ?? null,
    lead_time: sheet.lead_time ?? null, procedure: [], free_text: sheet.free_text ?? null,
  };
}

const filled = (text: string | null | undefined): string => (text ?? '').trim();

/** The facts filled, each under its name (the fields of the read view); nothing for an empty one. */
function factBlocks(sheet: PmapSheet, t: TFunction, language: string): Block[] {
  const content = contentOf(sheet);
  const texts: readonly (readonly [string, string | null])[] = [
    ['actor', filled(sheet.actor)],
    ['purpose', filled(sheet.purpose)],
    ['frequency', sheet.frequency === undefined ? null : t(`map.frequencies.${sheet.frequency}`)],
    ['duration', durationText(content, t, language)],
    ['lead_time', leadTimeText(content, t, language)],
  ];
  return texts.flatMap(([name, text]) => (text === null || text === '' ? [] : [{ kind: 'fact' as const, label: t(`sheet.${name}`), text }]));
}

/** The documents the step uses (the outputs of other steps, named by the map, with the code of their pain point), then its inputs from outside; no location. */
function inputBlocks(input: SheetInput, t: TFunction): Block[] {
  const used = (input.sheet.uses ?? []).flatMap((use) => {
    const name = filled(input.documentName(use.document));
    const code = filled(use.pain_point_code);
    return name === '' ? [] : [{ kind: 'item' as const, number: BULLET, text: name, ...(code === '' ? {} : { detail: t('sheet.pain_point.title_code', { code }) }) }];
  });
  const outside = (input.sheet.inputs ?? []).filter((document) => filled(document.name) !== '').map((document) => ({ kind: 'item' as const, number: BULLET, text: filled(document.name) }));
  return [...used, ...outside];
}

/** The procedure numbered, the tool and the nature after each task; a task with nothing in it is left out. */
function procedureBlocks(sheet: PmapSheet, t: TFunction): Block[] {
  const tasks = (sheet.procedure ?? []).map((task) => ({
    text: filled(task.text),
    detail: [filled(task.tool), task.nature === undefined ? '' : t(`sheet.natures.${task.nature}`)].filter((part) => part !== '').join(' · '),
  })).filter((task) => task.text !== '' || task.detail !== '');
  return tasks.map((task, index) => ({ kind: 'item' as const, number: `${String(index + 1)}.`, text: task.text, ...(task.detail === '' ? {} : { detail: task.detail }) }));
}

const section = (title: string, blocks: readonly Block[]): Block[] => (blocks.length === 0 ? [] : [{ kind: 'heading', text: title }, ...blocks]);

/**
 * The blocks of a sheet in the order of the read view (facts, inputs used, procedure, outputs, tools, free text), each section
 * under its title, the state first when the map shows one; nothing at all for a sheet with nothing in it (no page for it).
 * Writes the actor as the team wrote it; never the location of a document, nor the owner or the action of a pain point (cited by
 * its code, which the action plan explains).
 */
export function sheetBlocks(input: SheetInput, t: TFunction, language: string): Block[] {
  const { sheet, status } = input;
  const tools = citedTools(filled(sheet.tools));
  const free = filled(sheet.free_text);
  const outputs = (sheet.outputs ?? []).filter((document) => filled(document.name) !== '').map((document) => ({ kind: 'item' as const, number: BULLET, text: filled(document.name) }));
  const body: Block[] = [
    ...section(t('sheet.facts'), factBlocks(sheet, t, language)),
    ...section(t('sheet.inputs'), inputBlocks(input, t)),
    ...section(t('sheet.procedure'), procedureBlocks(sheet, t)),
    ...section(t('sheet.outputs'), outputs),
    ...(tools.length === 0 ? [] : [{ kind: 'fact' as const, label: t('sheet.tools'), text: tools.join(', ') }]),
    ...section(t('sheet.free_title'), free === '' ? [] : [{ kind: 'text' as const, text: free }]),
  ];
  if (body.length === 0) return [];
  const shown = status === 'draft' || status === 'validated' || status === 'needs_review';
  return shown ? [{ kind: 'note', text: t('project_pdf.sheet_state', { state: t(`map.sheet_states.${status}`) }) }, ...body] : body;
}
