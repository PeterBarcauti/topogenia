// Participants.tsx
// « Who takes part » in the project put forward: initials, name, Sponsor (B-1.3-24 to 27).

import { UsersRound } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../api/client.ts';
import { initialsOf } from '../../shared/initials.ts';
import { Icon } from '../../shared/ui/Icon.tsx';

export function Participants({ participants, projectName }: { participants: readonly Schema<'MemberView'>[]; projectName: string | undefined }) {
  const { t } = useTranslation();
  return (
    <aside className="people" aria-labelledby="participants-title" data-testid="participants">
      <h2 id="participants-title" className="people-head">
        <Icon icon={UsersRound} size={14} />
        {t('home.participants')}
        {projectName !== undefined && <span className="people-project">{projectName}</span>}
      </h2>
      <ul className="people-list">
        {participants.map((member) => (
          <li key={member.user_id} className="person">
            <span className="initials" aria-hidden="true">{initialsOf(member.display_name)}</span>
            <span className="who"><b>{member.display_name}</b><small>{t(`roles.${member.role}`)}</small></span>
            {member.is_sponsor && <span className="tag tag--sponsor">{t('home.sponsor')}</span>}
          </li>
        ))}
      </ul>
    </aside>
  );
}
