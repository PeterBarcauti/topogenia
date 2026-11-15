// tourShapes.test.ts
// The tour of a presentation on maps that twist (independent review of the lot J10): another entry that joins a branch of the thread, several entries that join at different steps, a parallel split inside a branch of a decision, a decision whose exits all go back, a flow from a step to itself, a gateway with one exit, a main path of the core that the flows do not follow, a main path that is one step tied to nothing.

import { describe, expect, it } from 'vitest';
import { flowOf, laneOf, mapOf, stepOf, streamOf } from '../../../../test/maps.ts';
import { tourOf } from './tour.ts';

const lanes = [laneOf('l1', 0), laneOf('l2', 1)];

const pathOf = (steps: string[]) => ({ steps, segments: [], lead_hours: 0, lead_complete: false, work_minutes: 0, work_complete: false });

type Tour = ReturnType<typeof tourOf>;
const stopsOf = (tour: Tour) => tour.stops.map((stop) => stop.stepId);
const waysOf = (tour: Tour) => tour.stops.map((stop) => stop.next?.flows ?? null);
const asideOf = (tour: Tour) => tour.stops.filter((stop) => stop.aside === true).map((stop) => stop.stepId);
const againOf = (tour: Tour) => tour.stops.map((stop, index) => (stop.again === true ? index : -1)).filter((index) => index >= 0);

describe('the other entries of a map and the branches of its thread (independent review, S1)', () => {
  it('stops another entry where it joins a branch of the thread: that branch is told with the thread, at the decision it leaves', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('s', 'l1', 0, { type: 'start' }), stepOf('d', 'l1', 1, { type: 'decision' }), stepOf('a', 'l1', 2), stepOf('e', 'l1', 4, { type: 'end' }),
        stepOf('s2', 'l2', 0, { type: 'start' }), stepOf('x', 'l2', 2), stepOf('y', 'l2', 3),
      ],
      flows: [
        flowOf('f1', 's', 'd'), flowOf('yes', 'd', 'a'), flowOf('f3', 'a', 'e'), flowOf('no', 'd', 'x'), flowOf('g1', 'x', 'y'), flowOf('g2', 'y', 'e'),
        flowOf('h', 's2', 'x'),
      ],
      value_stream: streamOf({ main_path: pathOf(['s', 'd', 'a', 'e']) }),
    });
    const tour = tourOf(map, null);
    // The other entry alone, its flow into the branch lit; then the thread, the branch of « no » told from the decision.
    expect(stopsOf(tour)).toEqual(['s2', 's', 'd', 'x', 'y', 'd', 'a', 'e']);
    expect(waysOf(tour)).toEqual([['h'], ['f1'], ['no'], ['g1'], ['g2'], ['yes'], ['f3'], null]);
    expect(asideOf(tour)).toEqual(['s2', 'x', 'y']);
  });

  it('tells each other entry up to its own joining step, in the reading order, before the thread', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('s', 'l1', 0, { type: 'start' }), stepOf('a', 'l1', 1), stepOf('b', 'l1', 2), stepOf('e', 'l1', 3, { type: 'end' }),
        stepOf('s2', 'l2', 0, { type: 'start' }), stepOf('s3', 'l2', 0, { type: 'start', row: 1 }), stepOf('m', 'l2', 1, { row: 1 }),
      ],
      flows: [flowOf('f1', 's', 'a'), flowOf('f2', 'a', 'b'), flowOf('f3', 'b', 'e'), flowOf('h2', 's2', 'a'), flowOf('h3', 's3', 'm'), flowOf('h4', 'm', 'b')],
      value_stream: streamOf({ main_path: pathOf(['s', 'a', 'b', 'e']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s2', 's3', 'm', 's', 'a', 'b', 'e']);
    expect(waysOf(tour)).toEqual([['h2'], ['h3'], ['h4'], ['f1'], ['f2'], ['f3'], null]);
  });
});

describe('gateways inside branches, and exits that lead nowhere new (independent review of the lot J10)', () => {
  it('tells a parallel split met in a branch of a decision, each of its branches in turn, before it comes back to the decision', () => {
    const map = mapOf({
      lanes,
      steps: [
        stepOf('s', 'l1', 0, { type: 'start' }), stepOf('d', 'l1', 1, { type: 'decision' }), stepOf('e', 'l1', 5, { type: 'end' }),
        stepOf('p', 'l2', 2, { type: 'parallel' }), stepOf('b1', 'l2', 3), stepOf('b2', 'l2', 3, { row: 1 }), stepOf('j', 'l2', 4, { type: 'parallel' }),
      ],
      flows: [
        flowOf('f1', 's', 'd'), flowOf('yes', 'd', 'e'), flowOf('no', 'd', 'p'), flowOf('p1', 'p', 'b1'), flowOf('p2', 'p', 'b2'), flowOf('j1', 'b1', 'j'),
        flowOf('j2', 'b2', 'j'), flowOf('je', 'j', 'e'),
      ],
      value_stream: streamOf({ main_path: pathOf(['s', 'd', 'e']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s', 'd', 'p', 'b2', 'b1', 'j', 'd', 'e']);
    expect(waysOf(tour)).toEqual([['f1'], ['no'], ['p2', 'p1'], ['j2'], ['j1'], ['je'], ['yes'], null]);
    expect(againOf(tour)).toEqual([6]);
  });

  it('shows a decision whose exits all go back once for each, each exit only lit, and ends on the flow of the last one', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0), stepOf('b', 'l1', 1), stepOf('d', 'l1', 2, { type: 'decision' })],
      flows: [flowOf('ab', 'a', 'b'), flowOf('bd', 'b', 'd'), flowOf('x', 'd', 'a'), flowOf('y', 'd', 'b')],
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['a', 'b', 'd', 'd']);
    expect(waysOf(tour)).toEqual([['ab'], ['bd'], ['x'], ['y']]);
    expect(tour.stops.at(-1)).toEqual({ stepId: 'd', next: { flows: ['y'], to: 'b' }, again: true });
  });

  it('lights a flow from a step to itself with the flow its line goes on by, and tells that step once', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2)],
      flows: [flowOf('ab', 'a', 'b'), flowOf('bb', 'b', 'b'), flowOf('bc', 'b', 'c')],
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['a', 'b', 'c']);
    expect(waysOf(tour)).toEqual([['ab'], ['bc', 'bb'], null]);
  });

  it('shows once a decision with one exit, and an inclusive gateway that only joins', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('s', 'l1', 0, { type: 'start' }), stepOf('d', 'l1', 1, { type: 'decision' }), stepOf('o', 'l1', 2, { type: 'inclusive' }), stepOf('e', 'l1', 3, { type: 'end' })],
      flows: [flowOf('f1', 's', 'd'), flowOf('f2', 'd', 'o'), flowOf('f3', 's', 'o'), flowOf('f4', 'o', 'e')],
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['s', 'd', 'o', 'e']);
    expect(againOf(tour)).toEqual([]);
  });
});

describe('a main path of the core the flows do not make a thread of (independent review of the lot J10)', () => {
  it('follows the flows where the main path names a next step no flow leads to', () => {
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2)],
      flows: [flowOf('ab', 'a', 'b'), flowOf('bc', 'b', 'c')],
      value_stream: streamOf({ main_path: pathOf(['a', 'c']) }),
    });
    expect(stopsOf(tourOf(map, null))).toEqual(['a', 'b', 'c']);
  });

  it('does not take for its thread a main path that is one step tied to nothing: the process is the thread, that step is told last (M9)', () => {
    // Without a start, the core counts every step no flow reaches as an entry, and a step alone may win as the main path.
    const map = mapOf({
      lanes,
      steps: [stepOf('a', 'l1', 0), stepOf('d', 'l1', 1, { type: 'decision' }), stepOf('b', 'l1', 2), stepOf('c', 'l2', 2), stepOf('alone', 'l2', 0)],
      flows: [flowOf('ad', 'a', 'd'), flowOf('db', 'd', 'b'), flowOf('dc', 'd', 'c')],
      value_stream: streamOf({ main_path: pathOf(['alone']) }),
    });
    const tour = tourOf(map, null);
    expect(stopsOf(tour)).toEqual(['a', 'd', 'c', 'd', 'b', 'alone']);
    expect(asideOf(tour)).toEqual(['c', 'alone']);
  });
});
