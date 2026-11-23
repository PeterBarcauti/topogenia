// sample.ts
// The creation of the sample project, from the card or from the menu of the starts (ADR 111): the host takes a code that is free, the message says it, the map of the sample opens.

import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client.ts';
import { navigate, paths } from '../../app/router.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { notify } from '../../shared/ui/toasts.ts';

export interface SampleCreation {
  readonly create: () => void;
  readonly sending: boolean;
  /** The refusal of the host, said on the page; the user tries again from the card or the menu. */
  readonly error: string | null;
}

/** Asks the host for the sample (`POST /api/desktop/sample-project`), says its code and opens its map; a refusal is kept to be said. */
export function useSampleCreation(): SampleCreation {
  const { t } = useTranslation();
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Set at once, before the state is drawn again: a second click on the same render finds it.
  const under = useRef(false);
  const create = () => {
    if (under.current) return;
    under.current = true;
    setSending(true);
    setError(null);
    api.post('/api/desktop/sample-project').then((created) => {
      notify(t('home.sample_created', { code: created.code }), 'success');
      navigate(paths.map(created.root_map_id));
    }, (failure: unknown) => {
      under.current = false;
      setError(messageOf(failure));
      setSending(false);
    });
  };
  return { create, sending, error };
}
