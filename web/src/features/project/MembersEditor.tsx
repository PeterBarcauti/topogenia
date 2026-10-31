// MembersEditor.tsx
// The editor of the members of a project: login, role, Sponsor, Remove per line, and « Add a person » (B-1.4-09 to 14, B-1.5-01).

import { Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../shared/ui/Button.tsx';
import { ROLES, newMember, type MemberLine, type MemberRole } from './members.ts';

export function MembersEditor({ lines, onChange }: { lines: readonly MemberLine[]; onChange: (lines: MemberLine[]) => void }) {
  const { t } = useTranslation();
  const change = (index: number, fields: Partial<MemberLine>) => {
    onChange(lines.map((line, at) => (at === index ? { ...line, ...fields } : line)));
  };
  return (
    <fieldset className="members" data-testid="members-editor">
      <legend>{t('project.members')}</legend>
      {lines.map((line, index) => (
        <div className="member-line" key={index} data-testid={`member-${index}`}>
          <input className="field-control" aria-label={t('project.login')} placeholder={t('project.login')} value={line.login} data-testid={`member-${index}-login`}
            onChange={(event) => { change(index, { login: event.target.value }); }} />
          <select className="field-control" aria-label={t('project.role')} value={line.role} data-testid={`member-${index}-role`}
            onChange={(event) => { change(index, { role: event.target.value as MemberRole }); }}>
            {ROLES.map((role) => <option key={role} value={role}>{t(`roles.${role}`)}</option>)}
          </select>
          <label className="check">
            <input type="checkbox" checked={line.isSponsor} data-testid={`member-${index}-sponsor`} onChange={(event) => { change(index, { isSponsor: event.target.checked }); }} />
            <span>{t('project.sponsor')}</span>
          </label>
          <Button testId={`member-${index}-remove`} icon={X} onClick={() => { onChange(lines.filter((_, at) => at !== index)); }}>{t('project.remove')}</Button>
        </div>
      ))}
      <Button testId="member-add" icon={Plus} variant="secondary" onClick={() => { onChange([...lines, newMember()]); }}>{t('project.add_member')}</Button>
    </fieldset>
  );
}
