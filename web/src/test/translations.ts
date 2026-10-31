// translations.ts
// Fictional texts of a project to translate, and the workbook opened again, for the tests of the translations (annex B 5).

import ExcelJS from 'exceljs';
import type { Schema } from '../api/client.ts';
import type { FakeReply } from './fakeApi.ts';
import { i18next, type Translate } from '../shared/i18n/i18n.ts';

export const PROJECT = '0192f0a0-0000-7000-8000-000000000001';
export const STEP = '0192f0a0-0000-7000-8000-000000000002';
export const FLOW = '0192f0a0-0000-7000-8000-000000000003';

/** The texts of the interface in French, as the tests of the screens show them. */
export const french: Translate = (key, options) => i18next.getFixedT('fr')(key, options);

/** Three texts in reading order: the project, a step entered in French, a flow label entered in English. */
export function textsOf(languages: readonly string[] = ['fr', 'en'], scope = 'missing'): Schema<'TranslationList'> {
  const text = (type: Schema<'TranslationObject'>, id: string, code: string, context: string, language: string, original: string) =>
    ({ type, id, code, context, language, original, translations: {}, sources: {} });
  return {
    project_id: PROJECT, project_code: 'VTE', scope, languages: [...languages],
    texts: [
      text('project', PROJECT, 'VTE', '', 'fr', 'Ventes'),
      text('step', STEP, 'VTE-E001', 'Ventes / Commercial', 'fr', 'Saisir la commande'),
      text('flow', FLOW, 'VTE-E001 > VTE-E002', 'Saisir la commande > Valider', 'en', 'yes'),
    ],
  };
}

/** A workbook given to the browser, opened again as a person would open it. */
export async function opened(blob: Blob): Promise<ExcelJS.Workbook> {
  const book = new ExcelJS.Workbook();
  await book.xlsx.load(await blob.arrayBuffer());
  return book;
}

/** The bytes of a workbook, as a file chosen in the dialog gives them. */
export async function bytesOf(book: ExcelJS.Workbook): Promise<ArrayBuffer> {
  const buffer = await book.xlsx.writeBuffer();
  return new Uint8Array(buffer).slice().buffer;
}

/** The detail of the project VTE as the host gives it, with its glossary of the translations and its version. */
export const detailOf = (glossary: string | null, version = 4): FakeReply => ({
  json: {
    id: PROJECT, code: 'VTE', name: 'Ventes', source_language: 'fr', name_translations: {}, description: null, shared_folder: null, classification: 'internal',
    sharing_group_label: null, translation_glossary: glossary, root_map_id: 'm-vte', role: 'facilitator', members: [], version,
  },
});
