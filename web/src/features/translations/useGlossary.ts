// useGlossary.ts
// The glossary of the translations in the dialog: read with the project, saved with the version of the project, read again after a conflict (spec 10, B-8.1-45, ADR 065).

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../api/client.ts';
import { isApiError } from '../../api/errors.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';

/** A line of the dialog that says what happened: a success, an information or a failure. */
export interface Status {
  readonly kind: 'info' | 'success' | 'error';
  readonly text: string;
}

export interface Glossary {
  /** The text of the field. */
  readonly text: string;
  readonly setText: (text: string) => void;
  /** The glossary the field means: trimmed as the host keeps it, null when empty. */
  readonly wanted: string | null;
  /** The project is read and nothing is being saved: the field may change. */
  readonly ready: boolean;
  /** The field holds another glossary than the one saved. */
  readonly changed: boolean;
  /** Saves the field when it changed; false when it could not be saved (the reason is given to the status). */
  readonly save: () => Promise<boolean>;
}

interface Saved {
  readonly text: string | null;
  readonly version: number;
}

const savedOf = (detail: Schema<'ProjectDetail'>): Saved => ({ text: detail.translation_glossary, version: detail.version });

/** The glossary of a project; `onStatus` must keep its identity (a setter of React state). */
export function useGlossary(projectId: string, onStatus: (status: Status) => void): Glossary {
  const { t } = useTranslation();
  const [saved, setSaved] = useState<Saved | null>(null);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    let alive = true;
    api.get('/api/projects/{id}', { path: { id: projectId } }).then((detail) => {
      if (!alive) return;
      setSaved(savedOf(detail));
      setText(detail.translation_glossary ?? '');
    }, (failure: unknown) => {
      if (alive) onStatus({ kind: 'error', text: messageOf(failure) });
    });
    return () => {
      alive = false;
    };
  }, [projectId, onStatus]);
  const apply = (detail: Schema<'ProjectDetail'>) => {
    setSaved(savedOf(detail));
    setText(detail.translation_glossary ?? '');
  };
  const wanted = text.trim() === '' ? null : text.trim();
  const changed = saved !== null && wanted !== saved.text;
  const save = async (): Promise<boolean> => {
    if (saved === null || !changed) return true;
    setBusy(true);
    try {
      apply(await api.put('/api/projects/{id}/translation-glossary', { path: { id: projectId }, body: { glossary: wanted }, version: saved.version }));
      onStatus({ kind: 'success', text: t('translations.glossary_saved') });
      return true;
    } catch (failure) {
      if (isApiError(failure) && failure.status === 409) {
        await api.get('/api/projects/{id}', { path: { id: projectId } }).then(apply, () => undefined);
        onStatus({ kind: 'error', text: t('translations.glossary_reloaded') });
      } else onStatus({ kind: 'error', text: messageOf(failure) });
      return false;
    } finally {
      setBusy(false);
    }
  };
  return { text, setText, wanted, ready: saved !== null && !busy, changed, save };
}
