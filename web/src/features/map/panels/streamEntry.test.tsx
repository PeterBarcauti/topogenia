// streamEntry.test.tsx
// The value stream layer on the canvas and its entries (spec 14.5, 14.6): the cartouches and the events drawn with the layer only, the quick add hidden; the volume of a start, the share of an exit with the remainder of a decision, the headcount of a lane, each sent once with its version, an empty field clearing it, Escape giving up.

import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { replyOf, sampleMap, streamOf } from '../../../test/maps.ts';
import { runMapCommand } from '../state/EditorContext.tsx';
import type { StreamEntry } from '../state/floating.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const stepStream = (id: string, overrides: Partial<Schema<'StepStream'>> = {}): Schema<'StepStream'> => ({
  step_id: id, throughput: 120, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: 120, basis: 'per_case', work_minutes: 20,
  work_origin: 'sheet', lead_hours: 14, lead_origin: 'sheet', expected_lead_hours: null, work_per_case_minutes: 20, charge_hours: 40, fte: 0.26,
  human: true, from_draft: false, ...overrides,
});

/** The sample map with a stream: 120 a month enter at s1, the decision s3 sends some back by « non » (f4) without a share yet. */
const streamed = () => sampleMap({
  value_stream: streamOf({
    steps: { s1: stepStream('s1', { entry: 'entered', entry_volume: 120 }), s2: stepStream('s2'), s3: stepStream('s3'), s4: stepStream('s4') },
    flows: { f3: { flow_id: 'f3', share: null, origin: 'unknown' }, f4: { flow_id: 'f4', share: null, origin: 'unknown' } },
  }),
});

async function withLayer(map = streamed()) {
  const shown = await showEditor(map);
  act(() => {
    shown.editor.preferences.getState().set('showValueStream', true);
  });
  return shown;
}

function openEntry(editor: Awaited<ReturnType<typeof showEditor>>['editor'], entry: StreamEntry) {
  act(() => {
    editor.floating.getState().open({ kind: 'stream-entry', entry, at: { x: 100, y: 120 } });
  });
  return screen.getByTestId(`stream-entry-${entry.kind}`);
}

describe('the layer on the canvas (spec 14.5)', () => {
  it('draws a cartouche under each step with content, the entry under the start, the exit under the end, with the layer only', async () => {
    const { canvas, editor } = await showEditor(streamed());
    expect(canvas.querySelectorAll('.stream-card')).toHaveLength(0);
    act(() => {
      editor.preferences.getState().set('showValueStream', true);
    });
    expect([...canvas.querySelectorAll('.stream-card')].map((card) => card.getAttribute('data-testid'))).toEqual(['stream-card-VTE-E002', 'stream-card-VTE-E003']);
    expect(screen.getByTestId('stream-entry-VTE-E001')).toHaveTextContent('Entrée120/mois');
    expect(screen.getByTestId('stream-exit-VTE-E004')).toHaveTextContent('Sortie120/mois');
    // The summary is in its panel beside the canvas, nothing under the lanes (spec 3.0 section 15.3).
    expect(canvas.querySelector('.stream-panel')).toBeNull();
    expect(screen.getByTestId('stream-panel').closest('.side-dock')).toHaveAttribute('data-panel', 'stream');
    expect(canvas.querySelectorAll('.quick-add')).toHaveLength(0);
  });

  it('writes an unknown share of an exit of a decision as a question, in the label of the flow', async () => {
    await withLayer();
    expect(screen.getAllByTestId('edge-share').map((share) => share.textContent)).toEqual(['?', '?']);
  });
});

describe('the entries of the layer (spec 14.6)', () => {
  it('« Volume entrant » on a start opens its field at the menu; the quantity and its period sent with the version of the step', async () => {
    const sent = fakeApi({ 'PATCH /api/steps/s1': { json: replyOf(streamed()) } });
    const { editor } = await withLayer();
    act(() => {
      runMapCommand(editor, 'step.volume', { kind: 'step', stepId: 's1', at: { x: 50, y: 60 } });
    });
    const form = screen.getByTestId('stream-entry-volume');
    expect([form.style.left, form.style.top]).toEqual(['50px', '60px']);
    await userEvent.clear(screen.getByTestId('stream-entry-input'));
    await userEvent.type(screen.getByTestId('stream-entry-input'), '2.5');
    await userEvent.selectOptions(screen.getByTestId('stream-entry-period'), 'day');
    await userEvent.type(screen.getByTestId('stream-entry-input'), '{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/steps/s1').map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ volume: { quantity: 2.5, period: 'day' } }, '"1"']]);
    });
    expect(screen.queryByTestId('stream-entry-volume')).toBeNull();
  });

  it('an empty field clears the value; Escape gives up without a call', async () => {
    const sent = fakeApi({ 'PATCH /api/steps/s1': { json: replyOf(streamed()) } });
    const map = streamed();
    const { editor } = await withLayer(sampleMap({ ...map, steps: map.steps.map((step) => (step.id === 's1' ? { ...step, volume: { quantity: 120, period: 'month' } } : step)) }));
    openEntry(editor, { kind: 'volume', stepId: 's1' });
    expect(screen.getByTestId('stream-entry-input')).toHaveValue(120);
    await userEvent.keyboard('{Escape}');
    expect(callsTo(sent, 'PATCH', '/api/steps/s1')).toEqual([]);
    openEntry(editor, { kind: 'volume', stepId: 's1' });
    await userEvent.clear(screen.getByTestId('stream-entry-input'));
    await userEvent.type(screen.getByTestId('stream-entry-input'), '{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/steps/s1').map((call) => call.body)).toEqual([{ volume: null }]);
    });
  });

  it('the share of an exit of a decision shows the remainder of the other exit as it is typed, and is sent in percent', async () => {
    const sent = fakeApi({ 'PUT /api/flows/f4/share': { json: replyOf(streamed()) } });
    const { editor } = await withLayer();
    openEntry(editor, { kind: 'share', flowId: 'f4' });
    await userEvent.type(screen.getByTestId('stream-entry-input'), '15');
    expect(screen.getByTestId('stream-entry-remainder')).toHaveTextContent('Reste pour « oui » : 85 %');
    await userEvent.type(screen.getByTestId('stream-entry-input'), '{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f4/share').map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ share: 15 }, '"1"']]);
    });
  });

  it('a share above 100 is refused by the field, nothing is sent', async () => {
    const sent = fakeApi({ 'PUT /api/flows/f4/share': { json: replyOf(streamed()) } });
    const { editor } = await withLayer();
    openEntry(editor, { kind: 'share', flowId: 'f4' });
    await userEvent.type(screen.getByTestId('stream-entry-input'), '120{Enter}');
    expect(callsTo(sent, 'PUT', '/api/flows/f4/share')).toEqual([]);
    expect(screen.getByTestId('stream-entry-share')).toBeInTheDocument();
  });

  it('the headcount of a lane, a whole number of people', async () => {
    const sent = fakeApi({ 'PATCH /api/lanes/l2': { json: replyOf(streamed()) } });
    const { editor } = await withLayer();
    openEntry(editor, { kind: 'headcount', laneId: 'l2' });
    await userEvent.type(screen.getByTestId('stream-entry-input'), '3{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/lanes/l2').map((call) => call.body)).toEqual([{ headcount: 3 }]);
    });
  });
});
