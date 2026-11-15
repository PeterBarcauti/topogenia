// OriginLanguage.tsx
// The language of origin of a project in the dialog of the translations (ADR 100): a language into which every text is translated may become the language of origin; a language that lacks some says how many, the comments of presentation among them, or that the count is unknown; the change is confirmed, then sent with the version of the project.

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { ownName, titleIn } from '../../shared/i18n/languageNames.ts';
import type { ProjectLanguages } from '../../shared/state/processLanguage.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Confirm } from '../../shared/ui/Confirm.tsx';
import { commentsIn } from './model/workbook.ts';
import type { Status } from './useGlossary.ts';

/** The texts of the project that are not written in a language yet: none, and it may become the language of origin. */
export function missingIn(coverage: ProjectLanguages, language: string): number {
  return Math.max(0, coverage.total - (coverage.written[language] ?? 0));
}

/** The languages the project is translated into: those that may be asked as its language of origin, in the order of the configuration. */
export function translatedOf(languages: readonly string[], coverage: ProjectLanguages): string[] {
  return languages.filter((code) => code !== coverage.origin && (coverage.written[code] ?? 0) > 0);
}

/**
 * The comments of presentation that lack their line in each language given (ADR 099 revised), those of the steps and
 * those of the flows together (lot J10): texts of the project like the others, kept beside the map, so the host is asked for them as the workbook lists them (scope `missing`). A
 * language is absent while its count is read, null when it could not be (audit 3.2): either way it is not offered.
 * Read again at each `round`.
 */
export function useCommentGaps(projectId: string, languages: readonly string[], round: number): Readonly<Record<string, number | null>> {
  const asked = languages.join(',');
  // What was read is kept with what it was read for: a count of another round, or of other languages, is not shown.
  const key = `${projectId}|${asked}|${String(round)}`;
  const [read, setRead] = useState<{ readonly key: string; readonly gaps: Readonly<Record<string, number | null>> }>({ key, gaps: {} });
  useEffect(() => {
    const control = new AbortController();
    // A count read, or null for a reading that failed; nothing once the reading is given up.
    const keep = (code: string, gap: number | null) => {
      if (control.signal.aborted) return;
      setRead((current) => ({ key, gaps: { ...(current.key === key ? current.gaps : {}), [code]: gap } }));
    };
    for (const code of asked === '' ? [] : asked.split(',')) {
      api.get('/api/projects/{id}/translations', { path: { id: projectId }, query: { scope: 'missing', languages: code }, signal: control.signal })
        .then((list) => { keep(code, commentsIn(list)); }, () => { keep(code, null); });
    }
    return () => {
      control.abort();
    };
  }, [projectId, asked, key]);
  return read.key === key ? read.gaps : {};
}

export interface OriginChange {
  readonly busy: boolean;
  /** Makes a language the language of origin of the project, with the version of the project read just before; false when it is refused (the reason is given to the status). */
  readonly change: (language: string) => Promise<boolean>;
}

/**
 * The change of the language of origin of a project; `onStatus` says a refusal, `onChanged` what follows a success,
 * `onRefused` what follows a refusal (what lacks is read again: the count shown was no longer the one of the host).
 */
export function useOriginChange(projectId: string, onStatus: (status: Status) => void, onChanged: (language: string) => void, onRefused: () => void): OriginChange {
  const [busy, setBusy] = useState(false);
  const change = async (language: string): Promise<boolean> => {
    setBusy(true);
    try {
      const detail = await api.get('/api/projects/{id}', { path: { id: projectId } });
      await api.put('/api/projects/{id}/origin-language', { path: { id: projectId }, body: { language }, version: detail.version });
      onChanged(language);
      return true;
    } catch (failure) {
      onStatus({ kind: 'error', text: messageOf(failure) });
      onRefused();
      return false;
    } finally {
      setBusy(false);
    }
  };
  return { busy, change };
}

/**
 * What a language lacks to become the language of origin: its count once known, the comments of presentation among it
 * (the coverage of the map does not count them, ADR 100 point 6), unknown when they could not be read; the button when
 * it lacks nothing.
 */
function LanguageState({ code, texts, comments, onMake }: { code: string; texts: number; comments: number | null | undefined; onMake: (language: string) => void }) {
  const { t } = useTranslation();
  if (comments === null) return <span className="muted">{t('translations.origin_unknown')}</span>;
  if (comments === undefined) return <span className="muted">{texts === 0 ? t('app.loading') : t('translations.origin_missing', { number: texts })}</span>;
  if (comments > 0) return <span className="muted">{t('translations.origin_missing_comments', { number: texts + comments, count: comments })}</span>;
  if (texts > 0) return <span className="muted">{t('translations.origin_missing', { number: texts })}</span>;
  return <Button testId={`translations-origin-make-${code}`} variant="secondary" onClick={() => { onMake(code); }}>{t('translations.origin_make')}</Button>;
}

/**
 * The languages the project is translated into, each with what it lacks to become the language of origin (the texts of
 * the map and the comments of presentation, `gaps`), or the button that makes it so; nothing while no language has a
 * translation.
 */
export function OriginLanguageChoice({ languages, coverage, gaps, onMake }: {
  languages: readonly string[]; coverage: ProjectLanguages; gaps: Readonly<Record<string, number | null>>; onMake: (language: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const translated = translatedOf(languages, coverage);
  if (translated.length === 0) return null;
  return (
    <section className="translation-step" aria-labelledby="translations-origin" data-testid="translations-origin">
      <h3 id="translations-origin" className="dialog-subtitle">{t('translations.origin_title', { language: titleIn(coverage.origin, i18n.language) })}</h3>
      <ul className="origin-languages">
        {translated.map((code) => (
          <li key={code} data-testid={`translations-origin-${code}`}>
            <span lang={code}>{ownName(code)}</span>
            <LanguageState code={code} texts={missingIn(coverage, code)} comments={gaps[code]} onMake={onMake} />
          </li>
        ))}
      </ul>
    </section>
  );
}

/** The question asked before the change: what becomes of the texts of both languages, the undo history of the project emptied (ADR 100 point 7), and how to go back. */
export function OriginLanguageConfirm({ language, origin, busy, onConfirm, onCancel }: {
  language: string; origin: string; busy: boolean; onConfirm: () => void; onCancel: () => void;
}) {
  const { t, i18n } = useTranslation();
  return (
    <Confirm title={t('translations.origin_confirm_title')} testId="translations-origin-confirm" confirmLabel={t('translations.origin_confirm')} ready={!busy}
      onConfirm={onConfirm} onCancel={onCancel}>
      <p>{t('translations.origin_confirm_text', { language: titleIn(language, i18n.language), origin: titleIn(origin, i18n.language) })}</p>
    </Confirm>
  );
}
