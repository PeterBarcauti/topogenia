// opening.measure.ts
// NF03 (S-NF-04, S-NF-05): a map of 150 steps opened on the real host, measured in the browser; under 1 s in desktop mode, under 2 s in server mode.

import { expect, test } from '../support/journey.ts';
import { MODE } from '../support/run.ts';
import { drawnAt, largeMap, percentile, summary, watchDrawing } from './measure.ts';

/** NF03: the opening of a map of 150 steps. */
const LIMIT = MODE === 'server' ? 2000 : 1000;
/** One opening before the series, not counted (the first reading of the host and of the bundle), then the series. */
const RUNS = 5;

test('a map of 150 steps opens in less than 1 s in desktop mode, 2 s in server mode: by its address, and from the home page (S-NF-04, S-NF-05)', async ({ page, api }, testInfo) => {
  test.setTimeout(600_000);
  const map = await largeMap(api, 'MES', 5, 30);
  await watchDrawing(page, map.steps, map.flows);

  // By its address: from the start of the navigation (the page, the bundle, the profile, the map) to the map drawn.
  const byAddress: number[] = [];
  for (let run = 0; run <= RUNS; run++) {
    await page.goto(`/maps/${map.mapId}`);
    const { drawn } = await drawnAt(page);
    if (run > 0) byAddress.push(drawn);
  }

  // From the home page: from the click on the project to the map drawn.
  const fromHome: number[] = [];
  for (let run = 0; run <= RUNS; run++) {
    await page.goto('/');
    const link = page.getByTestId(`project-${map.projectCode}-open`);
    await expect(link).toBeVisible();
    await link.click();
    const { drawn, clicked } = await drawnAt(page);
    if (run > 0) fromHome.push(drawn - clicked);
  }

  const report = `${MODE}: 150 steps, ${String(map.flows)} flows; by its address ${summary(byAddress)}; from the home page ${summary(fromHome)}; limit ${String(LIMIT)} ms`;
  testInfo.annotations.push({ type: 'measure', description: report });
  console.log(report);
  expect(percentile(byAddress, 0.5)).toBeLessThan(LIMIT);
  expect(percentile(fromHome, 0.5)).toBeLessThan(LIMIT);
});
