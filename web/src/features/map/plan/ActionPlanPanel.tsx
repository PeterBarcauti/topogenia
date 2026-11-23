// ActionPlanPanel.tsx
// The panel « Plan d'actions » docked on the right (ADR 112 point 5): the hours lost by the open pain points, the count of the late ones and of the lines shown, three sorts and three filters, then one line a pain point of the project, which leads to its step on its map (the view in BPMN 2.0 left), and « Edit the plan » and the status list for who may fill the sheet of the step that receives (ADR 112 point 6, ADR 116), the last status written said with « Undo »; Escape closes the panel and gives the focus back to the map.

import { useId, useMemo } from 'react';
import { Pencil, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { formatDay, localDay } from '../../../shared/i18n/formatDate.ts';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { useDock } from '../../../shared/ui/dock.ts';
import { FoldButton } from '../../../shared/ui/FoldButton.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { bpmnView } from '../bpmn/bpmnState.ts';
import { useLeadTo } from '../panels/useLeadTo.ts';
import { focusTheMap } from '../search/SearchPanel.tsx';
import { focusOnArrival } from '../state/arrival.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';
import { filterEntries, isLate, planCounts, planEntries, sortEntries, type PlanEntry, type PlanFilters, type PlanSort } from './actionPlan.ts';
import { askEdit, planStore } from './planStore.ts';
import { PlanChangeMessage, PlanStatusList, PlanStatusText } from './PlanStatus.tsx';
import { closePlan } from './usePlan.ts';

type MapView = Schema<'MapView'>;
type ActionPlanLine = Schema<'ActionPlanLine'>;

/** The sorts offered, each with the key of its name. */
const SORTS: readonly (readonly [PlanSort, string])[] = [['project', 'action_plan.sorts.project'], ['hours', 'action_plan.sorts.lost'], ['due', 'action_plan.sorts.due']];

/** The filters offered: the fact each one keeps, the key of its name, the end of its test identifier. */
const FILTERS: readonly (readonly [keyof PlanFilters, string, string])[] = [
  ['open', 'action_plan.filter_open', 'open'], ['late', 'action_plan.filter_late', 'late'], ['noAction', 'action_plan.filter_no_action', 'no-action'],
];

/**
 * Going to the step where a pain point is raised (where the map shows it): on this map chosen and put in view (F-64); on
 * another map that map opened, which chooses it on arrival, the panel open there; over the view in BPMN 2.0, which cannot
 * put a step in view, the view left and the canvas shown choosing it on arrival.
 */
function useGoTo(map: MapView): (line: ActionPlanLine) => void {
  const editor = useEditor();
  const leadTo = useLeadTo(map);
  return (line) => {
    const subject = { mapId: line.map_id, stepId: line.step_id, laneId: null };
    if (!bpmnView.getState().open) {
      leadTo(subject);
      return;
    }
    focusOnArrival(subject);
    bpmnView.getState().setOpen(false);
    if (subject.mapId !== map.id) editor.navigate(paths.map(subject.mapId));
  };
}

/** Opens the sheet of the step that receives the document and the field of the document: given the step and the document. */
export type EditPlan = (stepId: string, documentId: string) => void;

/**
 * The line may be edited: the person may fill the sheet of the step that receives (the right the host gives, ADR 102
 * revised), whose map is known.
 */
const editable = (line: ActionPlanLine): line is ActionPlanLine & { receiving_step_id: string; receiving_map_id: string; document_id: string } =>
  line.can_fill && line.receiving_step_id !== null && line.receiving_map_id !== null && line.document_id !== null;

/**
 * Editing the plan of a pain point, in the sheet of the step that receives its document, the inputs unfolded and the
 * caret in the field of the document (F-118), on the map of that step, which may not be the map of the pain point (a step
 * moved to another map keeps its documents): on this map at once; on another map that map opened, which opens the sheet
 * once shown (the request kept in the store of the page). The view in BPMN 2.0, where a sheet is read only, is left first.
 */
function useEdit(map: MapView, onEdit: EditPlan): (line: ActionPlanLine) => void {
  const editor = useEditor();
  return (line) => {
    if (!editable(line)) return;
    if (bpmnView.getState().open) bpmnView.getState().setOpen(false);
    if (line.receiving_map_id === map.id) {
      onEdit(line.receiving_step_id, line.document_id);
      return;
    }
    askEdit({ mapId: line.receiving_map_id, stepId: line.receiving_step_id, documentId: line.document_id });
    editor.navigate(paths.map(line.receiving_map_id));
  };
}

/** The head: the hours lost per month by the open pain points (all of them, whatever the filters), the late ones, the lines shown of all. */
function PlanHead({ total, entries, shown, today }: { total: number; entries: readonly PlanEntry[]; shown: number; today: string }) {
  const { t, i18n } = useTranslation();
  const counts = planCounts(entries, today);
  return (
    <div className="plan-head">
      <p className="plan-total" data-testid="plan-total">{t('action_plan.total', { hours: new Intl.NumberFormat(i18n.language).format(total) })}</p>
      <p className="plan-count" role="status">{t('action_plan.late_count', { count: counts.late })} · {t('action_plan.count', { shown, total: counts.total })}</p>
    </div>
  );
}

/** The sort, a list of three choices, and the three filters, buttons pressed or not that narrow together. */
function PlanControls() {
  const { t } = useTranslation();
  const sort = useStore(planStore, (state) => state.sort);
  const filters = useStore(planStore, (state) => state.filters);
  return (
    <div className="plan-controls">
      <label className="plan-sort">
        <span>{t('action_plan.sort')}</span>
        <select className="field-control" value={sort} data-testid="plan-sort" onChange={(event) => { planStore.setState({ sort: event.target.value as PlanSort }); }}>
          {SORTS.map(([value, label]) => <option key={value} value={value}>{t(label)}</option>)}
        </select>
      </label>
      <div className="plan-filters" role="group" aria-label={t('action_plan.filters')}>
        {FILTERS.map(([fact, label, id]) => (
          <button key={fact} type="button" className="plan-filter" aria-pressed={filters[fact]} data-testid={`plan-filter-${id}`}
            onClick={() => { planStore.setState({ filters: { ...filters, [fact]: !filters[fact] } }); }}>
            {t(label)}
          </button>
        ))}
      </div>
    </div>
  );
}

/**
 * A line on three rows, a button that leads to its step: the pain point; the step, its map and its lane; the action,
 * the owner, the due date and its late mark, the status, the hours. The names and the status are the cells of the sheet.
 * Out of the button: the status list and « Edit the plan », for who may fill the sheet of the step that receives.
 */
function PlanLine({ map, entry, late, onGo, onEdit }: { map: MapView; entry: PlanEntry; late: boolean; onGo: (line: ActionPlanLine) => void; onEdit: (line: ActionPlanLine) => void }) {
  const { t, i18n } = useTranslation();
  const text = useId();
  const { line } = entry;
  const [mapName, , step, lane, , , , , , status] = entry.cells.map(String);
  return (
    <li className="plan-line" data-testid={`plan-line-${line.code}`}>
      <button type="button" className="plan-go" aria-label={t('action_plan.go_to', { code: line.step_code })} aria-describedby={text} data-testid={`plan-go-${line.code}`}
        onClick={() => { onGo(line); }}>
        <span id={text} className="plan-line-text">
          <span className="plan-line-head"><code>{line.code}</code> {line.description}</span>
          <span className="plan-line-place">
            <span><code>{line.step_code}</code> {step}</span>
            <span>{t('action_plan.map', { name: mapName })}</span>
            {lane !== '' && <span>{t('action_plan.lane', { name: lane })}</span>}
          </span>
          <span className="plan-line-plan">
            <span>{entry.noAction ? t('action_plan.no_action') : t('sheet.pain_point.plan_action', { action: line.action })}</span>
            {(line.owner ?? '') !== '' && <span>{t('sheet.pain_point.plan_owner', { owner: line.owner })}</span>}
            {line.due_date !== null && (
              <span>
                {t('sheet.pain_point.plan_due', { date: formatDay(line.due_date, i18n.language) })}
                {late && <span className="plan-late" data-testid="plan-late">{t('action_plan.late')}</span>}
              </span>
            )}
            <PlanStatusText line={line} status={status ?? ''} />
            {line.lost_hours_per_month !== null && <span>{t('sheet.pain_point.lost_short', { hours: new Intl.NumberFormat(i18n.language).format(line.lost_hours_per_month) })}</span>}
          </span>
        </span>
      </button>
      <PlanStatusList map={map} line={line} />
      {editable(line) && (
        <button type="button" className="link-button plan-edit" aria-label={t('action_plan.edit_of', { code: line.code })} data-testid={`plan-edit-${line.code}`}
          onClick={() => { onEdit(line); }}>
          <Icon icon={Pencil} size={12} />{t('action_plan.edit')}
        </button>
      )}
    </li>
  );
}

/** What the panel says instead of lines: a refusal, the reading, a project without pain point, no line left by the filters. */
function PlanMessage({ count }: { count: number }) {
  const { t } = useTranslation();
  const reply = useStore(planStore, (state) => state.reply);
  const status = useStore(planStore, (state) => state.status);
  const error = useStore(planStore, (state) => state.error);
  if (status === 'error') return <p className="plan-message is-error" role="alert">{messageOf(error)}</p>;
  if (reply === null) return <p className="plan-message">{t('action_plan.loading')}</p>;
  if (reply.lines.length === 0) return <p className="plan-message">{t('action_plan.empty')}</p>;
  return count === 0 ? <p className="plan-message">{t('action_plan.none')}</p> : null;
}

export function ActionPlanPanel({ map, onEdit }: { map: MapView; onEdit: EditPlan }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const language = useMapLanguage();
  const reply = useStore(planStore, (state) => state.reply);
  const failed = useStore(planStore, (state) => state.status === 'error');
  const sort = useStore(planStore, (state) => state.sort);
  const filters = useStore(planStore, (state) => state.filters);
  // The status is in the language of the interface: `t` changes with it, and the entries are built again.
  const entries = useMemo(() => (reply === null ? [] : planEntries(reply, language, t)), [reply, language, t]);
  // The day of the browser, read at each drawing: a panel open at midnight is right again at the next gesture.
  const today = localDay();
  const shown = sortEntries(filterEntries(entries, filters, today), sort);
  const folded = useDock()?.folded === true;
  const goTo = useGoTo(map);
  const edit = useEdit(map, onEdit);
  const close = () => {
    focusTheMap(editor);
    closePlan();
  };
  useKeyLayer('panel', (event, chord) => {
    if (event.type !== 'keydown' || chord !== 'Escape' || document.activeElement?.closest('[data-testid="plan-panel"]') == null) return false;
    close();
    return true;
  }, !folded);
  const title = t('action_plan.title');
  const listed = !failed && reply !== null && reply.lines.length > 0;
  return (
    <aside className="side-panel plan-panel" aria-label={title} data-testid="plan-panel">
      <header className="side-panel-head">
        <div className="stream-panel-title"><h2>{title}</h2></div>
        <FoldButton />
        <button type="button" className="icon-button" aria-label={t('app.close')} title={t('app.close')} data-testid="plan-close" onClick={close}>
          <Icon icon={X} size={16} />
        </button>
      </header>
      <div className="side-panel-body">
        {listed && <PlanHead total={reply.open_lost_hours_per_month} entries={entries} shown={shown.length} today={today} />}
        {listed && <PlanControls />}
        {listed && <PlanChangeMessage map={map} />}
        <PlanMessage count={shown.length} />
        {listed && shown.length > 0 && (
          <ul className="plan-lines">
            {shown.map((entry) => <PlanLine key={entry.line.pain_point_id} map={map} entry={entry} late={isLate(entry, today)} onGo={goTo} onEdit={edit} />)}
          </ul>
        )}
      </div>
    </aside>
  );
}
