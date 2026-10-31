// frameAndRaci.test.tsx
// The canvas with a frame of dissolution and the RACI layer: the frame and its label, the steps under it, the badges under the steps (annex B 2.9, 2.11, 3.2).

import { act, fireEvent, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showEditor, showMap } from '../../../test/editor.tsx';
import { frameOf, sampleMap, sheetOf } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const remainder = {
  documents: [{ id: 'o', name: 'Devis', direction: 'output', used_by: ['VTE-E003'] }],
  usages: [{ document_id: 'u', name: 'Bon de commande', producer_code: 'VTE-E001' }],
  pain_points: [{ id: 'p', code: 'IRR-001', description: 'Ressaisie', status: 'reported' }],
} as const;

describe('the frame of a dissolution (B-2.9-17 to B-2.9-23, B-3.2-01, B-3.2-17)', () => {
  it('is drawn round its steps; its label says the remainder left; the steps of other flows under it are greyed', async () => {
    const canvas = await showMap(sampleMap({ dissolution: frameOf(['s2', 's4'], { documents: remainder.documents.map((document) => ({ ...document, used_by: [...document.used_by] })), usages: [...remainder.usages], pain_points: [...remainder.pain_points] }) }));
    expect(within(canvas).getByTestId('dissolution-frame')).toBeInTheDocument();
    expect(within(canvas).getByTestId('dissolution-label')).toHaveTextContent('Reliquat de VTE-E009 · 3');
    expect(within(canvas).getByTestId('dissolution-label')).toHaveAttribute('title', expect.stringContaining('VTE-E009 « Préparer la commande »'));
    expect(['VTE-E001', 'VTE-E002', 'VTE-E003', 'VTE-E004'].map((code) => within(canvas).getByTestId(`step-${code}`).classList.contains('is-outside'))).toEqual([false, false, true, false]);
  });

  it('with nothing left to assign, says « Remonté de CODE · rien à ventiler »', async () => {
    const canvas = await showMap(sampleMap({ dissolution: frameOf(['s2']) }));
    expect(within(canvas).getByTestId('dissolution-label')).toHaveTextContent('Remonté de VTE-E009 · rien à ventiler');
  });

  it('its label is a button that opens the panel of the remainder, for a reader too', async () => {
    const { editor, canvas } = await showEditor(sampleMap({ dissolution: frameOf(['s2']), rights: { structure: false, role: 'reader' } }));
    fireEvent.click(within(canvas).getByRole('button', { name: 'Remonté de VTE-E009 · rien à ventiler' }));
    expect(editor.floating.getState().floating).toEqual({ kind: 'remainder' });
  });

  it('is absent without a dissolution', async () => {
    const canvas = await showMap(sampleMap());
    expect(within(canvas).queryByTestId('dissolution-frame')).toBeNull();
  });
});

describe('the RACI layer on the canvas (B-2.11-04 to B-2.11-07)', () => {
  const map = sampleMap({ raci: [{ step_id: 's3', lane_id: 'l2', role: 'A' }] });

  it('is off by default: no badge', async () => {
    const canvas = await showMap(map);
    expect(within(canvas).queryAllByTestId('raci-badges')).toHaveLength(0);
  });

  it('on, shows « A lane » under a step, « A ? » under a step without approver, nothing under a start or an end', async () => {
    const canvas = await showMap(map, memoryStorage({ 'app.showRaci': '1' }));
    const badges = (code: string) => within(within(canvas).getByTestId(`step-${code}`)).queryByTestId('raci-badges')?.textContent ?? null;
    expect(['VTE-E001', 'VTE-E002', 'VTE-E003', 'VTE-E004'].map(badges)).toEqual([null, 'A ?', 'A Comptabilité', null]);
    expect(within(canvas).getByTestId('raci-missing')).toHaveAttribute('title', 'Aucun approbateur (A) pour cette étape');
    expect(within(canvas).getByTitle('Approuve : Comptabilité')).toHaveClass('raci-badge', 'raci-badge--A');
  });

  it('turned on and off by its button, kept in the preferences', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    act(() => {
      editor.preferences.getState().set('showRaci', true);
    });
    expect(within(canvas).getAllByTestId('raci-badges')).toHaveLength(2);
    act(() => {
      editor.preferences.getState().set('showRaci', false);
    });
    expect(within(canvas).queryAllByTestId('raci-badges')).toHaveLength(0);
  });

  it('places the badges under the frequency of a detailed step', async () => {
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false, sheet: sheetOf({ frequency: 'weekly' }) } : step));
    const canvas = await showMap({ ...map, steps }, memoryStorage({ 'app.showRaci': '1' }));
    expect(within(canvas).getByTestId('step-VTE-E002').style.getPropertyValue('--raci-top')).toBe('26px');
  });
});
