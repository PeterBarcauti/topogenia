// home-update-34.spec.ts
// Journeys of the home page and of the updates of the 3.4 (lot 3, ADR 111): the starts of the main button, the sample opening its map, « New project » with its code proposed, « Check for updates » in the Help menu and in the menu of the account, the reminder of an old version proved with the clock of the browser, no request of the page leaving the host, and the views of a user who is not administrator or without links.

import { readFileSync } from 'node:fs';
import type { BrowserContext, Page, Request } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, test } from './support/journey.ts';
import { BASE_URL, MODE } from './support/run.ts';

type Me = components['schemas']['ProfileView'];
type Links = NonNullable<Me['links']>;
type ProjectItem = components['schemas']['ProjectListItem'];

const MANIFEST = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf-8')) as { version: string; releaseDate: string };
const BASE_ORIGIN = new URL(BASE_URL).origin;
const KIND = MODE === 'server' ? 'server' : 'portable';
const CLOSED_KEY = 'app.updateReminderClosed';

/** The profile as the host gives it; the addresses of the site are read from it, never written in a journey. */
async function profileOf(api: Api): Promise<{ me: Me; links: Links }> {
  const me = await api.get<Me>('/api/me');
  if (me.links == null) throw new Error('The host gives no links: SiteLinks is false for this run.');
  return { me, links: me.links };
}

/** The page of the versions the browser must be sent to, in French or in English, the version and the kind of installation in the fragment. */
const versionsAt = (links: Links, english = false): string => `${links.site}/${english ? 'en/' : ''}versions/#v=${MANIFEST.version}&d=${KIND}`;

/** The profile read, changed, given to the page (the role or the links of another user are simulated, as the journeys of the properties do). */
async function simulate(page: Page, me: Me, change: Partial<Me>): Promise<void> {
  await page.route('**/api/me', async (route) => {
    if (route.request().method() !== 'GET') await route.fallback();
    else await route.fulfill({ json: { ...me, ...change } });
  });
}

/** What the guard saw leave the host: the addresses served empty (the site and the Store) and those refused. */
interface Guard {
  readonly served: string[];
  readonly refused: string[];
}

/**
 * Nothing goes out to internet: a request to another origin than the host is served an empty page when it is the site
 * or the Store (the tab the browser opens), refused otherwise.
 */
async function guardNetwork(context: BrowserContext, links: Links): Promise<Guard> {
  const sites = new Set([new URL(links.site).origin, new URL(links.store_page).origin]);
  const guard: Guard = { served: [], refused: [] };
  await context.route((url) => url.origin !== BASE_ORIGIN, async (route) => {
    const address = route.request().url();
    if (sites.has(new URL(address).origin)) {
      guard.served.push(address);
      await route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>site</title>' });
    } else {
      guard.refused.push(address);
      await route.abort();
    }
  });
  return guard;
}

/** Notes every violation of the policy of the page (`connect-src 'self'`), in the capture phase, from before the page's own scripts. */
async function recordViolations(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const seen: string[] = [];
    (window as unknown as { violations: string[] }).violations = seen;
    document.addEventListener('securitypolicyviolation', (event) => { seen.push(`${event.violatedDirective} ${event.blockedURI}`); }, true);
  });
}

const violationsOf = (page: Page): Promise<string[]> => page.evaluate(() => (window as unknown as { violations: string[] }).violations);

/** The tab a gesture opens: exactly one, at the address given, which has no window of the application to reach. */
async function expectTabAt(context: BrowserContext, gesture: () => Promise<void>, address: string): Promise<void> {
  const tabs: Page[] = [];
  const listen = (tab: Page) => { tabs.push(tab); };
  context.on('page', listen);
  await gesture();
  await expect.poll(() => tabs.length).toBe(1);
  const tab = tabs[0];
  if (tab === undefined) throw new Error('No tab opened.');
  await expect.poll(() => tab.url()).toBe(new URL(address).href);
  await tab.waitForLoadState('domcontentloaded');
  expect(await tab.evaluate(() => window.opener === null)).toBe(true);
  context.off('page', listen);
  await tab.close();
}

const helpUpdate = async (page: Page): Promise<void> => {
  await page.getByTestId('header-help').click();
  await page.getByTestId('help-menu-update').click();
};

const accountUpdate = async (page: Page): Promise<void> => {
  await page.getByTestId('header-user').click();
  await page.getByTestId('account-menu-update').click();
};

/** « New project » then its first line, the blank map. */
async function openNewProject(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await page.getByTestId('home-start-menu-blank').click();
  await expect(page.getByTestId('new-project')).toBeVisible();
}

/** The day `days` after a day `yyyy-mm-dd`, at noon of the local calendar (no hour of the changes of time can move it). */
function dayAfter(day: string, days: number): Date {
  const [year = 0, month = 1, date = 1] = day.split('-').map(Number);
  return new Date(year, month - 1, date + days, 12, 0, 0);
}

const isoDay = (date: Date): string =>
  `${String(date.getFullYear())}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

/** The home page at a day of the clock of the browser, which the application reads, the host's date being left as it is. */
async function homeAt(page: Page, date: Date): Promise<void> {
  await page.clock.setFixedTime(date);
  await page.goto('/');
  await expect(page.getByTestId('home')).toBeVisible();
}

const REMINDER = (page: Page) => page.getByTestId('update-reminder');

test('a first launch on new data: the main button offers the starts, the sample opens its map', async ({ page, api }) => {
  await page.goto('/');
  const main = page.getByRole('button', { name: 'Décrire un processus' });
  await expect(main).toBeEnabled();
  await expect(main).toHaveAttribute('aria-haspopup', 'menu');
  await expect(main).toHaveAttribute('aria-expanded', 'false');
  await main.click();
  await expect(main).toHaveAttribute('aria-expanded', 'true');
  const menu = page.getByRole('menu', { name: 'Commencer un projet' });
  const sample = MODE === 'server' ? [] : ['Projet d’exemple'];
  await expect(menu.locator('.menu-label')).toHaveText([...sample, 'Galerie de modèles', 'Carte vide…', 'Décrire avec l’IA', 'Ouvrir un fichier…']);
  await expect(page.getByTestId('home-start-menu-gallery')).toHaveAttribute('title', 'S’ouvre dans le navigateur.');
  // The lines that open a dialog: the description with the assistant, the file.
  await page.getByTestId('home-start-menu-describe').click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await main.click();
  await page.getByTestId('home-start-menu-open').click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await main.click();
  if (MODE === 'server') {
    // No sample on the server (the route answers 404): the line is not offered and nothing was created.
    await expect(page.getByTestId('home-start-menu-sample')).toHaveCount(0);
    expect(await api.get<ProjectItem[]>('/api/projects')).toEqual([]);
    return;
  }
  await page.getByTestId('home-start-menu-sample').click();
  await expect(page.getByText('Projet d’exemple CONGES créé.')).toBeVisible();
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveText('Demande de congés');
  await expect(page.getByTestId('step-CONGES-E001')).toContainText('Besoin de congés');
  expect((await api.get<ProjectItem[]>('/api/projects')).map((project) => project.code)).toEqual(['CONGES']);
});

test('New project: a name, the code proposed from it, a code typed kept, created, the map opens', async ({ page, api }) => {
  const proposals: Request[] = [];
  page.on('request', (request) => { if (request.url().endsWith('/api/projects/code-proposal')) proposals.push(request); });
  await page.goto('/');
  await openNewProject(page);
  const name = page.getByLabel('Nom en français');
  const code = page.getByLabel('Code court');
  await expect(name).toBeFocused();
  await expect(code).toHaveValue('');
  // The options are folded: only the name and the code are asked.
  await expect(page.getByTestId('new-project-description')).toBeHidden();
  await name.fill('Traitement des factures fournisseurs');
  await expect(code).toHaveValue('TDFF');
  await expect(page.getByText('Proposé d’après le nom.')).toBeVisible();
  // The name travels in the body, never in the address.
  expect(proposals).toHaveLength(1);
  expect(proposals[0]?.url()).not.toContain('factures');
  expect(proposals[0]?.postDataJSON()).toEqual({ name: 'Traitement des factures fournisseurs' });
  await page.getByText('Plus d’options').click();
  await expect(page.getByTestId('new-project-description')).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Classification' })).toHaveValue('internal');
  // A code typed is never replaced by a new name; emptied, it stays empty until the name changes, then follows the new name.
  await code.fill('tdf');
  await expect(code).toHaveValue('TDF');
  await expect(page.getByText('Proposé d’après le nom.')).toHaveCount(0);
  await name.fill('Traitement des factures');
  await page.waitForTimeout(700);
  await expect(code).toHaveValue('TDF');
  expect(proposals).toHaveLength(1);
  await code.fill('');
  await page.waitForTimeout(700);
  await expect(code).toHaveValue('');
  expect(proposals).toHaveLength(1);
  await name.fill('Traitement des paiements');
  await expect(code).toHaveValue('TDP');
  await expect(page.getByText('Proposé d’après le nom.')).toBeVisible();
  expect(proposals).toHaveLength(2);
  expect(proposals[1]?.postDataJSON()).toEqual({ name: 'Traitement des paiements' });
  await page.getByRole('button', { name: 'Créer le projet' }).click();
  await expect(page).toHaveURL(/\/maps\//);
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveText('Traitement des paiements');
  expect((await api.get<ProjectItem[]>('/api/projects')).map((project) => project.code)).toEqual(['TDP']);
});

test('Check for updates opens one new tab at the page of the versions, in the Help menu and in the menu of the account, in French then in English', async ({ page, context, api }) => {
  const { me, links } = await profileOf(api);
  await guardNetwork(context, links);
  await page.goto('/');
  await page.getByTestId('header-help').click();
  const line = page.getByTestId('help-menu-update');
  await expect(line).toContainText('Rechercher une mise à jour');
  await expect(line).toHaveAttribute('title', new RegExp(`Installée : ${MANIFEST.version.replaceAll('.', '\\.')} du `));
  await page.keyboard.press('Escape');
  await expectTabAt(context, () => helpUpdate(page), versionsAt(links));
  await expectTabAt(context, () => accountUpdate(page), versionsAt(links));
  // An interface in English is sent to the English pages of the site.
  await api.send('PATCH', '/api/me', { language: 'en' }, me.version);
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await page.getByTestId('header-user').click();
  await expect(page.getByTestId('account-menu-update')).toContainText('Check for updates');
  await page.keyboard.press('Escape');
  await expectTabAt(context, () => accountUpdate(page), versionsAt(links, true));
});

test('a Store version opens the page of the Store and is never reminded (simulated)', async ({ page, context, api }) => {
  const { me, links } = await profileOf(api);
  await guardNetwork(context, links);
  await simulate(page, me, { store: true });
  await homeAt(page, dayAfter(MANIFEST.releaseDate, 91));
  await expect(REMINDER(page)).toHaveCount(0);
  await page.getByTestId('header-help').click();
  await expect(page.getByTestId('help-menu-update')).toHaveAttribute('title', 'Ouvre la fiche du Microsoft Store, qui fait les mises à jour.');
  await page.keyboard.press('Escape');
  await expectTabAt(context, () => helpUpdate(page), links.store_page);
});

test('the reminder appears 91 days after the release (clock of the browser), closes, stays closed after a reload, comes back 91 days later', async ({ page, context, api }) => {
  const { links } = await profileOf(api);
  await guardNetwork(context, links);
  const [year = '', month = '', day = ''] = MANIFEST.releaseDate.split('-');
  // The day of the release and the day before the delay: nothing; the host date is never changed.
  await homeAt(page, dayAfter(MANIFEST.releaseDate, 0));
  await expect(REMINDER(page)).toHaveCount(0);
  await homeAt(page, dayAfter(MANIFEST.releaseDate, 89));
  await expect(REMINDER(page)).toHaveCount(0);
  const due = dayAfter(MANIFEST.releaseDate, 91);
  await homeAt(page, due);
  await expect(REMINDER(page)).toContainText(`Cette version date du ${day}/${month}/${year}.`);
  // The link is the entry « Check for updates ».
  await expectTabAt(context, () => page.getByTestId('update-reminder-open').click(), versionsAt(links));
  await expect(REMINDER(page)).not.toHaveAttribute('role', 'alert');
  await page.getByTestId('update-reminder-close').click();
  await expect(REMINDER(page)).toHaveCount(0);
  expect(await page.evaluate((key) => localStorage.getItem(key), CLOSED_KEY)).toBe(isoDay(due));
  await homeAt(page, due);
  await expect(REMINDER(page)).toHaveCount(0);
  await homeAt(page, dayAfter(isoDay(due), 89));
  await expect(REMINDER(page)).toHaveCount(0);
  await homeAt(page, dayAfter(isoDay(due), 91));
  await expect(REMINDER(page)).toBeVisible();
});

test('through the whole journey, every request of the page goes to the host and the policy refuses none; control: a request of the page to the site is refused and seen', async ({ page, context, api }) => {
  const { links } = await profileOf(api);
  const guard = await guardNetwork(context, links);
  await recordViolations(page);
  const outside: string[] = [];
  page.on('request', (request) => {
    if (/^https?:/.test(request.url()) && new URL(request.url()).origin !== BASE_ORIGIN) outside.push(request.url());
  });
  await page.goto('/');
  await page.getByRole('button', { name: 'Décrire un processus' }).click();
  await page.getByTestId('home-start-menu-blank').click();
  await page.getByLabel('Nom en français').fill('Achats');
  await expect(page.getByLabel('Code court')).toHaveValue('ACHATS');
  await page.getByRole('button', { name: 'Créer le projet' }).click();
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveText('Achats');
  await expectTabAt(context, () => helpUpdate(page), versionsAt(links));
  await page.getByTestId('header-brand').click();
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await expectTabAt(context, () => page.getByTestId('home-start-menu-gallery').click(), `${links.site}/modeles/`);
  expect(outside).toEqual([]);
  expect(await violationsOf(page)).toEqual([]);
  // Positive control: the policy answers a request of the page to the site, and the page sees it.
  const answer = await page.evaluate((address) => fetch(address).then(() => 'reached', () => 'refused'), links.site);
  expect(answer).toBe('refused');
  expect(await violationsOf(page)).toEqual([expect.stringContaining(`connect-src ${links.site}`)]);
  // The tabs opened were served empty by the guard; nothing else went out.
  expect(guard.refused).toEqual([]);
  expect(guard.served.length).toBeGreaterThanOrEqual(2);
});

test('a user who is not administrator (simulated) has neither the reminder nor the line « Check for updates » nor a start', async ({ page, api }) => {
  const { me } = await profileOf(api);
  await simulate(page, me, { is_admin: false });
  await homeAt(page, dayAfter(MANIFEST.releaseDate, 91));
  await expect(REMINDER(page)).toHaveCount(0);
  await expect(page.getByTestId('home-empty')).toHaveText('Aucun projet pour l’instant. L’animateur du projet enverra une invitation.');
  await expect(page.getByRole('button', { name: 'Décrire un processus' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Nouveau projet' })).toHaveCount(0);
  await page.getByTestId('header-help').click();
  await expect(page.getByTestId('help-menu-online')).toBeVisible();
  await expect(page.getByTestId('help-menu-update')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await page.getByTestId('header-user').click();
  await expect(page.getByTestId('account-menu-language')).toBeVisible();
  await expect(page.getByTestId('account-menu-update')).toHaveCount(0);
});

test('without links (SiteLinks false, simulated), no reminder, no « Check for updates », no gallery; the other starts stay', async ({ page, api }) => {
  const { me } = await profileOf(api);
  await simulate(page, me, { links: null });
  await homeAt(page, dayAfter(MANIFEST.releaseDate, 91));
  await expect(REMINDER(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'Décrire un processus' }).click();
  const sample = MODE === 'server' ? [] : ['Projet d’exemple'];
  await expect(page.getByRole('menu', { name: 'Commencer un projet' }).locator('.menu-label')).toHaveText([...sample, 'Carte vide…', 'Décrire avec l’IA', 'Ouvrir un fichier…']);
  await page.keyboard.press('Escape');
  await page.getByTestId('header-help').click();
  await expect(page.getByTestId('help-menu-update')).toHaveCount(0);
  await expect(page.getByTestId('help-menu-shortcuts')).toBeVisible();
  await page.keyboard.press('Escape');
  await page.getByTestId('header-user').click();
  await expect(page.getByTestId('account-menu-update')).toHaveCount(0);
});
