// usePresenterCamera.test.ts
// The camera of a presentation moved by its scenes alone (relecture 6: R6-03): the stage drawn again (a move of the mouse, a sentence, the bar hidden) does not start its travel again, a change of speed neither, and the map moved by hand in pause stays where it was put until the scene changes.

import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { MapEditor } from '../state/editor.ts';
import { createPresenterStore } from './presenterStore.ts';
import type { Presentation } from './usePresentation.ts';
import { boxesOf, usePresenterCamera } from './usePresenterCamera.ts';

interface View {
  readonly x: number;
  readonly y: number;
  readonly zoom: number;
}

/** The view of a React Flow that travels: a travel asked leaves the view where it is, on its way, until the test lets it arrive; a cut moves it at once. */
const flow = vi.hoisted(() => {
  const state: { view: View; asked: { view: View; duration: number }[] } = { view: { x: 0, y: 0, zoom: 1 }, asked: [] };
  return {
    state,
    getViewport: () => state.view,
    setViewport: (view: View, options: { duration: number }) => {
      state.asked.push({ view, duration: options.duration });
      if (options.duration === 0) state.view = view;
      return Promise.resolve(true);
    },
  };
});

vi.mock('@xyflow/react', () => ({
  useReactFlow: () => flow,
  useStore: (selector: (state: { width: number; height: number }) => number) => selector({ width: 1200, height: 800 }),
}));

afterEach(() => {
  flow.state.view = { x: 0, y: 0, zoom: 1 };
  flow.state.asked = [];
});

const box = (x: number) => ({ x, y: 100, width: 160, height: 64 });

/** Two steps far from each other, the second out of the view that frames the first: each scene has its own frame. */
const presentationOf = (): Presentation => ({
  scenes: [{ kind: 'opening' }, { kind: 'step', stop: 0 }, { kind: 'passage', stop: 0 }, { kind: 'step', stop: 1 }, { kind: 'closing' }],
  layout: { steps: [{ step: { id: 'a' }, box: box(200) }, { step: { id: 'b' }, box: box(4200) }], width: 4600, height: 400 },
  tour: { stops: [{ stepId: 'a', next: { flows: [], to: 'b' } }, { stepId: 'b', next: null }] },
  map: { flows: [] },
  traces: new Map(),
}) as unknown as Presentation;

/** A view that shows none of the two steps: the camera on its way, or the map dragged aside by hand. */
const ELSEWHERE: View = { x: -9000, y: -300, zoom: 1 };

/** The camera of a presentation started, and the stage drawn again as `usePresentation` gives it: a new object, the same map, layout and tour. */
function camera() {
  const editor = { presenter: createPresenterStore() } as unknown as MapEditor;
  const presentation = presentationOf();
  editor.presenter.getState().start(null);
  const { rerender } = renderHook((props: { presentation: Presentation }) => {
    usePresenterCamera(editor, props.presentation);
  }, { initialProps: { presentation } });
  const drawnAgain = () => {
    rerender({ presentation: { ...presentation } });
  };
  const moveTo = (stop: number) => {
    act(() => {
      editor.presenter.getState().move({ stop }, presentation.scenes);
    });
  };
  return { editor, drawnAgain, moveTo };
}

describe('the camera of a presentation moved by its scenes alone (relecture 6: R6-03)', () => {
  it('frames the opening at once, travels to a scene once, and does not start its travel again when the stage is drawn again or the speed changes', () => {
    const { editor, drawnAgain, moveTo } = camera();
    expect(flow.state.asked.map((asked) => asked.duration)).toEqual([0]);
    moveTo(1);
    expect([flow.state.asked.length, (flow.state.asked[1]?.duration ?? 0) > 0]).toEqual([2, true]);
    // The camera is on its way: the view shows the step of the scene not yet.
    flow.state.view = ELSEWHERE;
    drawnAgain();
    drawnAgain();
    act(() => {
      editor.presenter.getState().faster();
    });
    expect(flow.state.asked.length).toBe(2);
  });

  it('leaves the map where a hand moved it in pause until the scene changes, then frames the new scene', () => {
    const { editor, drawnAgain, moveTo } = camera();
    act(() => {
      editor.presenter.getState().toggle();
    });
    moveTo(0);
    flow.state.view = flow.state.asked.at(-1)?.view ?? flow.state.view;
    const asked = flow.state.asked.length;
    // The presenter drags the map to show another place of it.
    flow.state.view = ELSEWHERE;
    drawnAgain();
    expect([flow.state.asked.length, flow.state.view]).toEqual([asked, ELSEWHERE]);
    moveTo(1);
    expect(flow.state.asked.length).toBe(asked + 1);
  });
});

describe('what each scene frames on a tour that jumps (lot J10, independent review, S2)', () => {
  /** The end of a branch `x`, whose flow closes on `j` beside it; the story then comes back to the decision `d`, far away, and jumps to `far`. */
  const jumping = (): Presentation => ({
    scenes: [
      { kind: 'opening' }, { kind: 'step', stop: 0 }, { kind: 'passage', stop: 0 }, { kind: 'step', stop: 1 }, { kind: 'passage', stop: 1 }, { kind: 'step', stop: 2 },
      { kind: 'closing' },
    ],
    layout: { steps: [{ step: { id: 'x' }, box: box(200) }, { step: { id: 'j' }, box: box(500) }, { step: { id: 'd' }, box: box(4200) }, { step: { id: 'far' }, box: box(8000) }], width: 8400, height: 400 },
    tour: { stops: [{ stepId: 'x', next: { flows: ['c'], to: 'd' } }, { stepId: 'd', next: { flows: [], to: 'far' }, again: true }, { stepId: 'far', next: null }] },
    map: { flows: [{ id: 'c', source_step_id: 'x', target_step_id: 'j' }] },
    traces: new Map([['c', { points: [{ x: 360, y: 132 }, { x: 500, y: 132 }], lengths: [0, 140], total: 140 }]]),
  }) as unknown as Presentation;

  const framed = (scene: number) => boxesOf(jumping(), scene).map((rect) => rect.x).sort((a, b) => a - b);

  it('frames a step with the flow that leaves it and the step that flow leads to, never the stop the story jumps to next', () => {
    // The step, the end of its flow and the trace of that flow; the decision the story comes back to is not in the frame.
    expect(framed(1)).toEqual([200, 360, 500]);
    expect(framed(3)).toEqual([4200]);
    expect(framed(5)).toEqual([8000]);
  });

  it('frames a passage by its flows and their ends, and a jump without a flow by where it lands', () => {
    expect(framed(2)).toEqual([200, 360, 500]);
    expect(framed(4)).toEqual([8000]);
    // The whole map at the opening and the closing.
    expect([framed(0), framed(6)]).toEqual([[200, 500, 4200, 8000], [200, 500, 4200, 8000]]);
  });
});
