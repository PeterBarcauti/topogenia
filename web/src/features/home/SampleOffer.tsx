// SampleOffer.tsx
// The sample project offered by the home page of the desktop mode while the user has no project: a card with its button; the menu of the starts offers it too, once projects exist (spec 6.1, ADR 111).

import { useId } from 'react';
import { Plus, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../shared/ui/Button.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';

/**
 * A card in the place of the list: what the sample shows, and its button. The creation (the host's, under a code that is
 * free) is the page's, which opens the map of the sample and says a refusal under the card. Ignoring the offer leaves the
 * home page as it is.
 */
export function SampleOffer({ sending, onCreate }: { sending: boolean; onCreate: () => void }) {
  const { t } = useTranslation();
  const titleId = useId();
  return (
    <section className="project" aria-labelledby={titleId} data-testid="home-sample">
      <div className="project-title">
        <Icon icon={Sparkles} size={15} />
        <h3 id={titleId}>{t('home.sample_title')}</h3>
      </div>
      <p>{t('home.sample_text')}</p>
      <div className="project-actions">
        <Button testId="home-sample-create" icon={Plus} variant="secondary" disabled={sending} onClick={onCreate}>{t('home.sample_create')}</Button>
      </div>
    </section>
  );
}
