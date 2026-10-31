// SampleOffer.tsx
// The sample project offered by the home page of the desktop mode while the user has no project: created on demand by the host, never imposed (spec 6.1).

import { useId, useState } from 'react';
import { Plus, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';
import { notify } from '../../shared/ui/toasts.ts';

/**
 * A card in the place of the list: what the sample shows, and its button. The host imports the fictitious package of
 * its samples (`POST /api/desktop/sample-project`); the list is read again, where the project then is like any other.
 * A refusal (its code taken, or in the trash) is said under the card. Ignoring the offer leaves the home page as it is.
 */
export function SampleOffer({ onCreated }: { onCreated: () => void }) {
  const { t } = useTranslation();
  const titleId = useId();
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const create = async () => {
    setSending(true);
    setError(null);
    try {
      const created = await api.post('/api/desktop/sample-project');
      notify(t('home.sample_created', { code: created.code }), 'success');
      onCreated();
    } catch (failure) {
      setError(messageOf(failure));
      setSending(false);
    }
  };
  return (
    <>
      <section className="project" aria-labelledby={titleId} data-testid="home-sample">
        <div className="project-title">
          <Icon icon={Sparkles} size={15} />
          <h3 id={titleId}>{t('home.sample_title')}</h3>
        </div>
        <p>{t('home.sample_text')}</p>
        <div className="project-actions">
          <Button testId="home-sample-create" icon={Plus} variant="secondary" disabled={sending} onClick={() => void create()}>{t('home.sample_create')}</Button>
        </div>
      </section>
      {error !== null && <p className="message-error" role="alert" data-testid="home-sample-error">{error}</p>}
    </>
  );
}
