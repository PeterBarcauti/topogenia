// fluidity.measure.ts
// NF04 (S-NF-06): the view of a map of 300 steps moved by the hand and zoomed by Ctrl and the wheel (spec 3.0 section 15.1) on the real host; the frames of the browser stay at 30 images a second and more.

import type { Page } from '@playwright/test';
import { expect, openMap, test } from '../support/journey.ts';
import { boxOf, canvas, laneHead } from '../support/map-editing.ts';
import { MODE } from '../support/run.ts';
import { framesDuring, largeMap, percentile, summary } from './measure.ts';

/**
 * Fluid: half of the frames within two images of 60 Hz (30 images a second and more), nineteen in twenty within three
 * (no jolt the eye sees); an image lasts 16.7 ms, the clock of the frames moving by a few tenths, hence the half millisecond.
 */
const IMAGE = 1000 / 60;
const MEDIAN_LIMIT = 2 * IMAGE + 0.5;
const P95_LIMIT = 3 * IMAGE + 0.5;
/** One pan and one zoom first, written but not counted (the first drawing of the map at other places and scales), then the series. */
const RUNS = 3;

/** The translation and the scale of the view of React Flow. */
const viewOf = (page: Page) => page.locator('.react-flow__viewport').evaluate((element) => {
  const matrix = new DOMMatrixReadOnly(getComputedStyle(element).transform);
  return { x: matrix.m41, scale: matrix.a };
});

test('a map of 300 steps moves under the hand and zooms by the wheel at 30 images a second and more (S-NF-06)', async ({ page, api }, testInfo) => {
  test.setTimeout(600_000);
  const map = await largeMap(api, 'FLU', 10, 30);
  await openMap(page, map.mapId, 'FLU-E001');
  await expect(page.locator('.react-flow__node-step')).toHaveCount(map.steps);
  const area = await boxOf(canvas(page));
  const centre = { x: area.x + area.width / 2, y: area.y + area.height / 2 };
  // The hand takes the background in the free band above the steps of the first lane: no step under it as it moves sideways.
  const band = { x: centre.x, y: (await boxOf(laneHead(page, 'FLU-C01'))).y + 10 };

  const pans: number[][] = [];
  const zooms: number[][] = [];
  for (let run = 0; run <= RUNS; run++) {
    // The hand (the tool at the opening) drags the background 600 px to the left in 60 moves.
    const before = await viewOf(page);
    pans.push(await framesDuring(page, async () => {
      await page.mouse.move(band.x + 300, band.y);
      await page.mouse.down();
      await page.mouse.move(band.x - 300, band.y, { steps: 60 });
      await page.mouse.up();
    }));
    expect((await viewOf(page)).x).toBeLessThan(before.x - 500);
    // Four turns of Ctrl and the wheel in, four out, on the middle of the canvas (the zoom stops at 2, B-2.2-01: four turns stay under it).
    zooms.push(await framesDuring(page, async () => {
      await page.mouse.move(centre.x, centre.y);
      await page.keyboard.down('Control');
      for (let turn = 0; turn < 4; turn++) await page.mouse.wheel(0, -120);
      await expect.poll(async () => (await viewOf(page)).scale).toBeGreaterThan(1.5);
      for (let turn = 0; turn < 4; turn++) await page.mouse.wheel(0, 120);
      await page.keyboard.up('Control');
    }));
    // Back to the start at zoom 1 (B-2.1-43), where the band of the hand is free again.
    await page.getByTestId('map-back-to-start').click();
    await expect.poll(() => viewOf(page)).toEqual({ x: 192, scale: 1 });
  }

  const line = (name: string, series: number[][]) => series.map((frames, run) =>
    `${name} ${run === 0 ? 'warm-up' : String(run)}: ${String(frames.length)} frames, median ${percentile(frames, 0.5).toFixed(1)} ms, 95th ${percentile(frames, 0.95).toFixed(1)} ms, max ${Math.max(...frames).toFixed(1)} ms`).join('; ');
  const report = `${MODE}: 300 steps, ${String(map.flows)} flows; ${line('pan', pans)}; ${line('zoom', zooms)}; 95th of the pans counted ${summary(pans.slice(1).map((frames) => percentile(frames, 0.95)))}, of the zooms ${summary(zooms.slice(1).map((frames) => percentile(frames, 0.95)))}`;
  testInfo.annotations.push({ type: 'measure', description: report });
  console.log(report);
  for (const frames of [...pans.slice(1), ...zooms.slice(1)]) {
    expect(frames.length).toBeGreaterThan(10);
    expect(percentile(frames, 0.5)).toBeLessThanOrEqual(MEDIAN_LIMIT);
    expect(percentile(frames, 0.95)).toBeLessThanOrEqual(P95_LIMIT);
  }
});
