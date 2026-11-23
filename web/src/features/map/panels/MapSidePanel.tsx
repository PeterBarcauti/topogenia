// MapSidePanel.tsx
// The panel docked on the right of the canvas, one at a time, in its frame that resizes and folds: the remainder of the open frame, the sheet of the step open (a new one for each step), the search in the project or its action plan, the assistant, the value stream or the analysis with its layer on (spec 9.5, annex B 2.9, B 4, spec 3.0 sections 3.1 and 15.2, plan 3.1, ADR 110, ADR 112).

import { useCallback, useEffect, useState } from 'react';
import { ClipboardList, FileText, Search, Sparkles, SquareDashed } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { AssistantPanel } from '../../assistant/panel/AssistantPanel.tsx';
import { ReportTargets } from '../../sheet/SheetContext.tsx';
import { ANALYSIS_ICON, VALUE_STREAM_ICON } from '../chrome/shapeIcons.ts';
import { hasContent } from '../model/stepFacts.ts';
import { ActionPlanPanel } from '../plan/ActionPlanPanel.tsx';
import { peekEdit, planStore, takeEdit } from '../plan/planStore.ts';
import { leaveOtherPlan, planShownIn } from '../plan/usePlan.ts';
import { SearchPanel } from '../search/SearchPanel.tsx';
import { searchStore } from '../search/searchStore.ts';
import { leaveOtherProject } from '../search/useSearch.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import type { MapEditor } from '../state/editor.ts';
import { AnalysisPanel } from './AnalysisPanel.tsx';
import { RemainderPanel } from './RemainderPanel.tsx';
import { SheetDock, useSheetProject } from './SheetDock.tsx';
import { SideDock } from './SideDock.tsx';
import { StreamPanel } from './StreamPanel.tsx';

/**
 * A pain point asked from the outputs of a producer (F-118): the sheet of the step that receives opens, its inputs
 * unfolded for this opening (`SheetDock`), and the field of the document takes the caret; kept here, above the sheet
 * that changes. The plan of a pain point to edit, asked from a line of the action plan on another map (ADR 112 point 6),
 * is asked the same way by that map once shown, and forgotten by any other map shown first.
 */
function usePainReport(editor: MapEditor, mapId: string | null, openSheet: string | null) {
  const [pain, setPain] = useState<{ readonly stepId: string; readonly documentId: string } | null>(null);
  // Asked for one opening: once the sheet asked is closed, or left for another one, the request goes with it, taken or
  // not, so that no sheet opened later takes the caret (R4-10); adjusted while drawing, as the open sheet changes.
  const [shown, setShown] = useState(openSheet);
  if (shown !== openSheet) {
    setShown(openSheet);
    if (pain !== null && pain.stepId !== openSheet) setPain(null);
  }
  // The plan to edit for this map is read while drawing; the effect takes it from the store and opens its sheet.
  const [arrived, setArrived] = useState<string | null>(null);
  if (arrived !== mapId) {
    setArrived(mapId);
    const asked = mapId === null ? null : peekEdit(mapId);
    if (asked !== null) setPain({ stepId: asked.stepId, documentId: asked.documentId });
  }
  useEffect(() => {
    if (mapId === null) return;
    const asked = takeEdit(mapId);
    if (asked !== null) editor.store.setState({ openSheet: asked.stepId });
  }, [editor, mapId]);
  const report = useCallback((stepId: string, documentId: string) => {
    setPain({ stepId, documentId });
    editor.store.setState({ openSheet: stepId });
  }, [editor]);
  const focused = useCallback(() => {
    setPain(null);
  }, []);
  return { pain, report, focused };
}

export function MapSidePanel() {
  const { t } = useTranslation();
  const editor = useEditor();
  const remainder = useStore(editor.floating, (state) => state.floating?.kind === 'remainder');
  const map = useMapState((state) => state.map);
  const openSheet = useMapState((state) => state.openSheet);
  const project = useSheetProject(map?.project_id ?? '', openSheet !== null);
  const assistant = useStore(editor.assistant, (state) => state.open);
  const stream = useStore(editor.preferences, (state) => state.showValueStream);
  const analysis = useStore(editor.preferences, (state) => state.showAnalysis);
  const projectId = map?.project_id ?? null;
  // The search and the plan of the page are those of this project only: another project opened empties them.
  const search = useStore(searchStore, (state) => state.open && state.projectId === projectId);
  const plan = useStore(planStore, (state) => planShownIn(state, projectId));
  useEffect(() => {
    if (projectId === null) return;
    leaveOtherProject(projectId);
    leaveOtherPlan(projectId);
  }, [projectId]);
  const close = useCallback(() => {
    editor.floating.getState().close();
  }, [editor]);
  const { pain, report, focused } = usePainReport(editor, map?.id ?? null, openSheet);
  // No way to the sheet of a step that holds none, an event or a gateway that joins, whatever it used before its type
  // changed: the map opens no sheet for it (R4-11); nor to a sheet the person may not fill, where the pain point could
  // not be written (ADR 102 revised, audit 3.2): the right the map gives with each step.
  const mayReport = useCallback((stepId: string) => {
    const step = map?.steps.find((candidate) => candidate.id === stepId);
    return step !== undefined && hasContent(step.type) && step.sheet.can_fill;
  }, [map]);
  if (map === null) return null;
  if (remainder && map.dissolution !== null) {
    return (
      <SideDock key="remainder" panel="remainder" title={t('frame.title', { code: map.dissolution.step_code, label: map.dissolution.step_label })} icon={SquareDashed}>
        <RemainderPanel map={map} onClose={close} />
      </SideDock>
    );
  }
  if (openSheet !== null) {
    const code = map.steps.find((step) => step.id === openSheet)?.code ?? '';
    // A new key for each step: the panel starts again, what the previous step left is sent as it closes (B-4-06, B-4-45).
    return (
      <SideDock key="sheet" panel="sheet" title={t('sheet.title', { code })} icon={FileText}>
        <ReportTargets.Provider value={mayReport}>
          <SheetDock key={openSheet} map={map} stepId={openSheet} project={project}
            report={{ focus: pain?.stepId === openSheet ? pain.documentId : null, onReport: report, onFocused: focused }} />
        </ReportTargets.Provider>
      </SideDock>
    );
  }
  if (search) return <SideDock key="search" panel="search" title={t('search.title')} icon={Search}><SearchPanel map={map} /></SideDock>;
  if (plan) return <SideDock key="plan" panel="plan" title={t('action_plan.title')} icon={ClipboardList}><ActionPlanPanel map={map} onEdit={report} /></SideDock>;
  if (assistant) return <SideDock key="assistant" panel="assistant" title={t('assistant.panel.name')} icon={Sparkles}><AssistantPanel /></SideDock>;
  if (analysis) return <SideDock key="analysis" panel="analysis" title={t('ribbon.analysis')} icon={ANALYSIS_ICON}><AnalysisPanel map={map} /></SideDock>;
  if (!stream) return null;
  const warnings = map.value_stream.issues.filter((issue) => issue.severity === 'warning').length;
  return (
    <SideDock key="stream" panel="stream" title={t('ribbon.value_stream')} icon={VALUE_STREAM_ICON} badge={warnings === 0 ? null : String(warnings)}>
      <StreamPanel map={map} />
    </SideDock>
  );
}
