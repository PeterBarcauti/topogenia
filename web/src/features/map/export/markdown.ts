// markdown.ts
// The export of a map in Markdown (plan 3.1, L3): its title, its breadcrumb, its classification and its date, the map as a Mermaid flowchart, then the tables of the workbook (steps, links, RACI matrix, the value stream with its layer on, the action plan read from the host when it holds a pain point), in the language the map is shown in.

import type { Translate } from '../../../shared/i18n/i18n.ts';
import type { Schema } from '../../../api/client.ts';
import { flowchartOf, type MermaidLook } from './mermaid.ts';
import { workbookSheets, type CellValue, type Sheet } from './workbook.ts';

/** The media type of the file saved. */
export const MARKDOWN_TYPE = 'text/markdown;charset=utf-8';

/** What the document says of itself, as the PDF says it (B-2.14-14 to B-2.14-18). */
export interface MarkdownFacts {
  readonly title: string;
  readonly breadcrumb: string;
  readonly classification: string;
  readonly date: string;
  readonly footer: string;
}

/** The signs that make a text inside a line a code, an emphasis, a link, an image, HTML, a cell or a strike: escaped. */
const MARKS = /[\\`*_[\]<|~]/g;

/** A text on one line that stays a text in Markdown: its line ends joined, its signs escaped (a text of a model or of a map is never markup). */
export const inline = (text: string): string => text.replace(/\s*[\r\n]+\s*/g, ' ').trim().replace(MARKS, '\\$&');

/**
 * A text of paragraphs that stays a text in Markdown: each line trimmed (no code by indentation), its signs escaped, the
 * signs that would make it a heading, a quote, a list, a rule or the underline of a title escaped at its start; three
 * line ends or more made two.
 */
export const inertText = (text: string): string =>
  text.split(/\r?\n/).map((line) => line.trim().replace(MARKS, '\\$&').replace(/^([#>+=-])/, '\\$1').replace(/^(\d+)([.)])/, '$1\\$2'))
    .join('\n').replace(/\n{3,}/g, '\n\n').trim();

/** A cell of a table: a number as the language writes it, a text on one line that stays a text. */
const cellOf = (value: CellValue, language: string): string =>
  typeof value === 'number' ? new Intl.NumberFormat(language, { maximumFractionDigits: 2 }).format(value) : inline(value);

/** A sheet of the workbook as a Markdown table, its header first. */
export function tableOf(sheet: Pick<Sheet, 'headers' | 'rows'>, language: string): string {
  const row = (cells: readonly CellValue[]) => `| ${sheet.headers.map((_, index) => cellOf(cells[index] ?? '', language)).join(' | ')} |`;
  return [row(sheet.headers), `|${sheet.headers.map(() => ' --- ').join('|')}|`, ...sheet.rows.map(row)].join('\n');
}

/** A fenced block of Mermaid. */
export const mermaidBlock = (diagram: string): string => ['```mermaid', diagram, '```'].join('\n');

/**
 * The summary of the map under the title (plan 3.1, lot R), in italics, in the language of the export: none when the map
 * has none, or when it changed since the summary was written, which would tell another map.
 */
export function summaryLines(map: Pick<Schema<'MapView'>, 'summary'>, language: string): string[] {
  const summary = map.summary;
  if (summary === null || summary === undefined || summary.stale) return [];
  return [`_${inline(summary.translations[language] ?? summary.text)}_`, ''];
}

/**
 * The document: title, summary, breadcrumb, classification and date, the diagram, then each table of the workbook under its
 * name, the sheets read from the host after them (the action plan, Makigami M7; audit 3.1 V3, U9).
 */
export function mapMarkdown(map: Schema<'MapView'>, language: string, t: Translate, facts: MarkdownFacts, look: MermaidLook, valueStream = false, read: readonly Sheet[] = []): string {
  const parts = [`# ${inline(facts.title)}`, '', ...summaryLines(map, language)];
  if (facts.breadcrumb !== '') parts.push(`> ${inline(facts.breadcrumb)}`, '>');
  parts.push(`> ${facts.classification} · ${facts.date}`, '', `## ${t('export.md_diagram')}`, '', mermaidBlock(flowchartOf(map, language, { look })));
  for (const sheet of [...workbookSheets(map, language, t, valueStream), ...read]) parts.push('', `## ${sheet.name}`, '', tableOf(sheet, language));
  parts.push('', `_${facts.footer}_`, '');
  return parts.join('\n');
}
