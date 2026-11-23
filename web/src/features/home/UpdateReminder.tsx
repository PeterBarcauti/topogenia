// UpdateReminder.tsx
// The discreet line of the home page for a version 90 days old (ADR 111): its day, a link to the page of the versions, a cross to close it for 90 days; the application asks nothing of the network, the browser opens the page.

import { useState } from 'react';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../app/profile/ProfileProvider.tsx';
import { openUpdate } from '../../app/siteLinks.ts';
import { formatDay } from '../../shared/i18n/formatDate.ts';
import { Icon } from '../../shared/ui/Icon.tsx';
import { readReminderClosed, writeReminderClosed } from '../map/state/preferences.ts';
import { dayKey, reminderDue } from './updateReminder.ts';

export function UpdateReminder() {
  const { t, i18n } = useTranslation();
  const profile = useProfile((state) => state.profile);
  // The day the reminder was closed, read once; a closing in this visit hides the line whatever the storage did.
  const [closedDay, setClosedDay] = useState<string | null>(() => readReminderClosed());
  const due = profile !== null && reminderDue({
    releaseDay: __APP_RELEASE_DATE__, now: new Date(), closedDay, isAdmin: profile.is_admin, store: profile.store === true, hasLinks: profile.links != null,
  });
  if (!due) return null;
  return (
    <p className="update-reminder" data-testid="update-reminder">
      <span>{t('home.update_line', { date: formatDay(__APP_RELEASE_DATE__, i18n.language) })}</span>
      <button type="button" className="link-button" data-testid="update-reminder-open" onClick={() => {
        openUpdate(profile, i18n.language);
      }}>{t('home.update_link')}</button>
      <button type="button" className="icon-button" data-testid="update-reminder-close" aria-label={t('home.update_close')} title={t('home.update_close')} onClick={() => {
        const today = dayKey(new Date());
        setClosedDay(today);
        writeReminderClosed(today);
      }}><Icon icon={X} size={14} /></button>
    </p>
  );
}
