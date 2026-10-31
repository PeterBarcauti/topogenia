// TranslationsDialog.tsx
// The translations of a project: by the assistant into one language of the list (ADR 083), or by an Excel workbook in three steps: download the texts of the languages chosen with the glossary, have them completed, bring the workbook back and read the report (annex B 5, ADR 065).

import { useRef, useState, type ChangeEvent } from 'react';
import { ClipboardCopy, FileDown, FileUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../shared/ui/Button.tsx';
import { Dialog } from '../../shared/ui/Dialog.tsx';
import { WORKBOOK_TYPE } from '../../shared/download.ts';
import type { ProjectLanguages } from '../../shared/state/processLanguage.ts';
import type { ScopeCode } from './model/workbookReader.ts';
import { AssistantTranslation, WorkbookLanguages } from './AssistantTranslation.tsx';
import { GlossaryField, ImportReport, ScopeChoice, StatusLine } from './DialogParts.tsx';
import { useGlossary, type Status } from './useGlossary.ts';
import { useWorkbook } from './useWorkbook.ts';

export interface TranslationsDialogProps {
  readonly project: { readonly id: string; readonly code: string };
  /** The configured languages, among which the workbook and the assistant translate. */
  readonly languages: readonly string[];
  /** The language of origin of the project and how far each language is written (ADR 083). */
  readonly coverage: ProjectLanguages;
  /** The language shown, chosen first; the one asked from the banner of the map, if any. */
  readonly preselected?: string | undefined;
  /** Asks the assistant for the whole process in a language; none hides the choice. */
  readonly onAssistant?: ((language: string) => void) | undefined;
  /** Reads the map again once translations are saved (B-5-24: 1.x `projet.recharger()`). */
  readonly onImported: () => void;
  readonly onClose: () => void;
}

/** The languages ticked first for the workbook: those already translated, and the one shown (ADR 083). */
function firstTargets(languages: readonly string[], coverage: ProjectLanguages, preselected: string | undefined): string[] {
  return languages.filter((code) => code !== coverage.origin && ((coverage.written[code] ?? 0) > 0 || code === preselected));
}

/** A dialog wide: the assistant first, then the workbook in three steps (B-5-02), for who may translate the project (B-5-01). */
export function TranslationsDialog({ project, languages, coverage, preselected, onAssistant, onImported, onClose }: TranslationsDialogProps) {
  const { t } = useTranslation();
  const [scope, setScope] = useState<ScopeCode>('missing');
  const [status, setStatus] = useState<Status | null>(null);
  const [targets, setTargets] = useState<readonly string[]>(() => firstTargets(languages, coverage, preselected));
  const glossary = useGlossary(project.id, setStatus);
  // The columns of the workbook: the language of origin and the languages ticked, in the order of the configuration.
  const columns = languages.filter((code) => code === coverage.origin || targets.includes(code));
  const steps = useWorkbook({ project, languages: columns, glossary, onStatus: setStatus, onImported });
  const chooser = useRef<HTMLInputElement>(null);
  const chosen = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file !== undefined) void steps.importWorkbook(file);
  };
  const footer = <Button testId="translations-done" variant="secondary" onClick={onClose}>{t('app.close')}</Button>;
  return (
    <Dialog title={t('translations.title')} testId="translations" onClose={onClose} footer={footer} wide>
      {onAssistant !== undefined && <AssistantTranslation languages={languages} coverage={coverage} preselected={preselected} onTranslate={onAssistant} />}
      <h3 className="dialog-subtitle translation-workbook">{t('translations.workbook_title')}</h3>
      <p className="muted">{t('translations.help')}</p>
      <section className="translation-step" aria-labelledby="translations-step1">
        <h3 id="translations-step1" className="dialog-subtitle">{t('translations.step1')}</h3>
        <WorkbookLanguages languages={languages} origin={coverage.origin} chosen={targets} disabled={steps.busy} onChange={setTargets} />
        <ScopeChoice scope={scope} disabled={steps.busy} onChange={setScope} />
        <GlossaryField glossary={glossary} busy={steps.busy} />
        <Button testId="translations-download" icon={FileDown} variant="primary" disabled={steps.busy || targets.length === 0} onClick={() => void steps.downloadWorkbook(scope)}>
          {t('translations.download')}
        </Button>
      </section>
      <section className="translation-step" aria-labelledby="translations-step2">
        <h3 id="translations-step2" className="dialog-subtitle">{t('translations.step2')}</h3>
        <p className="muted">{t('translations.step2_help', { sheet: t('translations.sheet_instruction') })}</p>
        <Button testId="translations-copy" icon={ClipboardCopy} variant="secondary" onClick={steps.copyInstruction}>{t('translations.copy_instruction')}</Button>
      </section>
      <section className="translation-step" aria-labelledby="translations-step3">
        <h3 id="translations-step3" className="dialog-subtitle">{t('translations.step3')}</h3>
        <Button testId="translations-import" icon={FileUp} variant="secondary" disabled={steps.busy} onClick={() => chooser.current?.click()}>
          {t('translations.import')}
        </Button>
        <input ref={chooser} type="file" accept={`.xlsx,${WORKBOOK_TYPE}`} hidden aria-label={t('translations.import')} data-testid="translations-file" onChange={chosen} />
      </section>
      {status !== null && <StatusLine status={status} />}
      {steps.rowErrors.length > 0 && (
        <ul className="file-faults" data-testid="translations-errors">{steps.rowErrors.map((error) => <li key={error}>{error}</li>)}</ul>
      )}
      {steps.report !== null && <ImportReport written={steps.report.written} groups={steps.report.groups} />}
    </Dialog>
  );
}
