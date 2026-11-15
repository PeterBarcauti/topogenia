// DialogParts.tsx
// The parts of the dialog of the translations: the choice of the scope, the glossary of the project, the status line and the report of an import (annex B 5).

import { useTranslation } from 'react-i18next';
import { Button } from '../../shared/ui/Button.tsx';
import { Field } from '../../shared/ui/Field.tsx';
import type { ReasonLines } from './model/report.ts';
import type { ScopeCode } from './model/workbookReader.ts';
import type { Glossary, Status } from './useGlossary.ts';

export const SCOPES: readonly ScopeCode[] = ['missing', 'auto', 'all'];

/** The texts to give: without translation, without manual translation, everything (B-5-03 to B-5-05). */
export function ScopeChoice({ scope, disabled, onChange }: { scope: ScopeCode; disabled: boolean; onChange: (scope: ScopeCode) => void }) {
  const { t } = useTranslation();
  return (
    <fieldset className="scope-choice" disabled={disabled} data-testid="translations-scope">
      <legend className="muted">{t('translations.scope')}</legend>
      {SCOPES.map((code) => (
        <label key={code} className="check">
          <input type="radio" name="translation-scope" value={code} checked={scope === code} data-testid={`translations-scope-${code}`}
            onChange={() => { onChange(code); }} />
          <span>
            <strong>{t(`translations.scope_${code}`)}</strong>{' '}
            <span className="muted">· {t(`translations.scope_${code}_help`)}</span>
          </span>
        </label>
      ))}
    </fieldset>
  );
}

/** The glossary of the project, written into the instruction sheet of the workbook (B-5-11, B-8.1-45). */
export function GlossaryField({ glossary, busy }: { glossary: Glossary; busy: boolean }) {
  const { t } = useTranslation();
  return (
    <div className="glossary">
      <Field label={t('translations.glossary')} testId="translations-glossary" hint={t('translations.glossary_help', { sheet: t('translations.sheet_instruction') })}>
        {(control) => (
          <textarea {...control} className="field-control glossary-text" rows={4} maxLength={4000} value={glossary.text} disabled={!glossary.ready || busy}
            onChange={(event) => { glossary.setText(event.target.value); }} />
        )}
      </Field>
      <Button testId="translations-glossary-save" variant="secondary" disabled={!glossary.ready || busy || !glossary.changed}
        onClick={() => void glossary.save()}>{t('translations.glossary_save')}</Button>
    </div>
  );
}

const STATUS_CLASSES: Readonly<Record<Status['kind'], string>> = { info: 'muted', success: 'message-success', error: 'message-error' };

export function StatusLine({ status }: { status: Status }) {
  return <p role={status.kind === 'error' ? 'alert' : 'status'} className={STATUS_CLASSES[status.kind]} data-testid="translations-status">{status.text}</p>;
}

/** « N translations saved », then the rows left aside by reason with their lines (B-5-22, B-5-23). */
export function ImportReport({ written, groups }: { written: number; groups: readonly ReasonLines[] }) {
  const { t } = useTranslation();
  return (
    <div role="status" className="translation-report" data-testid="translations-report">
      <p><strong>{t('translations.written', { count: written })}</strong></p>
      {groups.map((group) => (
        <p key={group.reason} className="muted" data-testid={`translations-ignored-${group.reason}`}>
          {t('translations.report_line', {
            reason: t(`translations.ignored.${group.reason}`, { count: group.lines.length }), lines: t('translations.lines', { lines: group.lines.join(', ') }),
          })}
        </p>
      ))}
    </div>
  );
}
