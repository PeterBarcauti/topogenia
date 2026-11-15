// measure.ts
// The tools of the measures of NF03 and NF04: a large map laid out by the API, the moment a map is drawn, the frames of the browser during a gesture, the statistics of a series.

import type { Page } from '@playwright/test';
import type { Api } from '../support/api.ts';

/** A map of `lanes` lanes of `perLane` steps, each lane a chain of flows (the shape of the measure of NF05, S-NF-07). */
export async function largeMap(api: Api, code: string, lanes: number, perLane: number): Promise<{ mapId: string; projectCode: string; steps: number; flows: number }> {
  const { mapId } = await api.project(code, `Mesure de ${String(lanes * perLane)} étapes`);
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const ids = [first];
  for (let lane = 1; lane < lanes; lane++) ids.push(await api.lane(mapId, `Couloir ${String(lane + 1)}`));
  for (const lane of ids) {
    let before = '';
    for (let column = 0; column < perLane; column++) {
      const step = await api.step(mapId, lane, column, `Étape ${String(column + 1)}`);
      if (before !== '') await api.flow(mapId, before, step);
      before = step;
    }
  }
  return { mapId, projectCode: code, steps: lanes * perLane, flows: lanes * (perLane - 1) };
}

/** The window of the page as the measures see it. */
interface Measured {
  drawn?: number;
  clicked?: number;
  frames?: number[];
  framing?: boolean;
}

/**
 * From the start of each document of the page: the moment its map is drawn, its steps and its flows all in the page, two
 * frames later (painted), in milliseconds from the start of the navigation; and the moment of the last click.
 */
export async function watchDrawing(page: Page, steps: number, flows: number): Promise<void> {
  await page.addInitScript(([wanted, links]) => {
    const measured = window as unknown as Measured;
    const check = () => {
      if (measured.drawn !== undefined) return;
      if (document.querySelectorAll('.react-flow__node-step').length < wanted || document.querySelectorAll('.react-flow__edge').length < links) return;
      requestAnimationFrame(() => requestAnimationFrame(() => { measured.drawn ??= performance.now(); }));
    };
    new MutationObserver(check).observe(document, { childList: true, subtree: true });
    document.addEventListener('click', () => { measured.clicked = performance.now(); measured.drawn = undefined; }, true);
  }, [steps, flows] as const);
}

/** The moment the map was drawn, once it is. */
export async function drawnAt(page: Page): Promise<{ drawn: number; clicked: number }> {
  await page.waitForFunction(() => (window as unknown as Measured).drawn !== undefined, undefined, { timeout: 60_000 });
  return page.evaluate(() => ({ drawn: (window as unknown as Measured).drawn ?? 0, clicked: (window as unknown as Measured).clicked ?? 0 }));
}

/** Records the frames of the browser while `gesture` runs; gives the intervals between two frames, in milliseconds. */
export async function framesDuring(page: Page, gesture: () => Promise<void>): Promise<number[]> {
  await page.evaluate(() => {
    const measured = window as unknown as Measured;
    measured.frames = [];
    measured.framing = true;
    const tick = (time: number) => {
      measured.frames?.push(time);
      if (measured.framing === true) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });
  await gesture();
  const frames = await page.evaluate(() => new Promise<number[]>((done) => {
    const measured = window as unknown as Measured;
    requestAnimationFrame(() => requestAnimationFrame(() => {
      measured.framing = false;
      done(measured.frames ?? []);
    }));
  }));
  return frames.slice(1).map((time, index) => time - (frames[index] ?? time));
}

/** The value under which a share of a series falls (0.5: the median), the series sorted. */
export function percentile(values: readonly number[], share: number): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.min(sorted.length - 1, Math.max(0, Math.ceil(share * sorted.length) - 1))] ?? Number.NaN;
}

/** A series written for the report: its values rounded to the millisecond, its median and its maximum. */
export const summary = (values: readonly number[]): string =>
  `${values.map((value) => Math.round(value)).join(' / ')} ms (median ${String(Math.round(percentile(values, 0.5)))}, max ${String(Math.round(Math.max(...values)))})`;
