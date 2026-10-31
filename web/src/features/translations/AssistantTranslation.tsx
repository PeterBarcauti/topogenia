// AssistantTranslation.tsx
// « Translate with the assistant », first in the dialog of the translations (ADR 083): the languages of the list, how far each is translated, one chosen and the whole process asked in it; the version of origin never changes.

import { useState } from 'react';
import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ownName, titleIn } from '../../shared/i18n/languageNames.ts';
import { coverageOf, type ProjectLanguages } from '../../shared/state/processLanguage.ts';
import { Button } from '../../shared/ui/Button.tsx';

export interface AssistantTranslationProps {
  /** The languages of the configuration, in its order. */
  readonly languages: readonly string[];
  readonly coverage: ProjectLanguages;
  /** The language chosen first: the one shown, or the one asked from the banner of the map. */
  readonly preselected?: string | undefined;
  readonly onTranslate: (language: string) => void;
}

export function AssistantTranslation({ languages, coverage, preselected, onTranslate }: AssistantTranslationProps) {
  const { t, i18n } = useTranslation();
  const targets = languages.filter((code) => code !== coverage.origin);
  const [chosen, setChosen] = useState<string | null>(preselected !== undefined && targets.includes(preselected) ? preselected : null);
  const percent = (code: string) => new Intl.NumberFormat(i18n.language, { style: 'percent' }).format(coverageOf(coverage, code) / 100);
  return (
    <section className="translation-step translation-assistant" aria-labelledby="translations-assistant" data-testid="translations-assistant">
      <h3 id="translations-assistant" className="dialog-subtitle"><Sparkles size={16} aria-hidden="true" />{t('translations.assistant_title')}</h3>
      <p className="muted">{t('translations.assistant_help', { origin: ownName(coverage.origin) })}</p>
      <div className="language-grid language-grid--compact" role="radiogroup" aria-label={t('translations.assistant_languages')}>
        {targets.map((code) => (
          <button key={code} type="button" role="radio" aria-checked={code === chosen} className="language-card" data-testid={`translations-assistant-${code}`}
            onClick={() => { setChosen(code); }}>
            <span className="language-card-own" lang={code}>{ownName(code)}</span>
            <span className="language-card-in">{titleIn(code, i18n.language)}</span>
            <span className="language-card-meter" aria-hidden="true"><span style={{ width: `${String(coverageOf(coverage, code))}%` }} /></span>
            <span className="language-card-percent">{percent(code)}</span>
          </button>
        ))}
      </div>
      <Button testId="translations-assistant-run" icon={Sparkles} variant="primary" disabled={chosen === null}
        onClick={() => { if (chosen !== null) onTranslate(chosen); }}>
        {chosen === null ? t('translations.assistant_choose') : t('translations.assistant_run', { language: ownName(chosen) })}
      </Button>
    </section>
  );
}

/** The target languages of the workbook as boxes to tick: those already translated and the one shown ticked first (ADR 083). */
export function WorkbookLanguages({ languages, origin, chosen, disabled, onChange }: {
  languages: readonly string[]; origin: string; chosen: readonly string[]; disabled: boolean; onChange: (chosen: readonly string[]) => void;
}) {
  const { t } = useTranslation();
  const toggle = (code: string) => {
    onChange(chosen.includes(code) ? chosen.filter((other) => other !== code) : languages.filter((other) => other === code || chosen.includes(other)));
  };
  return (
    <fieldset className="workbook-languages" disabled={disabled} data-testid="translations-languages">
      <legend className="muted">{t('translations.languages')}</legend>
      {languages.filter((code) => code !== origin).map((code) => (
        <label key={code} className={`language-chip${chosen.includes(code) ? ' is-on' : ''}`}>
          <input type="checkbox" checked={chosen.includes(code)} data-testid={`translations-language-${code}`} onChange={() => { toggle(code); }} />
          <span lang={code}>{ownName(code)}</span>
        </label>
      ))}
    </fieldset>
  );
}
