// useWorkbook.ts
// The three steps of the workbook of the translations: download it with the glossary saved first, copy the instruction, bring it back and keep its report (annex B 5).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client.ts';
import { isApiError } from '../../api/errors.ts';
import { download, exportFileName } from '../../shared/download.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import type { Translate } from '../../shared/i18n/i18n.ts';
import { linesByReason, type ReasonLines } from './model/report.ts';
import { buildWorkbook, instructionOf, instructionText } from './model/workbook.ts';
import { readWorkbook, WorkbookError, type ScopeCode } from './model/workbookReader.ts';
import type { Glossary, Status } from './useGlossary.ts';

export interface ImportReported {
  readonly written: number;
  readonly groups: readonly ReasonLines[];
}

export interface WorkbookSteps {
  readonly busy: boolean;
  readonly report: ImportReported | null;
  /** The lines of a workbook the host refused, each with its line and its message (A-2.14-22). */
  readonly rowErrors: readonly string[];
  readonly downloadWorkbook: (scope: ScopeCode) => Promise<void>;
  readonly copyInstruction: () => void;
  readonly importWorkbook: (file: File) => Promise<void>;
}

export interface WorkbookOptions {
  readonly project: { readonly id: string; readonly code: string };
  /** The columns of the workbook: the language of origin and the languages chosen (ADR 083). */
  readonly languages: readonly string[];
  readonly glossary: Glossary;
  readonly onStatus: (status: Status | null) => void;
  readonly onImported: () => void;
}

/** The messages of the lines a refusal names, one a line (TRADUCTIONS_INVALIDES, 1.x: `erreurs[].message`). */
function rowErrorsOf(failure: unknown): string[] {
  if (!isApiError(failure) || !Array.isArray(failure.details.errors)) return [];
  return failure.details.errors.map((error: unknown) => (typeof error === 'object' && error !== null && 'message' in error ? String(error.message) : ''))
    .filter((message) => message !== '');
}

export function useWorkbook({ project, languages, glossary, onStatus, onImported }: WorkbookOptions): WorkbookSteps {
  const { t } = useTranslation();
  const translate: Translate = (key, options) => t(key, options);
  const [busy, setBusy] = useState(false);
  const [report, setReport] = useState<ImportReported | null>(null);
  const [rowErrors, setRowErrors] = useState<string[]>([]);
  const run = async (work: () => Promise<void>) => {
    setBusy(true);
    onStatus(null);
    try {
      await work();
    } catch (failure) {
      onStatus({ kind: 'error', text: failure instanceof WorkbookError ? t(`translations.errors.${failure.code}`) : messageOf(failure) });
      setRowErrors(rowErrorsOf(failure));
    } finally {
      setBusy(false);
    }
  };
  /** B-5-06: the texts of the scope read from the host, the workbook built here with the glossary saved first (B-5-13, B-5-14). */
  const downloadWorkbook = (scope: ScopeCode) => run(async () => {
    const glossaryText = glossary.wanted;
    if (!(await glossary.save())) return;
    const list = await api.get('/api/projects/{id}/translations', { path: { id: project.id }, query: { scope, languages: languages.join(',') } });
    if (list.texts.length === 0) {
      onStatus({ kind: 'info', text: t('translations.nothing') });
      return;
    }
    download(await buildWorkbook(list, glossaryText, translate), exportFileName(project.code, t('translations.file_name'), '.xlsx'));
    onStatus({ kind: 'success', text: t('translations.downloaded', { count: list.texts.length }) });
  });
  /** B-5-16 to B-5-24: the workbook checked and read here, sent in one pass, the report kept and the map read again. */
  const importWorkbook = (file: File) => run(async () => {
    setReport(null);
    setRowErrors([]);
    const read = await readWorkbook(await file.arrayBuffer(), project.id);
    const reply = await api.put('/api/projects/{id}/translations', { path: { id: project.id }, body: { scope: read.scope, rows: [...read.rows] } });
    setReport({ written: reply.written, groups: linesByReason(reply.ignored, read.unknownKeys) });
    onImported();
  });
  /** B-5-15: the instruction, with the glossary of the field, in the clipboard. */
  const copyInstruction = () => {
    void navigator.clipboard.writeText(instructionText(instructionOf(languages, glossary.wanted, translate), translate)).then(() => {
      onStatus({ kind: 'info', text: t('translations.instruction_copied') });
    }, (failure: unknown) => {
      onStatus({ kind: 'error', text: messageOf(failure) });
    });
  };
  return { busy, report, rowErrors, downloadWorkbook, copyInstruction, importWorkbook };
}
