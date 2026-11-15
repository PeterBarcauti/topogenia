// oracle.test.ts
// The drawing of the flows without a route chosen by the user is the one of the version 3.0.2, frozen in `src/test/oracle/routing-3.0.2`: the same on 1 000 maps drawn at random, with and without the band of the value stream layer, whatever the order of the lists (ADR 091).

import { layoutFlows as frozenLayout } from '../../../../../test/oracle/routing-3.0.2/geometry/routing/routing.ts';
import { randomMap, shuffled } from '../../../../../test/randomMaps.ts';
import { STREAM_BAND } from '../constants.ts';
import type { RoutingMap } from './model.ts';
import { layoutFlows } from './routing.ts';

const MAPS = 1000;
/** Each check routes 2 000 maps: a delay of its own, so that a busy machine never fails it (F-34). */
const DELAY = 60_000;
const SEEDS = Array.from({ length: MAPS }, (_, index) => 9001 + index);

/** The whole drawing, the side of what hangs under each step included, written in a stable order. */
function drawingOf(layout: ReturnType<typeof layoutFlows>): string {
  const under = [...layout.under].sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
  return JSON.stringify({ flows: layout.flows, under });
}

/** The seeds whose drawing differs from the one of 3.0.2. */
function changed(mapOf: (seed: number) => RoutingMap): number[] {
  return SEEDS.filter((seed) => {
    const map = mapOf(seed);
    return drawingOf(layoutFlows(map)) !== drawingOf(frozenLayout(map));
  });
}

describe('the drawing of 3.0.2 kept for the flows without a route (ADR 091)', () => {
  it('is the same on 1 000 maps drawn at random', () => {
    expect(changed(randomMap)).toEqual([]);
  }, DELAY);

  it('is the same with the band of the value stream layer', () => {
    expect(changed((seed) => ({ ...randomMap(seed), below: STREAM_BAND }))).toEqual([]);
  }, DELAY);

  it('is the same whatever the order of the lists received', () => {
    expect(changed((seed) => shuffled(randomMap(seed), 3))).toEqual([]);
  }, DELAY);
});
