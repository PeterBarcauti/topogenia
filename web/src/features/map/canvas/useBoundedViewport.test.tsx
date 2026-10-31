// useBoundedViewport.test.tsx
// The view kept by the editor: bounded at each move, bounded again when the map or the screen changes, back to the start (annex B 2.2, B-2.1-43, B-7-09).

import { act, renderHook } from '@testing-library/react';
import { INITIAL_VIEW } from '../model/geometry/constants.ts';
import type { Extent } from '../model/geometry/viewport.ts';
import { useBoundedViewport } from './useBoundedViewport.ts';

const screen: Extent = { width: 1000, height: 600 };
const large: Extent = { width: 3000, height: 1500 };

describe('the view of the editor', () => {
  it('opens at the start of the map, whatever was shown before (B-7-09)', () => {
    const { result } = renderHook(() => useBoundedViewport(large, screen));
    expect(result.current.view).toEqual(INITIAL_VIEW);
  });

  it('keeps a move bounded to the map (B-2.2-04 to B-2.2-08)', () => {
    const { result } = renderHook(() => useBoundedViewport(large, screen));
    act(() => {
      result.current.change({ x: -800, y: 200, zoom: 1.5 });
    });
    expect(result.current.view).toEqual({ x: -800, y: 16, zoom: 1.5 });
  });

  it('is bounded again when the map or the screen changes (B-2.2-09)', () => {
    const { result, rerender } = renderHook(({ map, size }) => useBoundedViewport(map, size), { initialProps: { map: large, size: screen } });
    act(() => {
      result.current.change({ x: -1900, y: -800, zoom: 1 });
    });
    expect(result.current.view).toEqual({ x: -1900, y: -800, zoom: 1 });
    rerender({ map: { width: 1500, height: 700 }, size: screen });
    expect(result.current.view).toEqual({ x: 1000 - 1500 - 40, y: 600 - 700 - 24, zoom: 1 });
    rerender({ map: { width: 1500, height: 700 }, size: { width: 1800, height: 900 } });
    expect(result.current.view).toEqual({ x: 192, y: 16, zoom: 1 });
  });

  it('goes back to the start of the map at zoom 1 (B-2.1-43)', () => {
    const { result } = renderHook(() => useBoundedViewport(large, screen));
    act(() => {
      result.current.change({ x: -1200, y: -300, zoom: 0.6 });
    });
    act(() => {
      result.current.backToStart();
    });
    expect(result.current.view).toEqual(INITIAL_VIEW);
  });

  it('gives the same view while nothing changes, so that the canvas does not set it again', () => {
    const { result, rerender } = renderHook(() => useBoundedViewport(large, screen));
    const first = result.current.view;
    rerender();
    expect(result.current.view).toBe(first);
  });
});
