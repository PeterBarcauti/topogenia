// global-setup.ts
// Before the journeys: the address written by the starter is opened in the browser of the run (Chromium, or Edge) as the default browser would be; in desktop mode the session cookie it gets is kept for every journey.

import { chromium } from '@playwright/test';
import { readLaunchAddress } from './host.ts';
import { BROWSER_OPTIONS, LAUNCH_FILE, SESSION_FILE, VIEWPORT } from './run.ts';

/**
 * Desktop mode: the real exchange of the launch token (spec 5.2), the cookie `desktop_session`, then `/` without the
 * token. Server mode: the application opens under the Windows identity of the browser (Negotiate), no cookie to keep.
 */
export default async function globalSetup(): Promise<void> {
  const address = await readLaunchAddress(LAUNCH_FILE);
  const { launchOptions, channel, ...contextOptions } = { launchOptions: {}, channel: undefined, ...BROWSER_OPTIONS };
  const browser = await chromium.launch({ ...launchOptions, ...(channel === undefined ? {} : { channel }) });
  try {
    const context = await browser.newContext({ viewport: VIEWPORT, ...contextOptions });
    const page = await context.newPage();
    await page.goto(address);
    await page.waitForURL((url) => url.pathname === '/' && url.search === '');
    await page.getByTestId('app-header').waitFor();
    await context.storageState({ path: SESSION_FILE });
  } finally {
    await browser.close();
  }
}
