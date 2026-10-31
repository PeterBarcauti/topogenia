// sheetPanel.tsx
// The sheet panel shown in a test: the sheet answered by a fake API, the step VTE-E002 of the lane Commercial, the project of two members, and what the panel asked of the map.

import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import type { Schema } from '../api/client.ts';
import { SheetPanel } from '../features/sheet/SheetPanel.tsx';
import type { SheetPanelProps, SheetSection } from '../features/sheet/SheetContext.tsx';
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

/** The folded sections held by the test, as the preferences of the editor hold them. */
function Harness({ props }: { props: SheetPanelProps }) {
  const [folded, setFolded] = useState(props.folded);
  return (
    <SheetPanel {...props} folded={folded} onFold={(section: SheetSection, value: boolean) => {
      props.onFold(section, value);
      setFolded((current) => ({ ...current, [section]: value }));
    }} />
  );
}

export const STEP = { id: 's2', code: 'VTE-E002', label: 'Vérifier la demande', laneName: 'Commercial', assigneeUserId: null };

/** Shows the panel of the step s2 on the routes given, without waiting for anything. */
export function mountSheet(routes: FakeRoutes, overrides: Partial<SheetPanelProps> = {}) {
  const sent = fakeApi(routes);
  const record: SheetRecord = { summaries: [], notes: [], assigned: [], events: [] };
  const props: SheetPanelProps = {
    step: STEP,
    project: { members: [memberOf(AUTHOR_ID, 'Camille Bernard'), memberOf(OTHER_ID, 'Alex Martin', 'reader')], sharingGroupLabel: 'Finance' },
    codeOf: (stepId) => ({ s1: 'VTE-E001', s3: 'VTE-E003' })[stepId] ?? null,
    folded: { inputs: false, procedure: false, outputs: false },
    onFold: (section, value) => record.events.push(`fold:${section}:${String(value)}`),
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
  const rendered = render(<KeyboardProvider><Harness props={props} /></KeyboardProvider>);
  return { ...rendered, sent, record, props };
}

/** Shows the panel of the sheet given (read by `GET /api/steps/s2/sheet`), with the other routes given; waits for the sheet. */
export async function renderSheet(view: Schema<'SheetView'>, routes: FakeRoutes = {}, overrides: Partial<SheetPanelProps> = {}) {
  const shown = mountSheet({ 'GET /api/steps/s2/sheet': { json: view }, ...routes }, overrides);
  await screen.findByTestId(view.is_draft ? 'sheet-body' : 'sheet-read');
  return shown;
}
