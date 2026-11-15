// resources.spec.ts
// D14, S-D14-03: the application loads nothing from outside its own address (no CDN): its scripts, styles, fonts and icons come from the host, through the home page, a map, a sheet and an export.

import type { Page } from '@playwright/test';
import { expect, openMap, openSheetOf, test } from './support/journey.ts';
import { laneOf } from './support/map-editing.ts';
import { BASE_URL } from './support/run.ts';

/** The addresses that are neither of the application nor kept in the page (data:, blob: of the application). */
function foreign(addresses: readonly string[], origin: string): string[] {
  return addresses.filter((address) => {
    if (address.startsWith('data:')) return false;
    const url = new URL(address.startsWith('blob:') ? address.slice('blob:'.length) : address);
    return url.origin !== origin;
  });
}

/** Every address the page asks for, and every resource the policy of the host refused (a CDN would show here before any request). */
async function watch(page: Page): Promise<{ requests: string[]; refused: () => Promise<string[]> }> {
  const requests: string[] = [];
  page.on('request', (request) => requests.push(request.url()));
  await page.addInitScript(() => {
    const refused: string[] = [];
    (window as unknown as { refused: string[] }).refused = refused;
    document.addEventListener('securitypolicyviolation', (event) => refused.push(`${event.violatedDirective} ${event.blockedURI}`));
  });
  return { requests, refused: () => page.evaluate(() => (window as unknown as { refused: string[] }).refused) };
}

test('the home page, a map with its icons, its fonts and a sheet, then an export: every resource from the host, none refused (S-D14-03)', async ({ page, api }) => {
  const { requests, refused } = await watch(page);
  const { mapId } = await api.project('RES', 'Ressources');
  const lane = await laneOf(api, mapId);
  await api.step(mapId, lane, 0, 'Saisie');
  await page.goto('/');
  await expect(page.getByTestId('app-header')).toBeVisible();
  await openMap(page, mapId, 'RES-E001');
  await openSheetOf(page, 'RES-E001');
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.png').click();
  await download;
  await expect(page.getByText('Export terminé : le fichier est dans le dossier Téléchargements.')).toBeVisible();

  // The fonts of the charter are the files of the host, loaded (spec 9.5: Inter embedded); the icons are drawn in the page.
  const fonts = await page.evaluate(() => performance.getEntriesByType('resource').filter((entry) => /\.(woff2?|ttf)$/.test(entry.name)).map((entry) => entry.name));
  expect(fonts.length).toBeGreaterThan(0);
  expect(fonts.every((font) => font.startsWith(`${BASE_URL}/assets/`))).toBe(true);
  expect(await page.evaluate(() => [...document.fonts].some((face) => face.family.includes('Inter') && face.status === 'loaded'))).toBe(true);
  expect(await page.locator('svg.lucide').count()).toBeGreaterThan(10);

  expect(requests.length).toBeGreaterThan(10);
  expect(foreign(requests, BASE_URL)).toEqual([]);
  expect(await refused()).toEqual([]);

  // The watch sees a resource from elsewhere: an image of another address is refused by the policy, and said.
  await page.evaluate(() => {
    const image = document.createElement('img');
    image.src = 'http://localhost:9/cdn.png';
    document.body.append(image);
  });
  await expect.poll(refused).toEqual([expect.stringContaining('http://localhost:9/cdn.png')]);
});

test('detects an address outside the application, and lets its own, its data and its blobs pass', () => {
  expect(foreign([`${BASE_URL}/assets/index.js`, 'data:image/png;base64,AAAA', `blob:${BASE_URL}/0a1b`, 'http://localhost:9/font.woff2', `blob:http://localhost:9/x`], BASE_URL))
    .toEqual(['http://localhost:9/font.woff2', 'blob:http://localhost:9/x']);
});
