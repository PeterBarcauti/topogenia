// counters.test.ts
// The counters of a presentation: the work and the lead time run up to a stop, by the main path of the core where the tour follows it, the figures of a step elsewhere; a long sentence cut into subtitles of two lines.

import { describe, expect, it } from 'vitest';
import { flowOf, laneOf, mapOf, stepOf, streamOf } from '../../../../test/maps.ts';
import { countersAt } from './counters.ts';
import { tourOf } from './tour.ts';

/** The figures of a step by execution, the same by case unless told (a step done by sessions differs). */
const figures = (work: number | null, lead: number | null, perCase = work) => ({ work_minutes: work, lead_hours: lead, work_per_case_minutes: perCase } as never);

describe('the counters of a presentation (A1 of the study)', () => {
  const map = mapOf({
    lanes: [laneOf('l1', 0)],
    steps: [stepOf('a', 'l1', 0), stepOf('b', 'l1', 1), stepOf('c', 'l1', 2), stepOf('x', 'l1', 1, { row: 1 })],
    flows: [flowOf('f1', 'a', 'b'), flowOf('f2', 'b', 'c'), flowOf('f3', 'a', 'x')],
    value_stream: streamOf({
      steps: { a: figures(10, 2), b: figures(99, 99), x: figures(5, null) },
      main_path: {
        steps: ['a', 'b', 'c'], lead_hours: 10, lead_complete: false, work_minutes: 40, work_complete: false,
        segments: [{ step_id: 'a', lead_hours: 2, work_minutes: 10 }, { step_id: 'b', lead_hours: 8, work_minutes: 30 }, { step_id: 'c', lead_hours: null, work_minutes: null }],
      },
    }),
  });

  it('adds the segments of the main path stop after stop, the stops not reached left out; a step of a branch told aside adds nothing (lot J10)', () => {
    const tour = tourOf(map, null);
    // The tour: a, its branch x told aside, then b and c of the main path.
    expect(tour.stops.map((stop) => stop.stepId)).toEqual(['a', 'x', 'b', 'c']);
    expect([0, 1, 2, 3].map((stop) => countersAt(map, tour, stop))).toEqual([
      { workMinutes: 10, leadHours: 2, complete: true }, { workMinutes: 10, leadHours: 2, complete: true }, { workMinutes: 40, leadHours: 10, complete: true },
      { workMinutes: 40, leadHours: 10, complete: false },
    ]);
  });

  it('counts a decision shown again for its next exit once (lot J10)', () => {
    const forked = mapOf({
      lanes: [laneOf('l1', 0)],
      steps: [stepOf('d', 'l1', 0, { type: 'decision' }), stepOf('y', 'l1', 1), stepOf('n', 'l1', 1, { row: 1 })],
      flows: [flowOf('yes', 'd', 'y'), flowOf('no', 'd', 'n')],
      value_stream: streamOf({
        steps: { d: figures(3, 1), y: figures(7, 2), n: figures(50, 50) },
        main_path: { steps: ['d', 'y'], lead_hours: 3, lead_complete: true, work_minutes: 10, work_complete: true, segments: [{ step_id: 'd', lead_hours: 1, work_minutes: 3 }, { step_id: 'y', lead_hours: 2, work_minutes: 7 }] },
      }),
    });
    const tour = tourOf(forked, null);
    expect(tour.stops.map((stop) => stop.stepId)).toEqual(['d', 'n', 'd', 'y']);
    expect([0, 1, 2, 3].map((stop) => countersAt(forked, tour, stop).workMinutes)).toEqual([3, 3, 3, 10]);
  });

  it('takes the figures of a step off the main path, and nothing before the first stop', () => {
    const tour = tourOf(map, 'x');
    expect(countersAt(map, tour, 0)).toEqual({ workMinutes: 5, leadHours: 0, complete: false });
    expect(countersAt(map, tour, -1)).toEqual({ workMinutes: 0, leadHours: 0, complete: true });
  });

  it('counts the work of a step off the main path by case, as the core counts the main path: a step done by sessions adds its share', () => {
    const sessions = { ...map, value_stream: { ...map.value_stream, steps: { ...map.value_stream.steps, x: figures(15.5, 7.5, 1.68) } } };
    expect(countersAt(sessions, tourOf(sessions, 'x'), 0)).toEqual({ workMinutes: 1.68, leadHours: 7.5, complete: true });
  });
});
