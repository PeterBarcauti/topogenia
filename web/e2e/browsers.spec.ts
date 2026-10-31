// browsers.spec.ts
// NF01, S-NF-01: the journeys play in Chromium, and with E2E_BROWSER=msedge in the Edge installed on the machine; this one says which browser it is and that the application works in it.

import { expect, openMap, openSheetOf, test } from './support/journey.ts';
import { laneOf } from './support/map-editing.ts';
import { BROWSER_CHANNEL } from './support/run.ts';

test('the browser of the run is the one asked, Chromium or Edge, and the application opens a map and its sheet in it (S-NF-01)', async ({ page, api, browser }, testInfo) => {
  await page.goto('/');
  // The brands the browser gives of itself: without a window, Edge writes no « Edg/ » in its user agent, but names itself here.
  const brands = await page.evaluate(() => (navigator as Navigator & { userAgentData?: { brands: { brand: string }[] } }).userAgentData?.brands.map((item) => item.brand) ?? []);
  testInfo.annotations.push({ type: 'browser', description: `${BROWSER_CHANNEL ?? 'chromium'} ${browser.version()} | ${brands.join(', ')}` });
  expect(brands).toContain('Chromium');
  if (BROWSER_CHANNEL === 'msedge') expect(brands).toContain('Microsoft Edge');
  else expect(brands).not.toContain('Microsoft Edge');
  expect(Number(browser.version().split('.')[0])).toBeGreaterThanOrEqual(120);

  const { mapId } = await api.project('NAV', 'Navigateurs');
  await api.step(mapId, await laneOf(api, mapId), 0, 'Saisie');
  await openMap(page, mapId, 'NAV-E001');
  await openSheetOf(page, 'NAV-E001');
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
});
