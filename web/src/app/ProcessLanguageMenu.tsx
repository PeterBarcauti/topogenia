// ProcessLanguageMenu.tsx
// The language of the process in the header (ADR 083): a button with the language shown, and its list: the language of origin, the languages translated with how far, the others; one chosen shows every map of the project in it.

import { useEffect, useRef, useState, type KeyboardEvent, type RefObject } from 'react';
import { Check, ChevronDown, Globe, Lock, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { nameIn, ownName } from '../shared/i18n/languageNames.ts';
import type { OpenProject } from '../shared/state/openProject.ts';
import { chooseProcessLanguage, coverageOf, processLanguage, shownLanguage, type ProjectLanguages } from '../shared/state/processLanguage.ts';
import { Icon } from '../shared/ui/Icon.tsx';
import { useProfile } from './profile/ProfileProvider.tsx';

/** The languages of the list: the language of origin, then those translated, then the others, each run in the order of the configuration. */
export function languageGroups(configured: readonly string[], languages: ProjectLanguages): { origin: string; translated: string[]; untranslated: string[] } {
  const others = configured.filter((code) => code !== languages.origin);
  return {
    origin: languages.origin,
    translated: others.filter((code) => (languages.written[code] ?? 0) > 0),
    untranslated: others.filter((code) => (languages.written[code] ?? 0) === 0),
  };
}

/** The language shown of the project open, from the language chosen, the languages of the project and the language of the interface. */
export function useShownLanguage(project: { readonly id: string; readonly languages: ProjectLanguages } | null): string | null {
  const { i18n } = useTranslation();
  const chosen = useStore(processLanguage, (state) => (project === null ? undefined : state.chosen[project.id]));
  return project === null ? null : shownLanguage(chosen, project.languages, i18n.language);
}

const OPTION = '[role="option"]';

/** Arrows move among the languages, Home and End go to the ends, Escape closes and gives the focus back to the button. */
function moveIn(event: KeyboardEvent<HTMLDivElement>, close: () => void): void {
  const options = [...event.currentTarget.querySelectorAll<HTMLElement>(OPTION)];
  const index = options.indexOf(document.activeElement as HTMLElement);
  const next = { ArrowDown: index + 1, ArrowUp: index - 1, Home: 0, End: options.length - 1 }[event.key];
  if (event.key === 'Escape') {
    event.preventDefault();
    close();
  } else if (next !== undefined) {
    event.preventDefault();
    options[Math.max(0, Math.min(options.length - 1, next))]?.focus();
  }
}

interface OptionProps {
  readonly code: string;
  readonly shown: string;
  readonly languages: ProjectLanguages;
  readonly onChoose: (code: string) => void;
}

/** A language of the list: its name in itself and in the language of the interface, « origin » or how far it is translated, a tick when shown. */
function LanguageOption({ code, shown, languages, onChoose }: OptionProps) {
  const { t, i18n } = useTranslation();
  const percent = new Intl.NumberFormat(i18n.language, { style: 'percent' }).format(coverageOf(languages, code) / 100);
  return (
    <li role="option" aria-selected={code === shown} tabIndex={-1} className="pl-option" data-testid={`process-language-${code}`}
      onClick={() => { onChoose(code); }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onChoose(code);
        }
      }}>
      <span className="pl-names">
        <span className="pl-own" lang={code}>{ownName(code)}</span>
        <span className="pl-in">{nameIn(code, i18n.language)}</span>
      </span>
      {code === languages.origin ? (
        <span className="pl-origin">{t('process_language.origin')}</span>
      ) : (
        <span className="pl-coverage" title={t('process_language.coverage', { percent })}>
          <span className="pl-meter" aria-hidden="true"><span style={{ width: `${String(coverageOf(languages, code))}%` }} /></span>
          <span className="pl-percent">{percent}</span>
        </span>
      )}
      <span className="pl-check" aria-hidden="true">{code === shown && <Icon icon={Check} size={15} />}</span>
    </li>
  );
}

/** A list open under its button: the language shown takes the focus, a press outside closes it. */
function usePopover(): { open: boolean; setOpen: (open: boolean) => void; box: RefObject<HTMLDivElement | null>; button: RefObject<HTMLButtonElement | null>; close: () => void } {
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (!open) return undefined;
    box.current?.querySelector<HTMLElement>('[aria-selected="true"]')?.focus();
    const away = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || box.current?.contains(event.target) !== true) setOpen(false);
    };
    document.addEventListener('pointerdown', away);
    return () => {
      document.removeEventListener('pointerdown', away);
    };
  }, [open]);
  const close = () => {
    setOpen(false);
    button.current?.focus();
  };
  return { open, setOpen, box, button, close };
}

export function ProcessLanguageMenu({ project }: { project: OpenProject }) {
  const { t } = useTranslation();
  const configured = useProfile((state) => state.profile?.languages);
  const shown = useShownLanguage(project) ?? project.languages.origin;
  const { open, setOpen, box, button, close } = usePopover();
  const choose = (code: string) => {
    chooseProcessLanguage(project.id, code);
    close();
  };
  const groups = languageGroups(configured ?? [project.languages.origin], project.languages);
  const option = (code: string) => <LanguageOption key={code} code={code} shown={shown} languages={project.languages} onChoose={choose} />;
  return (
    <div className="process-language" ref={box}>
      <button ref={button} type="button" className="pl-button" aria-haspopup="listbox" aria-expanded={open} title={t('process_language.help')}
        data-testid="process-language" onClick={() => { setOpen(!open); }}>
        <Icon icon={Globe} size={15} />
        <span className="pl-shown" lang={shown}>{ownName(shown)}</span>
        {shown !== project.languages.origin && <span className="pl-lock"><Icon icon={Lock} size={13} /></span>}
        <Icon icon={ChevronDown} size={14} />
      </button>
      {open && (
        <div className="pl-popover" role="presentation" onKeyDown={(event) => { moveIn(event, close); }}>
          <ul role="listbox" aria-label={t('process_language.label')} data-testid="process-language-list">
            <li role="presentation" className="pl-group">{t('process_language.origin_group')}</li>
            {option(groups.origin)}
            {groups.translated.length > 0 && <li role="presentation" className="pl-group">{t('process_language.translated_group')}</li>}
            {groups.translated.map(option)}
            {groups.untranslated.length > 0 && <li role="presentation" className="pl-group">{t('process_language.untranslated_group')}</li>}
            {groups.untranslated.map(option)}
          </ul>
          {project.openTranslations !== undefined && (
            <button type="button" className="pl-translate" data-testid="process-language-translate"
              onClick={() => {
                setOpen(false);
                project.openTranslations?.();
              }}>
              <Icon icon={Sparkles} size={15} />
              {t('process_language.translate')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
