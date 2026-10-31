// SheetDock.tsx
// The sheet panel of the step open, given what the map knows: the step and its lane in the language shown, the project read, the folded sections, the gauge, the person responsible, the map read again; the focus given back to the step (annex B 4, AR-91, NF08).

import { useEffect, useLayoutEffect, useState } from 'react';
import { useStore } from 'zustand';
import { api, type Schema } from '../../../api/client.ts';
import { SheetPanel } from '../../sheet/SheetPanel.tsx';
import type { SheetProject, SheetSection } from '../../sheet/SheetContext.tsx';
import { laneName, stepLabel } from '../model/mapLayout.ts';
import { focusBackOnStep } from '../canvas/stepFocus.ts';
import { assignStep, showSheetSummary } from '../state/actions/sheet.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';
import type { PreferenceName } from '../state/preferences.ts';

/** The preference that keeps each section folded (AR-91). */
const FOLDED: Readonly<Record<SheetSection, PreferenceName>> = { inputs: 'sheetInputsFolded', procedure: 'sheetProcedureFolded', outputs: 'sheetOutputsFolded' };

/**
 * The project of the map, read once when a sheet first opens (`GET /api/projects/{id}`, open to every member): its
 * members name the people of the sheets, its label names the sharing group; null before, or when it could not be read.
 */
export function useSheetProject(projectId: string, wanted: boolean): SheetProject | null {
  const [read, setRead] = useState<{ readonly projectId: string; readonly project: SheetProject | null } | null>(null);
  const known = read?.projectId === projectId;
  useEffect(() => {
    if (!wanted || known) return;
    let alive = true;
    api.get('/api/projects/{id}', { path: { id: projectId } }).then((detail) => {
      if (alive) setRead({ projectId, project: { members: detail.members, sharingGroupLabel: detail.sharing_group_label } });
    }, () => {
      if (alive) setRead({ projectId, project: null });
    });
    return () => {
      alive = false;
    };
  }, [projectId, wanted, known]);
  return known ? read.project : null;
}

export function SheetDock({ map, stepId, project }: { map: Schema<'MapView'>; stepId: string; project: SheetProject | null }) {
  const language = useMapLanguage();
  const editor = useEditor();
  const preferences = useStore(editor.preferences);
  // Closed while the focus is in it (Escape, its button), the panel gives the focus back to its step, still drawn then.
  useLayoutEffect(() => () => {
    focusBackOnStep(stepId, '[data-testid="sheet-panel"]');
  }, [stepId]);
  const step = map.steps.find((candidate) => candidate.id === stepId);
  if (step === undefined) return null;
  const lane = map.lanes.find((candidate) => candidate.id === step.lane_id);
  return (
    <SheetPanel
      step={{ id: step.id, code: step.code, label: stepLabel(step, language), laneName: lane === undefined ? '' : laneName(lane, language), assigneeUserId: step.assignee_user_id }}
      project={project}
      codeOf={(id) => map.steps.find((candidate) => candidate.id === id)?.code ?? null}
      folded={{ inputs: preferences.sheetInputsFolded, procedure: preferences.sheetProcedureFolded, outputs: preferences.sheetOutputsFolded }}
      onFold={(section, folded) => { preferences.set(FOLDED[section], folded); }}
      onClose={() => { editor.store.setState({ openSheet: null }); }}
      onSummary={(summary) => { showSheetSummary(editor, step.id, summary); }}
      onMapChanged={() => void editor.reload()}
      onAssign={(userId) => assignStep(editor, step.id, userId)}
      notify={editor.notify} />
  );
}
