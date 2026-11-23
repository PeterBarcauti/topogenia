// sheetPanel.tsx
// The sheet panel shown in a test: the sheet answered by a fake API, the step VIS-E002 of the lane Commercial, the project of two members, the steps whose sheet the map lets the person reach, and what the panel asked of the map.

import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import type { Schema } from '../api/client.ts';
import { SheetPanel } from '../features/sheet/SheetPanel.tsx';
import { ReportTargets, type SheetPanelProps, type SheetSection } from '../features/sheet/SheetContext.tsx';
import { KeyboardProvider } from '../shared/keyboard/KeyboardContext.tsx';
import type { ToastKind } from '../shared/ui/toasts.ts';
import { fakeApi, type FakeRoutes } from './fakeApi.ts';
import { AUTHOR_ID, memberOf, OTHER_ID } from './sheets.ts';

/** What the panel asked of the map, in order. */
export interface SheetRecord {
  readonly summaries: Schema<'SheetSummary'>[];
  readonly notes: [string, ToastKind | undefined][];
  readonly assigned: (string | null)[];
  readonly events: string[];
}

/** Every section of the sheet folded, or every one unfolded. */
export const allSections = (folded: boolean): Record<SheetSection, boolean> => ({ facts: folded, inputs: folded, procedure: folded, outputs: folded, pain_points: folded, tools: folded });

/** The folded sections held by the test, as the panel of the map holds them for a sheet open. */
function Harness({ props }: { props: SheetPanelProps }) {
  const [folded, setFolded] = useState(props.folded);
  return (
    <SheetPanel {...props} folded={folded} onFold={(section: SheetSection, value: boolean) => {
      props.onFold(section, value);
      setFolded((current) => ({ ...current, [section]: value }));
    }} onFoldAll={(value: boolean) => {
      props.onFoldAll(value);
      setFolded(allSections(value));
    }} />
  );
}

export const STEP = { id: 's2', code: 'VIS-E002', label: 'Vérifier la demande', laneName: 'Commercial', assigneeUserId: null };

/** Shows the panel of the step s2 on the routes given, without waiting for anything; every step reachable unless told (`ReportTargets`). */
export function mountSheet(routes: FakeRoutes, overrides: Partial<SheetPanelProps> = {}, reachable: (stepId: string) => boolean = () => true) {
  const sent = fakeApi(routes);
  const record: SheetRecord = { summaries: [], notes: [], assigned: [], events: [] };
  const props: SheetPanelProps = {
    step: STEP,
    project: { members: [memberOf(AUTHOR_ID, 'Camille Bernard'), memberOf(OTHER_ID, 'Alex Martin', 'reader')], sharingGroupLabel: 'Finance' },
    codeOf: (stepId) => ({ s1: 'VIS-E001', s3: 'VIS-E003' })[stepId] ?? null,
    // Unfolded here, so that a test reaches the fields it is about; the map opens a sheet folded (sheetDock.test.tsx).
    folded: allSections(false),
    onFold: (section, value) => record.events.push(`fold:${section}:${String(value)}`),
    onFoldAll: (value) => record.events.push(`fold-all:${String(value)}`),
    onClose: () => record.events.push('close'),
    onSummary: (summary) => record.summaries.push(summary),
    onMapChanged: () => record.events.push('map'),
    onAssign: (userId) => {
      record.assigned.push(userId);
      return Promise.resolve(true);
    },
    notify: (text, kind) => record.notes.push([text, kind]),
    ...overrides,
  };
  const rendered = render(<KeyboardProvider><ReportTargets.Provider value={reachable}><Harness props={props} /></ReportTargets.Provider></KeyboardProvider>);
  return { ...rendered, sent, record, props };
}

/** Shows the panel of the sheet given (read by `GET /api/steps/s2/sheet`), with the other routes given; waits for the sheet. */
export async function renderSheet(view: Schema<'SheetView'>, routes: FakeRoutes = {}, overrides: Partial<SheetPanelProps> = {}, reachable?: (stepId: string) => boolean) {
  const shown = mountSheet({ 'GET /api/steps/s2/sheet': { json: view }, ...routes }, overrides, reachable);
  await screen.findByTestId(view.is_draft ? 'sheet-body' : 'sheet-read');
  return shown;
}
