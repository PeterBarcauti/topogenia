// Responsible.tsx
// The person responsible for the sheet: chosen by the facilitator among the members who are not readers, read by the others (annex B 4).

import { useTranslation } from 'react-i18next';
import { useSheet } from './SheetContext.tsx';
import { assignSheet } from './state/sheetActions.ts';

/**
 * With the right to assign: the members who are not readers, and the empty choice « Personne en particulier », or the
 * person of the lane who answers when nobody is chosen (B-4-13 to B-4-15). Without it: « Responsable : nom » (B-4-16).
 */
export function Responsible() {
  const { t } = useTranslation();
  const sheet = useSheet();
  const { view, props, nameOf } = sheet;
  if (!view.permissions.assign) {
    return view.responsible === null ? null
      : <p className="sheet-responsible-read" data-testid="sheet-responsible">{t('sheet.responsible_read', { name: nameOf(view.responsible.user_id) })}</p>;
  }
  const members = (props.project?.members ?? []).filter((member) => member.role !== 'reader');
  const nobody = view.responsible?.origin === 'lane' ? t('sheet.responsible_lane', { name: nameOf(view.responsible.user_id) }) : t('sheet.responsible_none');
  return (
    <div className="field">
      <label htmlFor="sheet-responsible" className="field-label">{t('sheet.responsible')}</label>
      <select id="sheet-responsible" className="field-control" value={props.step.assigneeUserId ?? ''} data-testid="sheet-responsible"
        onChange={(event) => void assignSheet(sheet, event.target.value === '' ? null : event.target.value)}>
        <option value="">{nobody}</option>
        {members.map((member) => <option key={member.user_id} value={member.user_id}>{member.display_name}</option>)}
      </select>
    </div>
  );
}
