// NewProjectScreen.tsx
// Creates a project (administrator): code, the language of the process and its name in it, the translations of the name asked for, description, shared folder, classification, members (annex B 1.4, spec 4.1, ADR 083).

import { useState, type SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client.ts';
import { useProfile } from '../../app/profile/ProfileProvider.tsx';
import { navigate, paths } from '../../app/router.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { nameIn, ownName } from '../../shared/i18n/languageNames.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Field } from '../../shared/ui/Field.tsx';
import type { Classification } from './classification.ts';
import { ClassificationField } from './ClassificationField.tsx';
import { PROJECT_CODE, typedCode } from './files/pmapText.ts';
import { bodyOf, projectNames, type MemberLine } from './members.ts';
import { MembersEditor } from './MembersEditor.tsx';

interface NameFieldsProps {
  readonly languages: readonly string[];
  readonly language: string;
  readonly names: Readonly<Record<string, string>>;
  readonly added: readonly string[];
  readonly onLanguage: (language: string) => void;
  readonly onName: (language: string, text: string) => void;
  readonly onAdd: (language: string) => void;
}

/** The language of the process, its name in it, then the translations of the name asked for (ADR 083: not one field per language). */
function ProjectNameFields({ languages, language, names, added, onLanguage, onName, onAdd }: NameFieldsProps) {
  const { t, i18n } = useTranslation();
  const given = [language, ...added.filter((other) => other !== language)];
  const addable = languages.filter((code) => !given.includes(code));
  const label = (code: string) => `${ownName(code)} · ${nameIn(code, i18n.language)}`;
  return (
    <>
        <Field label={t('project.language')} testId="new-project-language" hint={t('project.language_help')}>
          {(control) => (
            <select {...control} className="field-control" value={language} onChange={(event) => { onLanguage(event.target.value); }}>
              {languages.map((code) => <option key={code} value={code} lang={code}>{label(code)}</option>)}
            </select>
          )}
        </Field>
        {given.map((code) => (
          <Field key={code} label={t('project.name_in', { language: nameIn(code, i18n.language) })} testId={`new-project-name-${code}`}>
            {(control) => <input {...control} lang={code} value={names[code] ?? ''} maxLength={200}
              onChange={(event) => { onName(code, event.target.value); }} />}
          </Field>
        ))}
        {addable.length > 0 && (
          <Field label={t('project.add_name')} testId="new-project-add-name" hint={t('project.names_help')}>
            {(control) => (
              <select {...control} className="field-control" value="" onChange={(event) => { if (event.target.value !== '') onAdd(event.target.value); }}>
                <option value="">{t('translations.manual.add_choose')}</option>
                {addable.map((code) => <option key={code} value={code}>{label(code)}</option>)}
              </select>
            )}
          </Field>
        )}
    </>
  );
}

export function NewProjectScreen() {
  const { t, i18n } = useTranslation();
  const profile = useProfile((state) => state.profile);
  const [code, setCode] = useState('');
  const [names, setNames] = useState<Record<string, string>>({});
  const [description, setDescription] = useState('');
  const [folder, setFolder] = useState('');
  const [classification, setClassification] = useState<Classification>('internal');
  const [members, setMembers] = useState<MemberLine[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const languages = profile?.languages ?? [];
  // The language of the process, the interface's by default (ADR 083); its name first, the translations asked for after it.
  const [language, setLanguage] = useState(i18n.language);
  const [added, setAdded] = useState<readonly string[]>([]);
  const given = [language, ...added.filter((other) => other !== language)];
  const named = (names[language] ?? '').trim() === '' ? undefined : projectNames(Object.fromEntries(given.map((code) => [code, names[code] ?? ''])), language, given);

  if (profile?.is_admin !== true) return <div className="page-form"><p className="message-error" role="alert" data-testid="new-project-refused">{t('errors.ACCES_REFUSE')}</p></div>;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (named === undefined) return;
    setSending(true);
    setError(null);
    try {
      const created = await api.post('/api/projects', {
        body: {
          code, name: named.name, source_language: named.sourceLanguage, translations: named.translations,
          description: description.trim() === '' ? null : description, shared_folder: folder.trim() === '' ? null : folder, classification, sharing_group_label: null, members: bodyOf(members),
        },
      });
      navigate(paths.map(created.root_map_id));
    } catch (failure) {
      setError(messageOf(failure));
      setSending(false);
    }
  };

  return (
    <div className="page-form" data-testid="new-project">
      <h1>{t('project.create_title')}</h1>
      <form onSubmit={(event) => void submit(event)} noValidate>
        <Field label={t('project.code')} testId="new-project-code" hint={t('project.code_help')}>
          {(control) => <input {...control} value={code} maxLength={6} required onChange={(event) => { setCode(typedCode(event.target.value)); }} />}
        </Field>
        <ProjectNameFields languages={languages} language={language} names={names} added={added} onLanguage={setLanguage}
          onName={(code, text) => { setNames((state) => ({ ...state, [code]: text })); }} onAdd={(code) => { setAdded([...added, code]); }} />
        <Field label={t('project.description')} testId="new-project-description">
          {(control) => <textarea {...control} value={description} maxLength={4000} rows={3} onChange={(event) => { setDescription(event.target.value); }} />}
        </Field>
        <Field label={t('project.shared_folder')} testId="new-project-folder">
          {(control) => <input {...control} value={folder} maxLength={400} onChange={(event) => { setFolder(event.target.value); }} />}
        </Field>
        <ClassificationField value={classification} testId="new-project-classification" onChange={setClassification} />
        <MembersEditor lines={members} onChange={setMembers} />
        {error !== null && <p className="message-error" role="alert">{error}</p>}
        <div className="form-actions">
          <Button testId="new-project-cancel" variant="secondary" onClick={() => { navigate(paths.home); }}>{t('project.cancel')}</Button>
          <Button testId="new-project-create" type="submit" variant="primary" disabled={sending || !PROJECT_CODE.test(code) || named === undefined}>{t('project.create')}</Button>
        </div>
      </form>
    </div>
  );
}
