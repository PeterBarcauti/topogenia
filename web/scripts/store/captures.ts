// captures.ts
// The captures of the listing of the Microsoft Store (ADR 088): the sample project of a first launch, in French then in English, its map, the assistant (a reply pasted from the AI of the person), a sheet, the RACI matrix and a detailed map.

import { mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import type { Page } from '@playwright/test';
import type { Api } from '../../e2e/support/api.ts';
import { expect, stepOf, test } from '../../e2e/support/journey.ts';

const FOLDER = process.env.STORE_CAPTURES ?? '';
type Language = 'fr' | 'en';

/** The code of the sample: written in French under a French interface, in English under any other (spec 6.1). */
const CODES: Record<Language, string> = { fr: 'CONGES', en: 'LEAVE' };
const codeIn = (language: Language) => (number: number): string => `${CODES[language]}-E${String(number).padStart(3, '0')}`;

/** What the person asks the assistant, and the reply of their AI pasted back: three findings on the map of the sample. */
const [fr, en] = [codeIn('fr'), codeIn('en')];
const ANALYSIS: Record<Language, { request: string; reply: object }> = {
  fr: {
    request: 'Qu’est-ce qui ralentit le traitement d’une demande ?',
    reply: {
      form: 'findings', message: 'Trois points à regarder sur la demande de congés.', cited: [],
      findings: [
        { kind: 'loop', title: 'Boucle de correction', detail: 'Une demande non conforme repart en saisie, sans délai ni limite de tours.',
          objects: [fr(8), fr(2)], suggestion: 'Fixer un délai de correction et prévenir le manager au-delà.' },
        { kind: 're_entry', title: 'Solde ressaisi', detail: 'Le solde est lu dans la paie, puis comparé à la main à la demande.',
          objects: [fr(13), fr(14)], suggestion: 'Afficher le solde dans le formulaire de demande.' },
        { kind: 'no_sheet', title: 'Étapes sans fiche', detail: 'Durée et fréquence inconnues : le coût du processus reste incomplet.',
          objects: [fr(8), fr(13), fr(14), fr(6)], suggestion: 'Remplir leur fiche avec les acteurs concernés.' },
      ],
    },
  },
  en: {
    request: 'What slows down the handling of a request?',
    reply: {
      form: 'findings', message: 'Three points to look at in the leave request.', cited: [],
      findings: [
        { kind: 'loop', title: 'Correction loop', detail: 'A non-compliant request goes back to entry, with no deadline and no limit on rounds.',
          objects: [en(8), en(2)], suggestion: 'Set a correction deadline and alert the manager beyond it.' },
        { kind: 're_entry', title: 'Balance entered twice', detail: 'The balance is read in payroll, then compared with the request by hand.',
          objects: [en(13), en(14)], suggestion: 'Show the balance in the request form.' },
        { kind: 'no_sheet', title: 'Steps without a sheet', detail: 'Duration and frequency unknown: the cost of the process is incomplete.',
          objects: [en(8), en(13), en(14), en(6)], suggestion: 'Fill in their sheet with the actors concerned.' },
      ],
    },
  },
};

/** The page as it stays once its dialogs, menus and notifications have faded in. */
async function settled(page: Page): Promise<void> {
  await page.evaluate(() => Promise.all(document.getAnimations()
    .filter((animation) => animation.effect?.getComputedTiming().iterations !== Infinity)
    .map((animation) => animation.finished.catch(() => undefined))));
}

/** A capture once the notifications are gone and the pointer is put away on the header. */
async function shot(page: Page, language: Language, name: string): Promise<void> {
  await page.mouse.move(800, 20);
  await expect(page.getByTestId('toasts').getByRole('status')).toHaveCount(0, { timeout: 15_000 });
  await settled(page);
  await page.screenshot({ path: join(FOLDER, language, `${name}.png`) });
}

/** The interface in the language, and the assistant by copy and paste, allowed on the confidential sample. */
async function prepare(api: Api, language: Language): Promise<void> {
  const me = await api.get<{ version: number }>('/api/me');
  await api.send('PATCH', '/api/me', { language }, me.version);
  const settings = await api.get<{ version: number }>('/api/admin/assistant');
  await api.send('PUT', '/api/admin/assistant', { connector: 'copy_paste', secret: null, allowed_classifications: ['internal', 'confidential'] }, settings.version);
}

/** The view zoomed out by its button, so that a whole map shows. */
async function zoomOut(page: Page, steps: number): Promise<void> {
  for (let step = 0; step < steps; step++) await page.locator('.react-flow__controls-zoomout').click();
}

/** The sample offered on the empty home page, created, then its root map opened, the view zoomed out to show it whole. */
async function openSample(page: Page, language: Language): Promise<void> {
  const code = codeIn(language);
  await page.goto('/');
  await page.getByTestId('home-sample').getByRole('button').first().click();
  await page.getByTestId(`project-${CODES[language]}-open`).click();
  await expect(stepOf(page, code(1))).toBeVisible();
  await zoomOut(page, 2);
}

for (const language of ['fr', 'en'] as const) {
  test(`the captures of the Store in ${language}`, async ({ page, api, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    rmSync(join(FOLDER, language), { recursive: true, force: true });
    mkdirSync(join(FOLDER, language), { recursive: true });
    const code = codeIn(language);
    await prepare(api, language);
    await openSample(page, language);
    await shot(page, language, '1-carte');

    // The assistant: the request, the prompt copied, the reply of the AI of the person pasted back.
    await page.getByTestId('ribbon-assistant').click();
    // The panel reads its connector before it copies a prompt.
    await expect(page.getByTestId('assistant-destination')).toContainText(language === 'fr' ? 'Copier-coller' : 'Copy and paste');
    await page.getByTestId('assistant-field').fill(ANALYSIS[language].request);
    await page.getByTestId('assistant-field').press('Enter');
    const copied = page.getByTestId('assistant-copied');
    await copied.getByTestId('assistant-pasted').fill(JSON.stringify(ANALYSIS[language].reply));
    await copied.getByTestId('assistant-read-pasted').click();
    await expect(page.getByTestId('assistant-reply')).toBeVisible();
    // The steps of the first finding shown on the map, the thread read from the reply.
    await page.getByTestId('assistant-show-finding').first().click();
    await page.getByTestId('assistant-reply').evaluate((reply) => { reply.scrollIntoView({ block: 'start' }); });
    await shot(page, language, '2-assistant');
    await page.getByTestId('ribbon-assistant').click();

    // The sheet of the step validated twice.
    await stepOf(page, code(4)).click();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('sheet-state')).toBeVisible();
    await shot(page, language, '3-fiche');
    await page.keyboard.press('Escape');

    // The RACI matrix, offered once the RACI layer is on.
    await page.getByTestId('ribbon-raci').click();
    await page.getByTestId('ribbon-matrix').click();
    await expect(page.getByTestId('raci-matrix')).toBeVisible();
    await shot(page, language, '4-raci');
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('raci-matrix')).toHaveCount(0);
    await page.getByTestId('ribbon-raci').click();

    // The detailed map of the entry of the request, beside the sheet to review of its form and its question.
    await stepOf(page, code(2)).dblclick();
    await expect(stepOf(page, code(9))).toBeVisible();
    await zoomOut(page, 1);
    await stepOf(page, code(10)).click();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('sheet-state')).toBeVisible();
    await shot(page, language, '5-detail');
  });
}
