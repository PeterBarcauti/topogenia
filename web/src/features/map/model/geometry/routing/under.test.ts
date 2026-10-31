// under.test.ts
// Where what hangs under a step goes, on maps routed for real: centred, or aside of the axis of the bottom point, away from the flows that run under the step (AR-58, AR-128).

import { contextOf, type RoutingFlow, type RoutingStep, type StepKind } from './model.ts';
import { layoutFlows } from './routing.ts';
import { underSides } from './under.ts';

// Lines: l0 from 0 (steps 28 to 92, free band to 120), l1 from 120, l2 from 240, l3 from 360. Column c: axis at 115 + 220c.
const LANES = [0, 1, 2, 3].map((order) => ({ id: `l${order}`, order }));

const step = (id: string, lane: number, column: number, type: StepKind = 'task'): RoutingStep => ({ id, laneId: `l${lane}`, column, row: 0, type });
const flow = (id: string, sourceId: string, targetId: string): RoutingFlow => ({ id, sourceId, targetId, label: null });

const sidesOf = (steps: RoutingStep[], flows: RoutingFlow[]) => Object.fromEntries(layoutFlows({ lanes: LANES, steps, flows }).under);

describe('what hangs under a step (AR-58, AR-128)', () => {
  it('stays centred when no flow uses the bottom point', () => {
    expect(sidesOf([step('A', 0, 0), step('B', 0, 1), step('C', 1, 2)], [flow('f1', 'A', 'B'), flow('f2', 'B', 'C')])).toEqual({});
  });

  it('goes left of the axis of a step whose flow back comes from the right under it, right of the gateway it leaves (the demonstration)', () => {
    // E1 -> E2 -> E3 -> E4 « Note conforme ? », E4 -> E2 « non »: the flow back reaches E2 by the bottom, from the aisle on its right.
    const steps = [step('E1', 0, 0, 'start'), step('E2', 0, 1), step('E3', 1, 2), step('E4', 2, 3, 'decision')];
    const flows = [flow('f1', 'E1', 'E2'), flow('f2', 'E2', 'E3'), flow('f3', 'E3', 'E4'), flow('no', 'E4', 'E2')];
    const routed = layoutFlows({ lanes: LANES, steps, flows }).flows.find((item) => item.id === 'no');
    expect([routed?.sourceSide, routed?.targetSide, routed?.curve]).toEqual(['bottom', 'bottom', null]);
    expect(sidesOf(steps, flows)).toEqual({ E2: 'left', E4: 'right' });
  });

  it('on one line, the loop back: left under its target, right under its source', () => {
    const steps = [step('A', 0, 0), step('B', 0, 1), step('C', 0, 2)];
    const flows = [flow('f1', 'A', 'B'), flow('f2', 'B', 'C'), flow('back', 'C', 'A')];
    expect(sidesOf(steps, flows)).toEqual({ A: 'left', C: 'right' });
  });

  it('right of the axis for a flow straight down, the step below it untouched (it is reached by its top)', () => {
    expect(sidesOf([step('X', 0, 0), step('Y', 1, 0)], [flow('down', 'X', 'Y')])).toEqual({ X: 'right' });
  });

  it('right of the axis when flows run under the step both ways', () => {
    // B receives the loop back of C, from its right, and sends its own back to A, to its left.
    const steps = [step('A', 0, 0), step('B', 0, 1), step('C', 0, 2)];
    const flows = [flow('f1', 'A', 'B'), flow('f2', 'B', 'C'), flow('cb', 'C', 'B'), flow('ba', 'B', 'A')];
    expect(sidesOf(steps, flows).B).toBe('right');
  });
});

describe('the way a flow runs from the bottom point', () => {
  const context = contextOf({ lanes: LANES, steps: [step('A', 0, 0), step('B', 1, 1)], flows: [] });
  const bottom = (points: { x: number; y: number }[], isCurve = false) =>
    underSides(context, [{ sourceId: 'A', targetId: 'B', sourceSide: 'bottom', targetSide: 'left', points, isCurve }]).get('A');

  it('a jog onto a track of the axis is no way: straight down', () => {
    expect(bottom([{ x: 115, y: 92 }, { x: 115, y: 100 }, { x: 121, y: 100 }, { x: 121, y: 180 }, { x: 250, y: 180 }])).toBe('right');
  });

  it('a turn once out of the free band under the step is no way either', () => {
    expect(bottom([{ x: 115, y: 92 }, { x: 115, y: 160 }, { x: 400, y: 160 }])).toBe('right');
  });

  it('a turn to the right in the free band, or a curve to the right, sends it left of the axis', () => {
    expect(bottom([{ x: 115, y: 92 }, { x: 115, y: 106 }, { x: 225, y: 106 }, { x: 225, y: 180 }])).toBe('left');
    expect(bottom([{ x: 115, y: 92 }, { x: 555, y: 92 }], true)).toBe('left');
    expect(bottom([{ x: 115, y: 92 }, { x: 5, y: 92 }], true)).toBe('right');
  });

  it('a step that is not on the map is left out', () => {
    expect(underSides(context, [{ sourceId: 'Z', targetId: 'Z', sourceSide: 'bottom', targetSide: 'bottom', points: [{ x: 0, y: 0 }], isCurve: false }])).toEqual(new Map());
  });
});
