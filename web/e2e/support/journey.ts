// journey.ts
// The test of the journeys: before each one, an empty base and no connector of the assistant, and the API of the session at hand; plus the readers of the map shown.

import { test as base, expect, type Locator, type Page } from '@playwright/test';
import { Api, pageTransport, requestTransport } from './api.ts';
import { BASE_URL, MODE } from './run.ts';

export { expect };

/**
 * Every journey starts from an empty base (no project, empty trash, no connector of the assistant), its profile in French, and may lay out its data by
 * `api`: by the request of Playwright in desktop mode, from a page of its own in server mode (see `pageTransport`).
 */
export const test = base.extend<{ api: Api }>({
  api: [
    async ({ request, browser }, provide) => {
      const context = MODE === 'server' ? await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true }) : null;
      const page = context === null ? null : await context.newPage();
      await page?.goto('/api/health');
      const api = new Api(page === null ? requestTransport(request) : pageTransport(page));
      await api.clearProjects();
      await api.resetLanguage();
      await api.resetAssistant();
      await provide(api);
      await context?.close();
    },
    { auto: true },
  ],
});

/** The step of the map shown, by its readable code (`SALES-E004`). */
export const stepOf = (page: Page, code: string): Locator => page.getByTestId(`step-${code}`);

/** Opens the sheet of a step as a user does (AR-151): a click chooses the step, Enter opens its sheet. */
export async function openSheetOf(page: Page, code: string): Promise<void> {
  await stepOf(page, code).click();
  await page.keyboard.press('Enter');
}

/** Opens a map and waits until its steps are drawn (the first code given), or its empty map is shown. */
export async function openMap(page: Page, mapId: string, firstCode?: string): Promise<void> {
  await page.goto(`/maps/${mapId}`);
  if (firstCode !== undefined) await expect(stepOf(page, firstCode)).toBeVisible();
  else await expect(page.locator('.react-flow')).toBeVisible();
}

/** The language of the interface chosen in the menu of the user (ADR 083), its name in the interface shown before. */
export async function chooseInterfaceLanguage(page: Page, code: string, menuItem = 'Langue de l’interface'): Promise<void> {
  await page.getByTestId('header-user').click();
  await page.getByRole('menuitem', { name: menuItem }).click();
  await page.getByTestId(`interface-language-${code}`).click();
  await expect(page.locator('html')).toHaveAttribute('lang', code);
}

/** The language of the process open chosen in the list of the header (ADR 083). */
export async function chooseProcessLanguage(page: Page, code: string): Promise<void> {
  await page.getByTestId('process-language').click();
  await page.getByTestId(`process-language-${code}`).click();
  await expect(page.getByTestId('process-language-list')).toHaveCount(0);
}
