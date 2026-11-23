// PresenterLaneNames.test.tsx
// The lane names of the overview of a presentation (F-209): the number of lines follows the height of each band, which follows the zoom; a band too low for one line leaves its name masked.

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { LaneBand } from '../model/mapLayout.ts';
import { PresenterLaneNames } from './PresenterLaneNames.tsx';

const band = (id: string, top: number, height: number): LaneBand => ({ lane: { id }, name: `Lane ${id}`, untranslated: false, tint: 1, parity: 'even', top, rows: 1, height, stream: null }) as unknown as LaneBand;
const LANES = [band('a', 0, 200), band('b', 200, 200)];

const drawn = (zoom: number, active: string | null = null) => {
  render(<PresenterLaneNames lanes={LANES} active={active} dim={false} y={0} zoom={zoom} />);
  return [...screen.getByTestId('presenter-lanes').querySelectorAll<HTMLElement>('.presenter-lane')].map((lane) => ({
    top: lane.style.top, height: lane.style.height, lines: lane.querySelector('span')?.style.getPropertyValue('--lines') ?? null, masked: lane.classList.contains('is-masked'),
  }));
};

describe('the lane names of the overview (F-209)', () => {
  it('gives each name three lines in a high band', () => {
    expect(drawn(1)).toEqual([{ top: '0px', height: '200px', lines: '3', masked: false }, { top: '200px', height: '200px', lines: '3', masked: false }]);
  });

  it('gives fewer lines as the zoom lowers the bands: 40 px two lines, 20 px one', () => {
    expect(drawn(0.2).map((lane) => [lane.height, lane.lines, lane.masked])).toEqual([['40px', '2', false], ['40px', '2', false]]);
  });

  it('masks a name whose band is lower than one line, the band and its colour staying', () => {
    expect(drawn(0.08).map((lane) => [lane.height, lane.lines, lane.masked])).toEqual([['16px', '0', true], ['16px', '0', true]]);
  });

  it('counts the taller line of the lane lit: at 36 px the lit lane keeps one line, an other lane two', () => {
    expect(drawn(0.18, 'a').map((lane) => [lane.lines, lane.masked])).toEqual([['1', false], ['2', false]]);
  });
});
