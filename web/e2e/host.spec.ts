// host.spec.ts
// The bench of the journeys (annex B 0): a real Chromium against the real host, the reference viewport, the built interface served by the host at its one address.

import { expect, test } from './support/journey.ts';
import { BASE_URL, VIEWPORT } from './support/run.ts';

test('a real Chromium drives the real host, which knows the user by the browser alone (B-0-06)', async ({ page, browserName, api, playwright }) => {
  expect(browserName).toBe('chromium');
  const reply = await page.goto('/');
  expect(reply?.status()).toBe(200);
  // The security headers of the host, not those of a server of development.
  expect(reply?.headers()['content-security-policy']).toContain("script-src 'self'");
  await expect(page.getByTestId('home-empty')).toBeVisible();
  const me = await api.get<{ login: string; is_admin: boolean }>('/api/me');
  expect(me.login).not.toBe('');
  expect(me.is_admin).toBe(true);
  // A client without what the browser holds gets nothing: no session cookie in desktop mode, no Windows identity in server mode.
  const anonymous = await playwright.request.newContext({ ignoreHTTPSErrors: true, storageState: { cookies: [], origins: [] } });
  expect((await anonymous.get(`${BASE_URL}/api/me`)).status()).toBe(401);
  await anonymous.dispose();
});

test('the reference viewport is 1366x768 (B-0-07)', async ({ page }) => {
  await page.goto('/');
  expect(page.viewportSize()).toEqual(VIEWPORT);
  expect(await page.evaluate(() => [window.innerWidth, window.innerHeight])).toEqual([VIEWPORT.width, VIEWPORT.height]);
});

test('the built interface is served by the host: one application at one address, deep links included (B-0-08)', async ({ page, api }) => {
  const scripts: string[] = [];
  page.on('response', (response) => {
    if (response.request().resourceType() === 'script') scripts.push(response.url());
  });
  const { mapId } = await api.project('BUILD', 'Construction');
  await page.goto(`/maps/${mapId}`);
  await expect(page.locator('.react-flow')).toBeVisible();
  // The bundle of the build (hashed names under /assets), from the address of the API itself.
  expect(scripts.length).toBeGreaterThan(0);
  for (const script of scripts) expect(script).toMatch(new RegExp(`^${BASE_URL}/assets/[\\w.-]+\\.js$`));
  expect(new URL(page.url()).origin).toBe(BASE_URL);
});
