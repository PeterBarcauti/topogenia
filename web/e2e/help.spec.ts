// help.spec.ts
// Journeys of the help: the menu « Aide » of the header opens it in a new tab (its entry « Aide en ligne »), served by the host under its policy (no inline script nor style); its summary leads to a chapter and marks it, its search finds a section without its accents and goes to it, a capture is enlarged and drawn; F1 opens it too. An interface in English opens the help in English, whose link leads to the same section in French.

import { chooseInterfaceLanguage, expect, test } from './support/journey.ts';

declare global {
  interface Window {
    violations: string[];
  }
}

test('the help: opened from the Help menu of the header in a new tab under the policy of the host, its summary, its search, a capture enlarged; F1 opens it too', async ({ page, context }) => {
  await context.addInitScript(() => {
    window.violations = [];
    document.addEventListener('securitypolicyviolation', (event) => window.violations.push(`${event.violatedDirective} ${event.blockedURI}`));
  });
  await page.goto('/');
  await expect(page.getByTestId('header-help')).toHaveText('Aide');
  const opened = context.waitForEvent('page');
  await page.getByTestId('header-help').click();
  await page.getByTestId('help-menu-online').click();
  const help = await opened;
  await help.waitForLoadState();
  // The title names the product and its version: « Aide de <produit> 3.1.0 ».
  await expect(help).toHaveTitle(/^Aide de \S+ \d+\.\d+\.\d+$/);
  await expect(help.getByRole('heading', { level: 1 })).toContainText('Vos processus');
  await expect(help.locator('#sidebar .toc-chapter')).toHaveCount(16);

  // The summary leads to a chapter and marks it as the one read.
  await help.locator('#sidebar .toc-chapter > a', { hasText: 'Raccourcis clavier' }).click();
  await expect(help.locator('.toc-chapter.is-current')).toContainText('Raccourcis clavier');
  const shortcut = help.locator('.chapter', { has: help.getByRole('heading', { level: 2, name: 'Raccourcis clavier' }) }).getByRole('row', { name: 'Aide en ligne' });
  await expect(shortcut).toBeVisible();
  await expect(shortcut.locator('kbd')).toHaveText('F1');

  // The search finds a section by its words, without their accents, and goes to it.
  await help.locator('#search').fill('etat cible');
  await expect(help.locator('.result').first()).toContainText('état cible');
  await help.locator('#search').press('Enter');
  await expect(help).toHaveURL(/#s9-9$/);

  // A capture enlarged in its dialog, its image drawn, closed by Escape.
  await help.locator('#s9-9 ~ .shot .zoom').first().click();
  await expect(help.locator('#lightbox')).toBeVisible();
  await expect.poll(() => help.locator('#lightbox-img').evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0)).toBe(true);
  await help.keyboard.press('Escape');
  await expect(help.locator('#lightbox')).toBeHidden();
  expect(await help.evaluate(() => window.violations)).toEqual([]);

  // F1 in the application opens the help as well.
  await page.bringToFront();
  const again = context.waitForEvent('page');
  await page.keyboard.press('F1');
  expect((await again).url()).toMatch(/\/aide\/index\.html$/);
  expect(await page.evaluate(() => window.violations)).toEqual([]);
});

test('the help in English: opened from the Help menu of an interface in English, its search, its link to the same section in French; F1 opens it too', async ({ page, context }) => {
  await context.addInitScript(() => {
    window.violations = [];
    document.addEventListener('securitypolicyviolation', (event) => window.violations.push(`${event.violatedDirective} ${event.blockedURI}`));
  });
  await page.goto('/');
  await chooseInterfaceLanguage(page, 'en');
  await expect(page.getByTestId('header-help')).toHaveText('Help');
  const opened = context.waitForEvent('page');
  await page.getByTestId('header-help').click();
  await page.getByTestId('help-menu-online').click();
  const help = await opened;
  await help.waitForLoadState();
  await expect(help).toHaveURL(/\/aide\/en\/index\.html$/);
  // The title names the product and its version: « <product> 3.1.0 help ».
  await expect(help).toHaveTitle(/^\S+ \d+\.\d+\.\d+ help$/);
  await expect(help.locator('html')).toHaveAttribute('lang', 'en');
  await expect(help.locator('#sidebar .toc-chapter')).toHaveCount(16);

  // The search finds the target state, at the place it has in French, and its capture is drawn.
  await help.locator('#search').fill('target state');
  await expect(help.locator('.result').first()).toContainText(/target state/i);
  await help.locator('#search').press('Enter');
  await expect(help).toHaveURL(/#s9-9$/);
  await expect.poll(() => help.locator('#s9-9 ~ .shot img').first().evaluate(async (image: HTMLImageElement) => {
    image.loading = 'eager';
    await image.decode().catch(() => undefined);
    return image.naturalWidth;
  })).toBeGreaterThan(0);

  // The link of the top bar reads the same section in French.
  await help.locator('.lang-switch').click();
  await expect(help).toHaveURL(/\/aide\/index\.html#s9-9$/);
  await expect(help.locator('html')).toHaveAttribute('lang', 'fr');
  await expect(help.locator('#s9-9')).toContainText('état cible');
  expect(await help.evaluate(() => window.violations)).toEqual([]);

  // F1 in the application opens the help in English as well.
  await page.bringToFront();
  const again = context.waitForEvent('page');
  await page.keyboard.press('F1');
  expect((await again).url()).toMatch(/\/aide\/en\/index\.html$/);
  expect(await page.evaluate(() => window.violations)).toEqual([]);
});
