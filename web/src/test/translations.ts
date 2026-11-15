// translations.ts
// Fictional texts of a project to translate, its comments of presentation among them, and the workbook opened again, for the tests of the translations (annex B 5).

import ExcelJS from 'exceljs';
import type { Schema } from '../api/client.ts';
import type { FakeReply } from './fakeApi.ts';
import { i18next, type Translate } from '../shared/i18n/i18n.ts';

export const PROJECT = '0192f0a0-0000-7000-8000-000000000001';
export const STEP = '0192f0a0-0000-7000-8000-000000000002';
export const FLOW = '0192f0a0-0000-7000-8000-000000000003';

/** The texts of the interface in French, as the tests of the screens show them. */
export const french: Translate = (key, options) => i18next.getFixedT('fr')(key, options);

/** The comment of presentation of the step of the sample, on two lines, with its English written by the assistant. */
export const commentOf = (): Schema<'CommentText'> => ({
  step_id: STEP, code: 'VIS-E001', context: 'Visites / Commercial', language: 'fr', original: 'Le commercial saisit la commande.\nIl la confirme au client.',
  translations: { en: 'Visits enters the order.\nThey confirm it to the customer.' }, sources: { en: 'assistant' },
});

/** The comment of presentation of the flow of the sample, named as the label of that flow is, with its English typed by hand. */
export const flowCommentOf = (): Schema<'FlowCommentText'> => ({
  flow_id: FLOW, code: 'VIS-E001 > VIS-E002', context: 'Saisir la commande > Valider', language: 'fr', original: 'La commande part chez le responsable.\nIl la valide le jour même.',
  translations: { en: 'The order goes to the manager.' }, sources: { en: 'manual' },
});

/** Three texts in reading order: the project, a step entered in French, a flow label entered in English; then the comments given, those of the steps, then those of the flows. */
export function textsOf(languages: readonly string[] = ['fr', 'en'], scope = 'missing', comments: readonly Schema<'CommentText'>[] = [],
  flowComments: readonly Schema<'FlowCommentText'>[] = []): Schema<'TranslationList'> {
  const text = (type: Schema<'TranslationObject'>, id: string, code: string, context: string, language: string, original: string) =>
    ({ type, id, code, context, language, original, translations: {}, sources: {} });
  return {
    project_id: PROJECT, project_code: 'VIS', scope, languages: [...languages],
    texts: [
      text('project', PROJECT, 'VIS', '', 'fr', 'Visites'),
      text('step', STEP, 'VIS-E001', 'Visites / Commercial', 'fr', 'Saisir la commande'),
      text('flow', FLOW, 'VIS-E001 > VIS-E002', 'Saisir la commande > Valider', 'en', 'yes'),
    ],
    comments: [...comments],
    flow_comments: [...flowComments],
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

/** The detail of the project VIS as the host gives it, with its glossary of the translations and its version. */
export const detailOf = (glossary: string | null, version = 4): FakeReply => ({
  json: {
    id: PROJECT, code: 'VIS', name: 'Visites', source_language: 'fr', name_translations: {}, description: null, shared_folder: null, classification: 'internal',
    sharing_group_label: null, translation_glossary: glossary, root_map_id: 'm-vis', role: 'facilitator', members: [], version,
  },
});
