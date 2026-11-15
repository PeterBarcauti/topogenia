// tour.test.ts
// The tour of a presentation (ADR 097 revised, lot J10): every step of the map told once; the main path of the core as the thread; at a decision each exit shown from it, the one of the thread last; the exits of any other step sent together and their branches told in turn; the other entries first, the steps no flow reaches last; from a step chosen, what follows it only.

import { describe, expect, it } from 'vitest';
import type { Schema } from '../../../../api/client.ts';
import { flowOf, laneOf, mapOf, stepOf, streamOf } from '../../../../test/maps.ts';
import { entriesOf, tourOf } from './tour.ts';

const lanes = [laneOf('l1', 0), laneOf('l2', 1)];

const pathOf = (steps: string[]) => ({ steps, segments: [], lead_hours: 0, lead_complete: false, work_minutes: 0, work_complete: false });

/** A start, a check, a decision (yes to the end, no back to the check, another exit to a step of its own), an end: the main path given by the core. */
function decisionMap(mainPath: string[] | null) {
  return mapOf({
    lanes,
    steps: [
      stepOf('s1', 'l1', 0, { type: 'start' }), stepOf('s2', 'l1', 1), stepOf('s3', 'l1', 2, { type: 'decision' }),
      stepOf('s4', 'l2', 3, { type: 'end' }), stepOf('s5', 'l2', 3, { row: 1 }),
    ],
    flows: [flowOf('f1', 's1', 's2'), flowOf('f2', 's2', 's3'), flowOf('f3', 's3', 's4'), flowOf('f4', 's3', 's2'), flowOf('f5', 's3', 's5')],
    value_stream: streamOf({
      main_path: mainPath === null ? null : pathOf(mainPath),
      flows: {
        f3: { flow_id: 'f3', share: 0.2, origin: 'entered' }, f5: { flow_id: 'f5', share: 0.7, origin: 'entered' },
        f4: { flow_id: 'f4', share: 0.1, origin: 'entered' },
      },
    }),
  });
}

type Tour = ReturnType<typeof tourOf>;
const stopsOf = (tour: Tour) => tour.stops.map((stop) => stop.stepId);
const waysOf = (tour: Tour) => tour.stops.map((stop) => stop.next?.flows ?? null);
const asideOf = (tour: Tour) => tour.stops.filter((stop) => stop.aside === true).map((stop) => stop.stepId);
const againOf = (tour: Tour) => tour.stops.map((stop, index) => (stop.again === true ? index : -1)).filter((index) => index >= 0);

/** Every step of the map is shown, once apart from a decision shown again; every flow is lit, once. */
function expectWhole(map: Schema<'MapView'>, tour: Tour): void {
  const first = tour.stops.filter((stop) => stop.again !== true).map((stop) => stop.stepId);
  expect([...first].sort()).toEqual(map.steps.map((step) => step.id).sort());
  expect(tour.stops.flatMap((stop) => stop.next?.flows ?? []).sort()).toEqual(map.flows.map((flow) => flow.id).sort());
  // Each way ends on the stop that follows it.
  tour.stops.slice(0, -1).forEach((stop, index) => {
    expect(stop.next?.to).toBe(tour.stops[index + 1]?.stepId);
  });
}

describe('the tour of a presentation', () => {
  it('follows the main path of the core, and shows a decision once for each exit: the other exits first, the stronger share first, the one of the path last', () => {
    const map = decisionMap(['s1', 's2', 's3', 's4']);
    const tour = tourOf(map, null);
    // The decision: by its strongest exit to a step of its own, back from it; by its exit back to the check, only lit; then on by the path.
    expect(stopsOf(tour)).toEqual(['s1', 's2', 's3', 's5', 's3', 's3', 's4']);
    expect(waysOf(tour)).toEqual([['f1'], ['f2'], ['f5'], [], ['f4'], ['f3'], null]);
    expect(tour.stops[4]?.next).toEqual({ flows: ['f4'], to: 's3' });
    expect(againOf(tour)).toEqual([4, 5]);
    expect(asideOf(tour)).toEqual(['s5']);
    expectWhole(map, tour);
  });

  it('from a step chosen, tells that step and what follows it, by the same rules, and nothing before it', () => {
    expect(stopsOf(tourOf(decisionMap(['s1', 's2', 's3', 's4']), 's2'))).toEqual(['s2', 's3', 's5', 's3', 's3', 's4']);
  });

  it('without a main path, goes on at a decision by the strongest share of the core, never by a flow back while another exit is there', () => {
    const map = decisionMap(null);
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s1', 's2', 's3', 's4', 's3', 's3', 's5']);
    expect(waysOf(tour)).toEqual([['f1'], ['f2'], ['f3'], [], ['f4'], ['f5'], null]);
    expectWhole(map, tour);
  });

  it('from a step off the main path, follows its exits until it reaches the path, then the path; a step that is no decision sends its exits together', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0, { type: 'start' }), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2, { type: 'end' }), stepOf('x', 'l2', 0), stepOf('y', 'l2', 1)],
      flows: [flowOf('f1', 'a', 'b'), flowOf('f2', 'b', 'c'), flowOf('f3', 'x', 'y'), flowOf('f4', 'y', 'b'), flowOf('f5', 'y', 'x')],
      value_stream: streamOf({ main_path: pathOf(['a', 'b', 'c']) }),
    });
    const tour = tourOf(map, 'x');
    expect(stopsOf(tour)).toEqual(['x', 'y', 'b', 'c']);
    // The flow back of y to x, a step told already, is lit with the flow its line goes on by, which comes first.
    expect(waysOf(tour)).toEqual([['f3'], ['f4', 'f5'], ['f2'], null]);
  });

  it('takes a flow back only when it is the one exit, never comes back to a step already shown, and lights after its last stop the flow left', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2)],
      flows: [flowOf('f1', 'a', 'b'), flowOf('f2', 'b', 'c'), flowOf('f3', 'c', 'a')],
    });
    expect(stopsOf(tourOf(map, 'b'))).toEqual(['b', 'c', 'a']);
    // The flow of the last stop closes on a step told: lit, it ends there, and no stop follows.
    expect(tourOf(map, 'b').stops.at(-1)?.next).toEqual({ flows: ['f1'], to: 'b' });
    const whole = tourOf(map, null);
    expect(stopsOf(whole)).toEqual(['a', 'b', 'c']);
    expect(whole.stops.at(-1)?.next).toEqual({ flows: ['f3'], to: 'a' });
  });

});

describe('the branches of a tour (lot J10)', () => {
  it('sends the branches of a parallel gateway together, then tells each one up to the join, the one of the main path last', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('p', 'l1', 0, { type: 'parallel' }), stepOf('b1', 'l1', 1), stepOf('b2', 'l2', 1), stepOf('j', 'l1', 2, { type: 'parallel' })],
      flows: [flowOf('f1', 'p', 'b1'), flowOf('f2', 'p', 'b2'), flowOf('f3', 'b1', 'j'), flowOf('f4', 'b2', 'j')],
      value_stream: streamOf({ main_path: pathOf(['p', 'b2', 'j']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['p', 'b1', 'b2', 'j']);
    // Both exits leave together, the one of the branch told first ahead; that branch lit up to the join, then the other one told.
    expect(tour.stops[0]?.next).toEqual({ flows: ['f1', 'f2'], to: 'b1' });
    expect(tour.stops[1]?.next).toEqual({ flows: ['f3'], to: 'b2' });
    expect(tour.stops[2]?.next).toEqual({ flows: ['f4'], to: 'j' });
    expect(asideOf(tour)).toEqual(['b1']);
    expectWhole(map, tour);
  });

  it('tells the branches of a step with several exits and no gateway as those of a parallel one, as the core counts them', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('s', 'l1', 0, { type: 'start' }), stepOf('t', 'l1', 1), stepOf('u', 'l1', 2), stepOf('v', 'l2', 2), stepOf('w', 'l1', 3),
        stepOf('e', 'l1', 4, { type: 'end' }),
      ],
      flows: [flowOf('f1', 's', 't'), flowOf('f2', 't', 'u'), flowOf('f3', 't', 'v'), flowOf('f4', 'u', 'w'), flowOf('f5', 'v', 'w'), flowOf('f6', 'w', 'e')],
      value_stream: streamOf({ main_path: pathOf(['s', 't', 'u', 'w', 'e']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s', 't', 'v', 'u', 'w', 'e']);
    expect(waysOf(tour)).toEqual([['f1'], ['f3', 'f2'], ['f5'], ['f4'], ['f6'], null]);
    expect(againOf(tour)).toEqual([]);
    expectWhole(map, tour);
  });

  it('shows an inclusive gateway once for each exit, as a decision', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('o', 'l1', 0, { type: 'inclusive' }), stepOf('b1', 'l1', 1), stepOf('b2', 'l2', 1), stepOf('j', 'l1', 2, { type: 'inclusive' })],
      flows: [flowOf('f1', 'o', 'b1'), flowOf('f2', 'o', 'b2'), flowOf('f3', 'b1', 'j'), flowOf('f4', 'b2', 'j')],
      value_stream: streamOf({ main_path: pathOf(['o', 'b1', 'j']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['o', 'b2', 'o', 'b1', 'j']);
    expect(waysOf(tour)).toEqual([['f2'], ['f4'], ['f1'], ['f3'], null]);
    expectWhole(map, tour);
  });

  it('tells a branch that divides in its turn before it comes back to the decision it left', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('s', 'l1', 0, { type: 'start' }), stepOf('d', 'l1', 1, { type: 'decision' }), stepOf('e', 'l1', 4, { type: 'end' }),
        stepOf('n', 'l2', 2), stepOf('dd', 'l2', 3, { type: 'decision' }), stepOf('m', 'l2', 4), stepOf('z', 'l2', 5, { type: 'end' }),
      ],
      flows: [
        flowOf('f1', 's', 'd'), flowOf('yes', 'd', 'e'), flowOf('no', 'd', 'n'), flowOf('g1', 'n', 'dd'), flowOf('g2', 'dd', 'm'), flowOf('g3', 'dd', 'e'),
        flowOf('g4', 'm', 'z'),
      ],
      value_stream: streamOf({ main_path: pathOf(['s', 'd', 'e']) }),
    });
    const tour = tourOf(map, null);
    // The branch of « no »: n, its decision by its exit to the end of the thread (only lit), then by its own line to its end; back to d, then « yes ».
    expect(stopsOf(tour)).toEqual(['s', 'd', 'n', 'dd', 'dd', 'm', 'z', 'd', 'e']);
    expect(waysOf(tour)).toEqual([['f1'], ['no'], ['g1'], ['g3'], ['g2'], ['g4'], [], ['yes'], null]);
    expectWhole(map, tour);
  });

});

describe('the entries of a map and the steps no flow reaches (lot J10)', () => {
  it('tells the other entries first, each to the step where it joins the rest, then the thread from its own start', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('s1', 'l1', 0, { type: 'start' }), stepOf('a', 'l1', 1), stepOf('b', 'l1', 2), stepOf('e', 'l1', 3, { type: 'end' }),
        stepOf('s2', 'l2', 0, { type: 'start' }), stepOf('x', 'l2', 1),
      ],
      flows: [flowOf('f1', 's1', 'a'), flowOf('f2', 'a', 'b'), flowOf('f3', 'b', 'e'), flowOf('g1', 's2', 'x'), flowOf('g2', 'x', 'b')],
      value_stream: streamOf({ main_path: pathOf(['s1', 'a', 'b', 'e']) }),
    });
    const tour = tourOf(map, null);
    expect(entriesOf(map)).toEqual(['s1', 's2']);
    expect(stopsOf(tour)).toEqual(['s2', 'x', 's1', 'a', 'b', 'e']);
    // The flow by which the other entry joins the thread is lit as the story leaves it for the start of the thread.
    expect(waysOf(tour)).toEqual([['g1'], ['g2'], ['f1'], ['f2'], ['f3'], null]);
    expect(asideOf(tour)).toEqual(['s2', 'x']);
    expectWhole(map, tour);
    // From a step chosen, the other entry is not told: it does not follow that step.
    expect(stopsOf(tourOf(map, 'a'))).toEqual(['a', 'b', 'e']);
  });

  it('tells last, in the reading order, the steps no flow reaches from an entry: a step alone, a loop without an entry', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('t', 'l1', 0), stepOf('s', 'l2', 0, { type: 'start' }), stepOf('e', 'l2', 1, { type: 'end' }),
        stepOf('p', 'l1', 2), stepOf('q', 'l1', 3),
      ],
      flows: [flowOf('f1', 's', 'e'), flowOf('g1', 'p', 'q'), flowOf('g2', 'q', 'p')],
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s', 'e', 't', 'p', 'q']);
    expect(waysOf(tour)).toEqual([['f1'], [], [], ['g1'], ['g2']]);
    expect(tour.stops.at(-1)?.next).toEqual({ flows: ['g2'], to: 'p' });
    expect(asideOf(tour)).toEqual(['t', 'p', 'q']);
    expect(tourOf(mapOf({ lanes }), null).stops).toEqual([]);
  });

  it('without any flow, shows the steps in the reading order, column then lane then row, jumping from one to the next', () => {
    const map = mapOf({ lanes, steps: [stepOf('c', 'l1', 1), stepOf('b', 'l2', 0), stepOf('a', 'l1', 0)] });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['a', 'b', 'c']);
    expect(tour.stops.map((stop) => stop.next)).toEqual([{ flows: [], to: 'b' }, { flows: [], to: 'c' }, null]);
  });

  it('ignores a chosen step that is not on the map', () => {
    expect(stopsOf(tourOf(decisionMap(['s1', 's2', 's3', 's4']), 'gone'))).toEqual(['s1', 's2', 's3', 's5', 's3', 's3', 's4']);
  });
});

describe('a decision and its exits', () => {
  it('tells the branch of the other exit up to the flow by which it joins the thread, comes back to the decision, then follows the main path', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('s1', 'l1', 0, { type: 'start' }), stepOf('d', 'l1', 1, { type: 'decision' }), stepOf('yes', 'l1', 2), stepOf('no', 'l2', 2), stepOf('end', 'l1', 3, { type: 'end' })],
      flows: [flowOf('f1', 's1', 'd'), flowOf('f2', 'd', 'yes'), flowOf('f3', 'd', 'no'), flowOf('f4', 'yes', 'end'), flowOf('f5', 'no', 'end')],
      value_stream: streamOf({ main_path: pathOf(['s1', 'd', 'yes', 'end']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s1', 'd', 'no', 'd', 'yes', 'end']);
    expect(waysOf(tour)).toEqual([['f1'], ['f3'], ['f5'], ['f2'], ['f4'], null]);
    // Back to the decision by a way that lights the flow of the branch into the end, a step of the thread not told yet.
    expect(tour.stops[2]?.next).toEqual({ flows: ['f5'], to: 'd' });
    expectWhole(map, tour);
  });

  it('lights an exit back to a step told already without telling that step again, and shows the decision again for its next exit', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('s1', 'l1', 0, { type: 'start' }), stepOf('check', 'l1', 1), stepOf('d', 'l1', 2, { type: 'decision' }), stepOf('end', 'l1', 3, { type: 'end' })],
      flows: [flowOf('f1', 's1', 'check'), flowOf('f2', 'check', 'd'), flowOf('yes', 'd', 'end'), flowOf('no', 'd', 'check')],
      value_stream: streamOf({ main_path: pathOf(['s1', 'check', 'd', 'end']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s1', 'check', 'd', 'd', 'end']);
    expect(tour.stops[2]?.next).toEqual({ flows: ['no'], to: 'd' });
    expect(tour.stops[3]).toEqual({ stepId: 'd', next: { flows: ['yes'], to: 'end' }, again: true });
    expectWhole(map, tour);
  });

  it('follows the main path the core gives even where another exit has the stronger share: that exit is shown first, the path last', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0, { type: 'decision' }), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2)],
      flows: [flowOf('ab', 'a', 'b'), flowOf('ac', 'a', 'c'), flowOf('bc', 'b', 'c')],
      value_stream: streamOf({
        main_path: pathOf(['a', 'b', 'c']),
        flows: { ab: { flow_id: 'ab', share: 0.4, origin: 'entered' }, ac: { flow_id: 'ac', share: 0.6, origin: 'remainder' } },
      }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['a', 'a', 'b', 'c']);
    expect(waysOf(tour)).toEqual([['ac'], ['ab'], ['bc'], null]);
  });

  it('goes forward before back, whatever the shares, while a step ahead is there; the step a flow back leads to is told as a branch when nothing told it before', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2)],
      flows: [flowOf('ba', 'b', 'a'), flowOf('bc', 'b', 'c')],
      value_stream: streamOf({ flows: { ba: { flow_id: 'ba', share: 0.9, origin: 'entered' }, bc: { flow_id: 'bc', share: 0.1, origin: 'entered' } } }),
    });
    const tour = tourOf(map, 'b');
    expect(stopsOf(tour)).toEqual(['b', 'a', 'c']);
    expect(waysOf(tour)).toEqual([['ba', 'bc'], [], null]);
    expect(asideOf(tour)).toEqual(['a']);
  });
});

describe('a map without any flow (relecture 6: R6-09)', () => {
  it('without any flow, shows every step whatever step the main path of the core is made of; from the step chosen when one is (R6-09)', () => {
    // Without flows the core gives as main path the one step of the longest lead time: here the second of the reading order.
    const map = mapOf({ lanes, steps: [stepOf('c', 'l1', 1), stepOf('b', 'l2', 0), stepOf('a', 'l1', 0)], value_stream: streamOf({ main_path: pathOf(['b']) }) });
    expect(stopsOf(tourOf(map, null))).toEqual(['a', 'b', 'c']);
    expect(stopsOf(tourOf(map, 'b'))).toEqual(['b', 'c']);
    // A start that is not the first step of the reading order does not cut the steps before it either.
    const started = mapOf({ lanes, steps: [stepOf('a', 'l1', 0), stepOf('s', 'l2', 0, { type: 'start' }), stepOf('c', 'l1', 1)] });
    expect(stopsOf(tourOf(started, null))).toEqual(['a', 's', 'c']);
  });
});
