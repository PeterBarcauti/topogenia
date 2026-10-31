// remainder.test.ts
// The remainder of a frame: its size, the steps it may go to in two groups and in the reading order, the moment it is stored on its own, what « Finish » did (annex B 2.9).

import { frameOf, laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { arrangementOutcome, assignCandidates, remainderSize, storedOnItsOwn } from './remainder.ts';

describe('the remainder (B-2.9-21, B-2.9-29, B-2.9-30, B-2.9-37, B-2.9-38)', () => {
  it('counts the documents, the documents used and the pain points left', () => {
    const frame = frameOf([], {
      documents: [{ id: 'o', name: 'Devis', direction: 'output', used_by: [] }],
      usages: [{ document_id: 'u', name: 'Bon', producer_code: 'VTE-E001' }, { document_id: 'v', name: 'Avoir', producer_code: null }],
      pain_points: [{ id: 'p', code: 'IRR-001', description: 'Ressaisie', status: 'reported' }],
    });
    expect([remainderSize(frame), remainderSize(frameOf([]))]).toEqual([4, 0]);
  });

  it('offers the steps of the frame, then the others, in the reading order, without events nor joining gateways', () => {
    const map = mapOf({
      lanes: [laneOf('l2', 1), laneOf('l1', 0)],
      steps: [
        stepOf('late', 'l1', 4), stepOf('low', 'l2', 1), stepOf('high', 'l1', 1), stepOf('start', 'l1', 0, { type: 'start' }), stepOf('join', 'l2', 2, { type: 'parallel' }),
        stepOf('choice', 'l2', 3, { type: 'decision' }), stepOf('or', 'l1', 3, { type: 'inclusive' }), stepOf('end', 'l1', 5, { type: 'end' }), stepOf('wait', 'l2', 0, { type: 'wait' }),
      ],
    });
    const { inFrame, others } = assignCandidates(map, frameOf(['low', 'choice', 'join', 'high']));
    expect([inFrame.map((step) => step.id), others.map((step) => step.id)]).toEqual([['high', 'low', 'choice'], ['wait', 'late']]);
  });

  it('is stored on its own when one step or none is left in the frame', () => {
    expect([storedOnItsOwn(frameOf([])), storedOnItsOwn(frameOf(['a'])), storedOnItsOwn(frameOf(['a', 'b']))]).toEqual([true, true, false]);
  });

  it('says where « Finish » put each element: a step, the map, nowhere, or dropped as already received', () => {
    const outcomes = [
      { kind: 'document', step_id: 'a', step_code: 'VTE-E004' }, { kind: 'pain_point', step_id: null, step_code: null }, { kind: 'document', step_id: null, step_code: null },
      { kind: 'usage', step_id: null, step_code: 'VTE-E002' }, { kind: 'usage', step_id: 'b', step_code: 'VTE-E002' }, { kind: 'usage', step_id: null, step_code: null },
    ] as const;
    expect(outcomes.map((arrangement) => arrangementOutcome(arrangement))).toEqual(['to-step', 'to-map', 'deleted', 'duplicate', 'to-step', 'deleted']);
  });
});
