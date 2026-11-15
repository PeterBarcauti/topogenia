// MapSidePanel.tsx
// The panel docked on the right of the canvas, one at a time, in its frame that resizes and folds: the remainder of the open frame, the sheet of the step open (a new one for each step), the assistant, the value stream or the analysis with its layer on (spec 9.5, annex B 2.9, B 4, spec 3.0 sections 3.1 and 15.2, plan 3.1).

import { useCallback, useState } from 'react';
import { FileText, Sparkles, SquareDashed } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { AssistantPanel } from '../../assistant/panel/AssistantPanel.tsx';
import { ReportTargets } from '../../sheet/SheetContext.tsx';
import { ANALYSIS_ICON, VALUE_STREAM_ICON } from '../chrome/shapeIcons.ts';
import { hasContent } from '../model/stepFacts.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import { AnalysisPanel } from './AnalysisPanel.tsx';
import { RemainderPanel } from './RemainderPanel.tsx';
import { SheetDock, useSheetProject } from './SheetDock.tsx';
import { SideDock } from './SideDock.tsx';
import { StreamPanel } from './StreamPanel.tsx';

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
  const close = useCallback(() => {
    editor.floating.getState().close();
  }, [editor]);
  // A pain point asked from the outputs of a producer (F-118): the sheet of the step that receives opens, its inputs
  // unfolded for this opening (`SheetDock`), and the field of the document takes the caret; kept here, above the sheet
  // that changes.
  const [pain, setPain] = useState<{ readonly stepId: string; readonly documentId: string } | null>(null);
  // Asked for one opening: once the sheet asked is closed, or left for another one, the request goes with it, taken or
  // not, so that no sheet opened later takes the caret (R4-10); adjusted while drawing, as the open sheet changes.
  const [shown, setShown] = useState(openSheet);
  if (shown !== openSheet) {
    setShown(openSheet);
    if (pain !== null && pain.stepId !== openSheet) setPain(null);
  }
  const report = useCallback((stepId: string, documentId: string) => {
    setPain({ stepId, documentId });
    editor.store.setState({ openSheet: stepId });
  }, [editor]);
  const focused = useCallback(() => {
    setPain(null);
  }, []);
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
