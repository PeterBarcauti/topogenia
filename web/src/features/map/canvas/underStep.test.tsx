// underStep.test.tsx
// The canvas puts what hangs under a step aside of the axis of its bottom point when a flow uses it: the frequency of a detailed step, the RACI badges, the label of a gateway (AR-58, AR-128).

import { within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showMap } from '../../../test/editor.tsx';
import { flowOf, laneOf, mapOf, sheetOf, stepOf } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

/**
 * The case of the demonstration (P6-T11): a detailed step with its frequency, reached by its bottom point by the flow back
 * « non » of a decision, which comes from the aisle on its right; the decision leaves by its own bottom point.
 */
const demonstration = () => mapOf({
  lanes: [laneOf('l1', 0), laneOf('l2', 1), laneOf('l3', 2)],
  steps: [
    stepOf('s1', 'l1', 0, { type: 'start' }),
    stepOf('s2', 'l1', 1, { sub_map_id: 'm2', sub_map_empty: false, sheet: sheetOf({ frequency: 'monthly' }) }),
    stepOf('s3', 'l2', 2),
    stepOf('s4', 'l3', 3, { type: 'decision', label: 'Conforme ?' }),
  ],
  flows: [flowOf('f1', 's1', 's2'), flowOf('f2', 's2', 's3'), flowOf('f3', 's3', 's4'), flowOf('no', 's4', 's2', { label: 'non' })],
  raci: [{ step_id: 's2', lane_id: 'l2', role: 'A' }],
});

describe('what hangs under a step (AR-58, AR-128)', () => {
  it('goes left of the axis under the step the flow back reaches from its right, with the frequency and the RACI badges', async () => {
    const canvas = await showMap(demonstration(), memoryStorage({ 'app.showRaci': '1' }));
    const detailed = within(canvas).getByTestId('step-VTE-E002');
    expect(detailed).toHaveClass('is-under-left');
    expect(detailed).not.toHaveClass('is-under-right');
    expect(within(detailed).getByTestId('step-frequency')).toHaveTextContent('Mensuelle');
    expect(within(detailed).getByTestId('raci-badges')).toHaveTextContent('A Couloir l2');
  });

  it('goes right of the axis under the decision that leaves by its bottom point, and stays centred under the others', async () => {
    const canvas = await showMap(demonstration());
    expect(within(canvas).getByTestId('step-VTE-E004')).toHaveClass('step--gateway', 'is-under-right');
    for (const code of ['VTE-E001', 'VTE-E003']) {
      const step = within(canvas).getByTestId(`step-${code}`);
      expect([step.classList.contains('is-under-left'), step.classList.contains('is-under-right')]).toEqual([false, false]);
    }
  });
});
