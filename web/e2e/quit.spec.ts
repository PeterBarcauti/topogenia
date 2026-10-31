// quit.spec.ts
// Journey of the command « Quit » of the desktop mode (spec 5.2, S-5.2-16): on a host of its own, the menu of the account, a confirmation, the page that says the application stopped, then the host stopped and its address silent; on the host of the run, Annuler leaves it running, and the server mode has no such menu.

import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import { startOwnHost, type OwnHost } from './support/host.ts';
import { MODE, QUIT_HOST_PORT } from './support/run.ts';

const ORIGIN = `http://127.0.0.1:${String(QUIT_HOST_PORT)}`;

/** The texts of the command name the product, written only in the language files: a word stands in its place. */
const QUIT_ENTRY = new RegExp('^Quitter \\w+$');
const QUIT_TITLE = new RegExp('^Quitter \\w+ \\?$');

/** Whether an address still answers (any status): false once nothing listens on its port. */
async function answers(request: APIRequestContext, url: string): Promise<boolean> {
  try {
    await request.get(url, { timeout: 5_000 });
    return true;
  } catch {
    return false;
  }
}

/** The menu of the account opened from the header, then its command « Quitter », which asks first. */
async function askToQuit(page: Page): Promise<Locator> {
  const account = page.getByTestId('header-user');
  await expect(account).toHaveRole('button');
  await account.click();
  await page.getByRole('menu', { name: 'Menu du compte' }).getByRole('menuitem', { name: QUIT_ENTRY }).click();
  const confirm = page.getByRole('dialog', { name: QUIT_TITLE });
  await expect(confirm).toContainText('Les modifications sont enregistrées au fil de la saisie.');
  return confirm;
}

test.describe('on a host of its own, which the journey stops', () => {
  let host: OwnHost;

  test.beforeAll(async () => {
    host = await startOwnHost(QUIT_HOST_PORT);
  });

  test.afterAll(async () => {
    await host.stop();
  });

  // The browser of this journey opens the launch address of its own host.
  test.use({ storageState: { cookies: [], origins: [] } });

  test('« Quitter » in the menu of the account, confirmed: the page says the application stopped, then the host stops and its address answers no more (S-5.2-16)', async ({ page }) => {
    await page.goto(host.launchAddress);
    await expect(page).toHaveURL(`${ORIGIN}/`);
    await expect(page.getByTestId('home')).toBeVisible();
    const request = page.context().request;
    expect(await answers(request, `${ORIGIN}/api/me`)).toBe(true);

    const confirm = await askToQuit(page);
    await confirm.getByRole('button', { name: 'Quitter', exact: true }).click();
    // The page says it in the place of the whole application: no header, no screen left to call the host.
    const stopped = page.getByTestId('page-stopped');
    await expect(stopped).toHaveRole('status');
    await expect(stopped).toContainText(new RegExp('^\\w+ est arrêté\\. Cet onglet peut être fermé\\.'));
    await expect(page.getByTestId('app-header')).toHaveCount(0);
    // The process of the host ends by itself, with the code of a normal stop, within the time bound; its port is closed.
    const ended = await Promise.race([host.exited, new Promise<string>((done) => setTimeout(() => { done('still running'); }, 30_000))]);
    expect(ended).toBe(0);
    expect(await answers(request, `${ORIGIN}/api/me`)).toBe(false);
    await expect(stopped).toBeVisible();
  });
});

test('the account in the header: in desktop mode a menu whose « Quitter » asks first, Annuler sending nothing; in server mode a menu with the language of the interface only (S-5.2-16, ADR 083)', async ({ page }) => {
  const quits: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/desktop/quit')) quits.push(request.method());
  });
  await page.goto('/');
  await expect(page.getByTestId('home')).toBeVisible();
  const account = page.getByTestId('header-user');
  await expect(account).toContainText(new RegExp('\\w'));
  if (MODE === 'desktop') {
    const confirm = await askToQuit(page);
    await confirm.getByRole('button', { name: 'Annuler' }).click();
    await expect(confirm).toHaveCount(0);
    expect(quits).toEqual([]);
  } else {
    // No « Quitter »: the tab of the browser closes the application of the server, which knows no such command; the menu
    // holds the language of the interface alone (ADR 083).
    await account.click();
    await expect(page.getByRole('menu').getByRole('menuitem')).toHaveText(['Langue de l’interface']);
    await page.keyboard.press('Escape');
    const refused = await page.evaluate(async () => (await fetch('/api/desktop/quit', { method: 'POST', headers: { 'X-Requested-With': 'XMLHttpRequest' } })).status);
    expect(refused).toBe(404);
  }
  // The host of the run still answers, and the application still runs.
  await page.reload();
  await expect(page.getByTestId('home')).toBeVisible();
  await expect(page.getByTestId('page-stopped')).toHaveCount(0);
});
