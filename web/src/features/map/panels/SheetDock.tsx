// SheetDock.tsx
// The sheet panel of the step open, given what the map knows: the step and its lane in the language shown, the project read, the sections folded at each opening unless « Tout déplier » was asked since the map opened (the inputs unfolded for this opening when a pain point is asked), the gauge, the person responsible, the map read again; the focus given back to the step (annex B 4, AR-91, ADR 102, NF08).

import { useEffect, useLayoutEffect, useState } from 'react';
import { useStore } from 'zustand';
import { api, type Schema } from '../../../api/client.ts';
import { SheetPanel } from '../../sheet/SheetPanel.tsx';
import { SHEET_SECTIONS, type SheetProject, type SheetSection } from '../../sheet/SheetContext.tsx';
import { laneName, stepLabel } from '../model/mapLayout.ts';
import { focusBackOnStep } from '../canvas/stepFocus.ts';
import { assignStep, showSheetSummary } from '../state/actions/sheet.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';
import { bpmnView } from '../bpmn/bpmnState.ts';

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
    // A failed reading is not kept: the sheet opened again reads the project again (audit 5.5).
    api.get('/api/projects/{id}', { path: { id: projectId } }).then((detail) => {
      if (alive) setRead({ projectId, project: { members: detail.members, sharingGroupLabel: detail.sharing_group_label } });
    }, () => undefined);
    return () => {
      alive = false;
    };
  }, [projectId, wanted, known]);
  return known ? read.project : null;
}

/** The pain point asked from the outputs of a producer (F-118): the document whose field takes the caret here, the way to ask it, what forgets it. */
export interface PainReport {
  readonly focus: string | null;
  readonly onReport: (stepId: string, documentId: string) => void;
  readonly onFocused: () => void;
}

export function SheetDock({ map, stepId, project, report }: { map: Schema<'MapView'>; stepId: string; project: SheetProject | null; report: PainReport }) {
  const language = useMapLanguage();
  const editor = useEditor();
  // A sheet opens folded, so that it is read at a glance (essai J10): every section unless « Tout déplier » was asked
  // since the map opened; a section folded or unfolded by hand is so for this opening only, the panel being new for
  // each step. A pain point asked here unfolds the inputs for this opening (ADR 102 point 2); a fold by hand ends it.
  const unfolded = useStore(editor.store, (state) => state.sheetsUnfolded);
  // In the view in BPMN 2.0 the sheet is read only: it is changed on the map (ADR 106).
  const bpmnOpen = useStore(bpmnView, (state) => state.open);
  const [own, setOwn] = useState<Partial<Record<SheetSection, boolean>>>({});
  const [asked, setAsked] = useState<string | null>(null);
  if (report.focus !== asked) {
    setAsked(report.focus);
    if (report.focus !== null) setOwn((current) => ({ ...current, inputs: false }));
  }
  const folded = Object.fromEntries(SHEET_SECTIONS.map((section) => [section, own[section] ?? !unfolded])) as Record<SheetSection, boolean>;
  const step = map.steps.find((candidate) => candidate.id === stepId);
  // Closed while the focus is in it (Escape, its button), the panel gives the focus back to its step, still drawn then:
  // on the map, or in the view in BPMN 2.0, where the form of the step is named by its code.
  const code = step?.code;
  useLayoutEffect(() => () => {
    focusBackOnStep(stepId, '[data-testid="sheet-panel"]', code);
  }, [stepId, code]);
  if (step === undefined) return null;
  const lane = map.lanes.find((candidate) => candidate.id === step.lane_id);
  return (
    <SheetPanel
      step={{ id: step.id, code: step.code, label: stepLabel(step, language), laneName: lane === undefined ? '' : laneName(lane, language), assigneeUserId: step.assignee_user_id }}
      project={project}
      codeOf={(id) => map.steps.find((candidate) => candidate.id === id)?.code ?? null}
      folded={folded}
      onFold={(section, value) => { setOwn((current) => ({ ...current, [section]: value })); }}
      onFoldAll={(value) => {
        editor.store.setState({ sheetsUnfolded: !value });
        setOwn({});
      }}
      onClose={() => { editor.store.setState({ openSheet: null }); }}
      onSummary={(summary) => { showSheetSummary(editor, step.id, summary); }}
      onMapChanged={() => void editor.reload()}
      onAssign={(userId) => assignStep(editor, step.id, userId)}
      notify={editor.notify}
      desktop={editor.desktop}
      onReport={report.onReport} painFocus={report.focus} onPainFocused={report.onFocused} readOnly={bpmnOpen} />
  );
}
