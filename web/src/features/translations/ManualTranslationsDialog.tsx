// ManualTranslationsDialog.tsx
// The manual translations of one name from the interface (B-5-28, spec 10: the 1.x had them only through Excel): the text entered, one field per language of the project and « Add a language » for the others (ADR 083), only the languages changed sent.

import { useId, useState, type SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ownName } from '../../shared/i18n/languageNames.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Dialog } from '../../shared/ui/Dialog.tsx';
import { Field } from '../../shared/ui/Field.tsx';

/** A name to translate: its text, its language of origin, its translations and the length of the field (A-2.14-22). */
export interface TranslatedName {
  readonly name: string;
  readonly sourceLanguage: string;
  readonly translations: Readonly<Record<string, string>>;
  readonly max: number;
}

/**
 * The languages whose field changed, trimmed: a text writes a manual translation, an empty text deletes the translation
 * (A-2.14-04); a field left as it was is not sent, so that an automatic translation stays automatic.
 */
export function changedTranslations(before: Readonly<Record<string, string>>, fields: Readonly<Record<string, string>>): Record<string, string> {
  return Object.fromEntries(Object.entries(fields)
    .map(([language, text]) => [language, text.trim()] as const)
    .filter(([language, text]) => text !== (before[language] ?? '').trim()));
}

export interface ManualTranslationsDialogProps {
  readonly named: TranslatedName;
  readonly languages: readonly string[];
  /**
   * The languages given a field at once (ADR 083: the language shown and those the project is translated into), besides
   * those the name is already translated into; the others come by « Add a language ». None: every language.
   */
  readonly preferred?: readonly string[] | undefined;
  /** Sends the changes; true when they are saved, the dialog then closes. */
  readonly onSave: (changes: Record<string, string>) => Promise<boolean>;
  readonly onClose: () => void;
}

export function ManualTranslationsDialog({ named, languages, preferred, onSave, onClose }: ManualTranslationsDialogProps) {
  const { t } = useTranslation();
  const formId = useId();
  const targets = languages.filter((language) => language !== named.sourceLanguage);
  const [added, setAdded] = useState<readonly string[]>([]);
  const given = preferred === undefined
    ? targets
    : targets.filter((language) => preferred.includes(language) || named.translations[language] !== undefined || added.includes(language));
  const addable = targets.filter((language) => !given.includes(language));
  const [fields, setFields] = useState<Record<string, string>>(() => Object.fromEntries(targets.map((language) => [language, named.translations[language] ?? ''])));
  const [saving, setSaving] = useState(false);
  const changes = changedTranslations(named.translations, fields);
  const changed = Object.keys(changes).length > 0;
  // The button that submits is disabled without a change and while saving: the browser then submits nothing, Enter included.
  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setSaving(true);
    if (await onSave(changes)) onClose();
    else setSaving(false);
  };
  const footer = (
    <>
      <Button testId="manual-translations-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="manual-translations-save" variant="primary" type="submit" form={formId} disabled={!changed || saving}>{t('translations.manual.save')}</Button>
    </>
  );
  return (
    <Dialog title={t('translations.manual.title', { name: named.name })} testId="manual-translations" onClose={onClose} footer={footer}>
      <form id={formId} className="manual-translations" onSubmit={(event) => void submit(event)}>
        <p className="muted">{t('translations.manual.help')}</p>
        <div className="field">
          <span className="field-label">{t('translations.manual.original', { language: ownName(named.sourceLanguage) })}</span>
          <p className="manual-original" lang={named.sourceLanguage} data-testid="manual-translations-original">{named.name}</p>
        </div>
        {given.map((language) => (
          <Field key={language} label={t('translations.manual.field', { language: ownName(language) })} testId={`manual-translations-${language}`}>
            {(control) => (
              <input {...control} lang={language} value={fields[language] ?? ''} maxLength={named.max} disabled={saving}
                onChange={(event) => { setFields((before) => ({ ...before, [language]: event.target.value })); }} />
            )}
          </Field>
        ))}
        {addable.length > 0 && (
          <label className="manual-add">
            <span className="field-label">{t('translations.manual.add')}</span>
            <select className="field-control" value="" disabled={saving} data-testid="manual-translations-add"
              onChange={(event) => { if (event.target.value !== '') setAdded([...added, event.target.value]); }}>
              <option value="">{t('translations.manual.add_choose')}</option>
              {addable.map((language) => <option key={language} value={language}>{ownName(language)}</option>)}
            </select>
          </label>
        )}
      </form>
    </Dialog>
  );
}
