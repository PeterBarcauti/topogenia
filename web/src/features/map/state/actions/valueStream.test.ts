// valueStream.test.ts
// The figures follow the work time of the project (spec 14.2, S6): the map read again when the properties of its project change it, never otherwise; and the layout with the value stream layer: rows of 156 px, the share measured with the label, the figures under the steps with content only.

import { act } from '@testing-library/react';
import { projectProperties } from '../../../project/properties/projectProperties.ts';
import { PROJECT_ID, sampleMap, streamOf } from '../../../../test/maps.ts';
import { layoutMap } from '../../model/mapLayout.ts';
import type { MapEditor } from '../editor.ts';
import { watchWorkTime } from './valueStream.ts';

afterEach(() => {
  projectProperties.setState({ detail: null, open: false });
});

const detailOf = (workday: number, fte = 151.67) =>
  ({ id: PROJECT_ID.toUpperCase(), workday_hours: workday, fte_hours_per_month: fte }) as unknown as NonNullable<ReturnType<typeof projectProperties.getState>['detail']>;

describe('the work time of the project (spec 14.2, S6)', () => {
  it('reads the map again when the properties of its project change the working day or the hours of a full-time equivalent', () => {
    const reload = vi.fn(() => Promise.resolve());
    const editor = { store: { getState: () => ({ map: sampleMap() }) }, reload } as unknown as MapEditor;
    const stop = watchWorkTime(editor);
    act(() => {
      projectProperties.setState({ detail: detailOf(7) });
    });
    expect(reload).not.toHaveBeenCalled();
    act(() => {
      projectProperties.setState({ detail: detailOf(7.5) });
    });
    act(() => {
      projectProperties.setState({ detail: detailOf(7, 160) });
    });
    expect(reload).toHaveBeenCalledTimes(2);
    stop();
    act(() => {
      projectProperties.setState({ detail: detailOf(8) });
    });
    expect(reload).toHaveBeenCalledTimes(2);
  });

  it('leaves the map of another project alone', () => {
    const reload = vi.fn(() => Promise.resolve());
    const editor = { store: { getState: () => ({ map: sampleMap() }) }, reload } as unknown as MapEditor;
    const stop = watchWorkTime(editor);
    act(() => {
      projectProperties.setState({ detail: { ...detailOf(8), id: 'another' } });
    });
    expect(reload).not.toHaveBeenCalled();
    stop();
  });
});

describe('the layout with the value stream layer (spec 14.5)', () => {
  const measure = (text: string) => text.length * 6;
  const t = (key: string) => key;
  const map = sampleMap({
    value_stream: streamOf({ flows: { f4: { flow_id: 'f4', share: 0.15, origin: 'entered' } } }),
  });

  it('gives each row its band, the share after the label of an exit, and nothing of it without the layer', () => {
    const off = layoutMap(map, 'fr', measure);
    const on = layoutMap(map, 'fr', measure, { language: 'fr', workdayHours: 7, t });
    expect([off.lanes.map((band) => band.height), on.lanes.map((band) => band.height)]).toEqual([[120, 120], [156, 156]]);
    expect([off.valueStream, on.valueStream]).toEqual([false, true]);
    const label = (layout: typeof on) => layout.flows.find((flow) => flow.route.id === 'f4')?.route.label?.width ?? 0;
    expect(label(on)).toBeGreaterThan(label(off));
    expect(on.flows.find((flow) => flow.route.id === 'f4')?.share?.text.replace(/\s/g, ' ')).toBe('15 %');
    expect(off.flows.every((flow) => flow.share === null) && off.steps.every((figure) => figure.stream === null)).toBe(true);
  });
});
