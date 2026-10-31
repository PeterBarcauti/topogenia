// launch.spec.ts
// Journey of the launch token (spec 5.2, 11; S-11-05): a host of its own, its address opened once by the browser, the cookie of the session, the token spent.

import { expect, test, type Page } from '@playwright/test';
import { startOwnHost, type OwnHost } from './support/host.ts';
import { OWN_HOST_PORT } from './support/run.ts';

const ORIGIN = `http://127.0.0.1:${OWN_HOST_PORT}`;

let host: OwnHost;

test.beforeAll(async () => {
  host = await startOwnHost(OWN_HOST_PORT);
});

test.afterAll(async () => {
  await host.stop();
});

/**
 * The status the host answers to a page the browser asks for. A refusal without a body is shown by Edge as a page of error
 * of its own (net::ERR_HTTP_RESPONSE_CODE_FAILURE) where Chromium shows the empty page: the status is then read by the
 * same browser context, without the page.
 */
async function statusOf(page: Page, url: string): Promise<number | undefined> {
  try {
    return (await page.goto(url))?.status();
  } catch (error) {
    if (!String(error).includes('ERR_HTTP_RESPONSE_CODE_FAILURE')) throw error;
    return (await page.context().request.get(url, { maxRedirects: 0 })).status();
  }
}

// The browser of this journey has never received a launch address.
test.use({ storageState: { cookies: [], origins: [] } });

test('the launch address opens the application once, without the token in the address, by an HttpOnly and Strict cookie (S-11-05)', async ({ browser }) => {
  const context = await browser.newContext();
  const page = await context.newPage();
  // Before the launch: nothing, not even the page.
  expect(await statusOf(page, `${ORIGIN}/`)).toBe(401);

  await page.goto(host.launchAddress);
  await expect(page).toHaveURL(`${ORIGIN}/`);
  await expect(page.getByTestId('home-empty')).toBeVisible();
  const [cookie, ...others] = await context.cookies(ORIGIN);
  expect(others).toEqual([]);
  expect(cookie).toMatchObject({ name: 'desktop_session', httpOnly: true, sameSite: 'Strict', path: '/', expires: -1 });
  // The page reads the profile with the cookie; the cookie is out of reach of its scripts.
  expect(await page.evaluate(() => document.cookie)).toBe('');
  expect((await context.request.get(`${ORIGIN}/api/me`)).status()).toBe(200);

  // The same address read again (history, another user of the machine) opens nothing: the token is spent.
  const other = await browser.newContext();
  const again = await other.newPage();
  expect(await statusOf(again, host.launchAddress)).toBe(401);
  expect(await other.cookies(ORIGIN)).toEqual([]);
  expect((await other.request.get(`${ORIGIN}/api/me`)).status()).toBe(401);
  await other.close();
  await context.close();
});
