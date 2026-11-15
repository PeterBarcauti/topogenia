// actionPlanSheet.ts
// « Plan d'actions »: the sheet of the workbook that lists the pain points of the map and of the maps below it with their action, owner, due date and status (Makigami M7, plan 3.1).

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import type { Sheet } from './workbook.ts';

type ActionPlanView = Schema<'ActionPlanView'>;
type NamedText = Schema<'NamedText'>;

/** A name of the plan in the language shown, else as it was entered. */
const shown = (named: NamedText | null | undefined, language: string): string =>
  named == null ? '' : localizedName({ name: named.text, source_language: named.source_language, name_translations: named.translations }, language);

/**
 * The action plan as a sheet, in the order of the host (the map, then the levels below it, the reading order, the codes):
 * map, step code and label, lane, pain point code and description, action, owner, due date (`yyyy-mm-dd`, which Excel
 * sorts), status, hours lost per month; null when the plan holds no pain point, so that the workbook has no empty sheet.
 */
export function actionPlanSheet(plan: ActionPlanView, language: string, t: Translate): Sheet | null {
  if (plan.lines.length === 0) return null;
  return {
    name: t('export.sheet_action_plan'),
    headers: [t('export.plan.map'), t('raci.column_code'), t('raci.column_step'), t('export.stream.lane'), t('export.plan.pain_point'),
      t('export.plan.description'), t('export.plan.action'), t('export.plan.owner'), t('export.plan.due'), t('export.plan.status'), t('export.plan.lost')],
    rows: plan.lines.map((line) => [
      shown(line.map, language), line.step_code, shown(line.step, language), shown(line.lane, language), line.code, line.description,
      line.action ?? '', line.owner ?? '', line.due_date ?? '', t(`sheet.pain_point.statuses.${line.status}`), line.lost_hours_per_month ?? '',
    ]),
    widths: [28, 12, 36, 22, 12, 40, 40, 22, 12, 14, 12],
  };
}
