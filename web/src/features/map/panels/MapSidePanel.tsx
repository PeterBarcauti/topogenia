// MapSidePanel.tsx
// The panel docked on the right of the canvas, one at a time, in its frame that resizes and folds: the remainder of the open frame, the sheet of the step open (a new one for each step), the assistant, the value stream with its layer on (spec 9.5, annex B 2.9, B 4, spec 3.0 sections 3.1 and 15.2).

import { useCallback } from 'react';
import { FileText, Sparkles, SquareDashed } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { AssistantPanel } from '../../assistant/panel/AssistantPanel.tsx';
import { VALUE_STREAM_ICON } from '../chrome/shapeIcons.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
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
  const close = useCallback(() => {
    editor.floating.getState().close();
  }, [editor]);
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
        <SheetDock key={openSheet} map={map} stepId={openSheet} project={project} />
      </SideDock>
    );
  }
  if (assistant) return <SideDock key="assistant" panel="assistant" title={t('assistant.panel.name')} icon={Sparkles}><AssistantPanel /></SideDock>;
  if (!stream) return null;
  const warnings = map.value_stream.issues.filter((issue) => issue.severity === 'warning').length;
  return (
    <SideDock key="stream" panel="stream" title={t('ribbon.value_stream')} icon={VALUE_STREAM_ICON} badge={warnings === 0 ? null : String(warnings)}>
      <StreamPanel map={map} />
    </SideDock>
  );
}
