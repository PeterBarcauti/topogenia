// PlanStatus.tsx
// The status on a line of the panel « Plan d'actions » (ADR 116): its text, with the local day of the change on a line dealt with; its button for who may fill the receiving sheet, out of the view in BPMN 2.0, disabled while it is written; the message of the last change, said in a region there before it, with « Undo », which takes the focus.

import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { formatDate } from '../../../shared/i18n/formatDate.ts';
import { PainPointStatusOptions } from '../../sheet/documents/PainPointStatusOptions.tsx';
import { bpmnView } from '../bpmn/bpmnState.ts';
import { useEditor } from '../state/EditorContext.tsx';
import { chooseStatus, fillable, undoChange } from './planStatus.ts';
import { planStore } from './planStore.ts';
import { rootMapOf } from './usePlan.ts';

type MapView = Schema<'MapView'>;
type ActionPlanLine = Schema<'ActionPlanLine'>;

/**
 * The status of a line, as the sheet of the export writes it; a line dealt with says since when, the day of the change in
 * the local time of the browser, never the hour nor a cut of the universal instant (ADR 116 point 7).
 */
export function PlanStatusText({ line, status }: { line: ActionPlanLine; status: string }) {
  const { t, i18n } = useTranslation();
  const since = !line.open && line.status_changed_at !== null;
  return (
    <span data-testid="plan-status">
      {since ? t('action_plan.status_since', { status, date: formatDate(line.status_changed_at ?? '', i18n.language) }) : status}
    </span>
  );
}

/**
 * The status button of a line the person may fill, the choice written once it is made; none in the view in BPMN 2.0,
 * read only (ADR 106).
 */
export function PlanStatusList({ map, line }: { map: MapView; line: ActionPlanLine }) {
  const editor = useEditor();
  const writing = useStore(planStore, (state) => state.writing[line.pain_point_id]);
  const bpmn = useStore(bpmnView, (state) => state.open);
  if (!fillable(line) || bpmn) return null;
  return (
    <PainPointStatusOptions code={line.code} held={line.status} shown={writing ?? line.status} testId={`plan-status-of-${line.code}`} className="field-control plan-status"
      disabled={writing !== undefined} onChoose={(status) => { void chooseStatus(editor, rootMapOf(map), line, status); }} />
  );
}

/**
 * The last status written from the panel, said until the next reading of the panel, in a live region there before it so
 * that it is announced (review m7), with « Undo », which takes the focus: the button that had it may have left with its line
 * (ADR 116 point 6). The name of « Undo » begins with the word it shows.
 */
export function PlanChangeMessage({ map }: { map: MapView }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const change = useStore(planStore, (state) => state.change);
  const busy = useStore(planStore, (state) => state.change !== null && state.change.painPointId in state.writing);
  const undo = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    undo.current?.focus();
  }, [change]);
  const label = (status: string) => t(`sheet.pain_point.statuses.${status}`);
  return (
    <div className="plan-change-live" aria-live="polite" aria-atomic="true" data-testid="plan-change-live">
      {change !== null && (
        <div className="plan-message plan-change" data-testid="plan-change">
          {t('action_plan.status_changed', { code: change.code, status: label(change.after) })}
          {change.before !== null && (
            <button ref={undo} type="button" className="link-button" data-testid="plan-change-undo" disabled={busy}
              aria-label={t('action_plan.undo_of', { code: change.code, status: label(change.before) })} onClick={() => { void undoChange(editor, rootMapOf(map)); }}>
              {t('action_plan.undo')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
