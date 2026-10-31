// useHeaderWheel.test.tsx
// The wheel on the fixed lane headers: it scrolls, sideways with Shift, zooms on the pointer with Ctrl between the bounds of the zoom; a field keeps its wheel (spec 3.0 section 15.1, ADR 086).

import { useRef } from 'react';
import { fireEvent, render } from '@testing-library/react';
import { MAX_ZOOM, MIN_ZOOM } from '../model/geometry/constants.ts';
import type { View } from '../model/geometry/viewport.ts';
import { useHeaderWheel, wheelView, type WheelTurn } from './useHeaderWheel.ts';

const view: View = { x: 192, y: 16, zoom: 1 };
const turn = (changes: Partial<WheelTurn>): WheelTurn => ({ deltaX: 0, deltaY: 0, deltaMode: 0, ctrlKey: false, shiftKey: false, pointer: { x: 0, y: 0 }, ...changes });

describe('a turn of the wheel on the headers', () => {
  it('scrolls down and up by the pixels of the turn, whatever the zoom', () => {
    expect(wheelView(view, turn({ deltaY: 100 }))).toEqual({ x: 192, y: -84, zoom: 1 });
    expect(wheelView({ ...view, zoom: 0.5 }, turn({ deltaY: -100 }))).toEqual({ x: 192, y: 116, zoom: 0.5 });
  });

  it('scrolls sideways with Shift, and both ways on a touchpad', () => {
    expect(wheelView(view, turn({ deltaY: 100, shiftKey: true }))).toEqual({ x: 92, y: 16, zoom: 1 });
    expect(wheelView(view, turn({ deltaX: 30, deltaY: 40 }))).toEqual({ x: 162, y: -24, zoom: 1 });
  });

  it('counts a line of Firefox as twenty pixels', () => {
    expect(wheelView(view, turn({ deltaY: 3, deltaMode: 1 }))).toEqual({ x: 192, y: -44, zoom: 1 });
  });

  it('zooms with Ctrl, the point under the pointer kept, between the bounds of the zoom', () => {
    const pointer = { x: 500, y: 300 };
    const out = wheelView(view, turn({ deltaY: 500, ctrlKey: true, pointer }));
    expect(out.zoom).toBeCloseTo(0.5, 6);
    // The point of the map under the pointer stays under it.
    expect((pointer.x - out.x) / out.zoom).toBeCloseTo(pointer.x - view.x, 6);
    expect((pointer.y - out.y) / out.zoom).toBeCloseTo(pointer.y - view.y, 6);
    expect(wheelView(view, turn({ deltaY: 5000, ctrlKey: true })).zoom).toBe(MIN_ZOOM);
    expect(wheelView(view, turn({ deltaY: -5000, ctrlKey: true })).zoom).toBe(MAX_ZOOM);
  });
});

function Headers({ onChange }: { onChange: (next: View) => void }) {
  const root = useRef<HTMLDivElement>(null);
  useHeaderWheel(root, view, onChange);
  return (
    <div ref={root} data-testid="root">
      <div className="lane-heads"><span data-testid="head">A</span><textarea className="nowheel" data-testid="field" /></div>
      <div data-testid="canvas" />
    </div>
  );
}

describe('the wheel listened to on the canvas', () => {
  it('moves the view on a header, not on the canvas of React Flow nor in a field', () => {
    const onChange = vi.fn();
    const { getByTestId } = render(<Headers onChange={onChange} />);
    const event = new WheelEvent('wheel', { deltaY: 100, bubbles: true, cancelable: true });
    getByTestId('head').dispatchEvent(event);
    expect(onChange).toHaveBeenCalledWith({ x: 192, y: -84, zoom: 1 });
    // Not passive: Ctrl and the wheel do not zoom the page.
    expect(event.defaultPrevented).toBe(true);
    fireEvent.wheel(getByTestId('canvas'), { deltaY: 100 });
    fireEvent.wheel(getByTestId('field'), { deltaY: 100 });
    expect(onChange).toHaveBeenCalledTimes(1);
  });
});
