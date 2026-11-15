// captures.ts
// The captures of the user manual, in French, or in English with MANUEL_LANG=en (the interface, the sample LEAVE and the texts typed in English): the home page, a new project and a proposal of the assistant on its empty map, the sample project (map, menus, sheet, RACI, the route of a flow, detailed map, value stream, the layer Analyse of the Makigami), the dialogs of the header and of the File menu; a capture that fails is noted and the run goes on.

import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Locator, Page } from '@playwright/test';
import type { Api } from '../../e2e/support/api.ts';
import { expect, stepOf, test } from '../../e2e/support/journey.ts';

const FOLDER = process.env.MANUEL_CAPTURES ?? '';
/** The name of the .pmap format: the name of the product, read where it is written (D13). */
const PRODUCT = (JSON.parse(readFileSync(new URL('../../src/shared/i18n/locales/fr.json', import.meta.url), 'utf-8')) as { app: { name: string } }).app.name.toLowerCase();
/** The language of the captures: French by default, English with MANUEL_LANG=en. */
const LANGUAGE = process.env.MANUEL_LANG ?? 'fr';
if (LANGUAGE !== 'fr' && LANGUAGE !== 'en') throw new Error('MANUEL_LANG is fr or en.');
const FRENCH = LANGUAGE === 'fr';
type Messages = { [key: string]: string | Messages };
const MESSAGES = JSON.parse(readFileSync(new URL(`../../src/shared/i18n/locales/${LANGUAGE}.json`, import.meta.url), 'utf-8')) as Messages;
/** A text of the interface in the language of the captures, by its key: the names the gestures look for. */
function label(key: string): string {
  const found = key.split('.').reduce<string | Messages | undefined>((node, part) => (typeof node === 'object' ? node[part] : undefined), MESSAGES);
  if (typeof found !== 'string') throw new Error(`No text ${key} in ${LANGUAGE}.json.`);
  return found;
}
/** The sample offered in the language of the interface (ADR 088): CONGES written in French, LEAVE written in English. */
const SAMPLE = FRENCH ? 'CONGES' : 'LEAVE';
/** The small project of purchases, and the target state made from the sample. */
const PURCHASE = FRENCH ? 'ACH' : 'PUR';
const TARGET = FRENCH ? 'CIBLE' : 'TARGET';
const code = (number: number): string => `${SAMPLE}-E${String(number).padStart(3, '0')}`;
const failures: string[] = [];

/** A small purchase process, pasted back as the reply of the AI of the person: the desired state of the empty map. */
const PROPOSAL_FR = {
  format: PRODUCT, version: '1.2', project: { name: 'Demande d’achat' },
  map: {
    lanes: [{ id: 'c1', name: 'Demandeur' }, { id: 'c2', name: 'Acheteur', type: 'department' }, { id: 'c3', name: 'Direction', type: 'department' }],
    steps: [
      { id: 'e1', lane: 'c1', label: 'Besoin identifié', type: 'start' },
      { id: 'e2', lane: 'c1', label: 'Saisir la demande d’achat' },
      { id: 'e3', lane: 'c2', label: 'Montant > 5 000 € ?', type: 'decision' },
      { id: 'e4', lane: 'c3', label: 'Valider la dépense' },
      { id: 'e5', lane: 'c2', label: 'Passer la commande' },
      { id: 'e6', lane: 'c1', label: 'Commande passée', type: 'end' },
    ],
    flows: [
      { source: 'e1', target: 'e2' }, { source: 'e2', target: 'e3' }, { source: 'e3', target: 'e4', label: 'oui' },
      { source: 'e3', target: 'e5', label: 'non' }, { source: 'e4', target: 'e5' }, { source: 'e5', target: 'e6' },
    ],
  },
};

/** What the person asks the assistant on the sample, and the reply of their AI pasted back. */
const ANALYSIS_FR = {
  request: 'Qu’est-ce qui ralentit le traitement d’une demande ?',
  reply: {
    form: 'findings', message: 'Trois points à regarder sur la demande de congés.', cited: [],
    findings: [
      { kind: 'loop', title: 'Boucle de correction', detail: 'Une demande non conforme repart en saisie, sans délai ni limite de tours.',
        objects: [code(8), code(2)], suggestion: 'Fixer un délai de correction et prévenir le manager au-delà.' },
      { kind: 're_entry', title: 'Solde ressaisi', detail: 'Le solde est lu dans la paie, puis comparé à la main à la demande.',
        objects: [code(13), code(14)], suggestion: 'Afficher le solde dans le formulaire de demande.' },
      { kind: 'break', title: 'Rupture de support', detail: 'La demande arrive par le portail, l’avis du manager repart par courriel.',
        objects: [code(4)], suggestion: 'Répondre dans le portail, là où la demande est saisie.' },
    ],
  },
};

/** The same purchase process, written in English. */
const PROPOSAL_EN = {
  format: PRODUCT, version: '1.2', project: { name: 'Purchase request' },
  map: {
    lanes: [{ id: 'c1', name: 'Requester' }, { id: 'c2', name: 'Buyer', type: 'department' }, { id: 'c3', name: 'Management', type: 'department' }],
    steps: [
      { id: 'e1', lane: 'c1', label: 'Need identified', type: 'start' },
      { id: 'e2', lane: 'c1', label: 'Enter the purchase request' },
      { id: 'e3', lane: 'c2', label: 'Amount > €5,000?', type: 'decision' },
      { id: 'e4', lane: 'c3', label: 'Approve the expense' },
      { id: 'e5', lane: 'c2', label: 'Place the order' },
      { id: 'e6', lane: 'c1', label: 'Order placed', type: 'end' },
    ],
    flows: [
      { source: 'e1', target: 'e2' }, { source: 'e2', target: 'e3' }, { source: 'e3', target: 'e4', label: 'yes' },
      { source: 'e3', target: 'e5', label: 'no' }, { source: 'e4', target: 'e5' }, { source: 'e5', target: 'e6' },
    ],
  },
};

/** The same analysis of the leave request, written in English. */
const ANALYSIS_EN = {
  request: 'What slows down the handling of a request?',
  reply: {
    form: 'findings', message: 'Three points to look at in the leave request.', cited: [],
    findings: [
      { kind: 'loop', title: 'Correction loop', detail: 'A request that is not compliant goes back to be entered again, with no deadline and no limit on the rounds.',
        objects: [code(8), code(2)], suggestion: 'Set a deadline for the correction and warn the manager beyond it.' },
      { kind: 're_entry', title: 'Balance entered again', detail: 'The balance is read in the payroll, then compared by hand with the request.',
        objects: [code(13), code(14)], suggestion: 'Show the balance in the request form.' },
      { kind: 'break', title: 'Media break', detail: 'The request arrives through the portal, the opinion of the manager goes back by email.',
        objects: [code(4)], suggestion: 'Reply in the portal, where the request is entered.' },
    ],
  },
};

const PROPOSAL = FRENCH ? PROPOSAL_FR : PROPOSAL_EN;
const ANALYSIS = FRENCH ? ANALYSIS_FR : ANALYSIS_EN;

async function settled(page: Page): Promise<void> {
  await page.evaluate(() => Promise.all(document.getAnimations()
    .filter((animation) => animation.effect?.getComputedTiming().iterations !== Infinity)
    .map((animation) => animation.finished.catch(() => undefined))));
}

type Zone = { x: number; y: number; width: number; height: number };

/** A capture of the page, of one element, or of a zone of the page, once the notifications are gone and the pointer is put away. */
async function shot(page: Page, name: string, element?: Locator, keepPointer = false, zone?: Zone): Promise<void> {
  if (!keepPointer) await page.mouse.move(700, 12);
  await expect(page.getByTestId('toasts').getByRole('status')).toHaveCount(0, { timeout: 15_000 });
  await settled(page);
  await page.waitForTimeout(250);
  if (element !== undefined) await element.screenshot({ path: join(FOLDER, `${name}.png`) });
  else await page.screenshot({ path: join(FOLDER, `${name}.png`), ...(zone === undefined ? {} : { clip: zone }) });
}

/** The colours of a terminal message (the escape sign, a bracket, numbers, m); the folder where the screen at a failure is kept, outside the manual. */
const COLOURS = new RegExp(`${String.fromCodePoint(27)}\\[[0-9;]*m`, 'g');
const FAILURES = process.env.MANUEL_FAILURES;

/** One capture and the gestures before it: a failure is noted, the menus and dialogs are closed, and the run goes on. */
async function capture(page: Page, name: string, gestures: () => Promise<void>): Promise<void> {
  try {
    await gestures();
  } catch (error) {
    const lines = (error instanceof Error ? error.message : String(error)).replace(COLOURS, '').split('\n');
    failures.push(`${name}: ${[lines[0], lines.find((line) => line.startsWith('Locator:'))].filter(Boolean).join(' ')}`);
    if (FAILURES !== undefined) await page.screenshot({ path: join(FAILURES, `${name}.png`) }).catch(() => undefined);
    for (let index = 0; index < 3; index++) await page.keyboard.press('Escape').catch(() => undefined);
  }
}

async function zoomOut(page: Page, steps: number): Promise<void> {
  for (let step = 0; step < steps; step++) await page.locator('.react-flow__controls-zoomout').click();
}

async function zoomIn(page: Page, steps: number): Promise<void> {
  for (let step = 0; step < steps; step++) await page.locator('.react-flow__controls-zoomin').click();
}

/** The interface in the language of the captures, the assistant by copy and paste, allowed on the confidential sample. */
async function prepare(api: Api): Promise<void> {
  const me = await api.get<{ version: number }>('/api/me');
  await api.send('PATCH', '/api/me', { language: LANGUAGE }, me.version);
  const settings = await api.get<{ version: number }>('/api/admin/assistant');
  await api.send('PUT', '/api/admin/assistant', { connector: 'copy_paste', secret: null, allowed_classifications: ['internal', 'confidential'] }, settings.version);
}

/** A request sent in copy and paste, and the reply of the AI of the person pasted back. */
async function pasteReply(page: Page, request: string, reply: string): Promise<void> {
  await page.getByTestId('assistant-field').fill(request);
  await page.getByTestId('assistant-field').press('Enter');
  const copied = page.getByTestId('assistant-copied');
  await copied.getByTestId('assistant-pasted').fill(reply);
  await copied.getByTestId('assistant-read-pasted').click();
}

/** The home page, a new project, and the map the assistant proposes from a pasted reply (captures 01 to 07). */
async function firstProject(page: Page): Promise<void> {
  await page.goto('/');
  await capture(page, '01-accueil', async () => {
    await expect(page.getByTestId('home-sample')).toBeVisible();
    await shot(page, '01-accueil');
  });
  // The sample is offered while no project exists: created first.
  await page.getByTestId('home-sample').getByRole('button').first().click();
  await expect(page.getByTestId(`project-${SAMPLE}`)).toBeVisible();

  await capture(page, '02-nouveau-projet', async () => {
    await page.getByTestId('home-new-project').click();
    await page.getByTestId('new-project-code').fill(PURCHASE);
    await page.getByTestId(`new-project-name-${LANGUAGE}`).fill(FRENCH ? 'Demande d’achat' : 'Purchase request');
    await page.getByTestId('new-project-description').fill(FRENCH ? 'Du besoin d’achat à la commande passée.' : 'From the purchase need to the order placed.');
    await shot(page, '02-nouveau-projet');
    await page.getByTestId('new-project-create').click();
    await expect(page.getByTestId('map-empty')).toBeVisible();
    await shot(page, '03-carte-vide');
  });

  await capture(page, '04-assistant-tuiles', async () => {
    await page.getByTestId('ribbon-assistant').click();
    await expect(page.getByTestId('assistant-panel')).toBeVisible();
    await shot(page, '04-assistant-tuiles');
  });

  await capture(page, '05-assistant-proposition', async () => {
    await pasteReply(page, FRENCH
      ? 'Décris le processus suivant et propose sa carte : un salarié saisit une demande d’achat ; au-delà de 5 000 €, la direction valide la dépense ; l’acheteur passe la commande.'
      : 'Describe the following process and propose its map: an employee enters a purchase request; above €5,000, management approves the expense; the buyer places the order.',
      JSON.stringify(PROPOSAL));
    await expect(page.getByTestId('assistant-proposal')).toBeVisible();
    await page.getByTestId('assistant-preview').click();
    await expect(page.getByTestId('assistant-changes')).toBeVisible();
    await page.getByTestId('assistant-proposal').evaluate((card) => { card.scrollIntoView({ block: 'start' }); });
    await shot(page, '05-assistant-apercu');
    await page.getByTestId('assistant-apply').click();
    await expect(page.getByTestId('assistant-applied')).toBeVisible();
    await page.getByTestId('ribbon-assistant').click();
    await shot(page, '06-carte-appliquee');
  });

  // The sample project, from the home page.
  await capture(page, '07-accueil-projets', async () => {
    await page.getByTestId('header-brand').click();
    await expect(page.getByTestId(`project-${PURCHASE}`)).toBeVisible();
    await shot(page, '07-accueil-projets');
  });
}

/** The sample project: the summary of its map, the map, its menus, the command bar, a sheet and the RACI layer (captures 35, 08 to 17). */
async function sampleMap(page: Page): Promise<void> {
  await page.getByTestId(`project-${SAMPLE}-open`).click();
  await expect(stepOf(page, code(1))).toBeVisible();
  // The summary of the map in its bubble under the title, at the opening (plan 3.1, lot R): folded by the first click after.
  await capture(page, '35-resume-bulle', async () => {
    await expect(page.getByTestId('summary-bubble')).toBeVisible();
    await shot(page, '35-resume-bulle', undefined, true, { x: 0, y: 0, width: 980, height: 190 });
  });
  await zoomOut(page, 1);

  await capture(page, '08-carte', async () => {
    await shot(page, '08-carte');
    await shot(page, '09-ruban', page.getByTestId('ribbon'));
    await shot(page, '10-en-tete', page.getByTestId('app-header'));
  });

  await capture(page, '11-menu-etape', async () => {
    await stepOf(page, code(4)).click({ button: 'right' });
    await expect(page.getByRole('menu').first()).toBeVisible();
    await shot(page, '11-menu-etape', undefined, true);
    await page.keyboard.press('Escape');
  });

  await capture(page, '12-menu-couloir', async () => {
    await page.getByTestId(`lane-head-${SAMPLE}-C02`).click({ button: 'right' });
    await expect(page.getByRole('menu').first()).toBeVisible();
    await shot(page, '12-menu-couloir', undefined, true);
    await page.keyboard.press('Escape');
  });

  await capture(page, '13-barre-de-commande', async () => {
    await page.getByTestId('map-canvas').click({ position: { x: 1300, y: 600 } });
    await page.keyboard.press('Control+k');
    await expect(page.getByTestId('command-bar')).toBeVisible();
    await page.keyboard.type(FRENCH ? 'E8 > E4 : corrigée' : 'E8 > E4 : corrected');
    await shot(page, '13-barre-de-commande');
    await page.keyboard.press('Escape');
  });

  await capture(page, '14-fiche', async () => {
    await stepOf(page, code(4)).click();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('sheet-state')).toBeVisible();
    await shot(page, '14-fiche');
    await page.keyboard.press('Escape');
  });

  await capture(page, '15-raci', async () => {
    await page.getByTestId('ribbon-raci').click();
    await shot(page, '15-raci-calque');
    await stepOf(page, code(2)).click();
    await expect(page.getByTestId('raci-panel')).toBeVisible();
    await shot(page, '16-raci-panneau');
    await page.keyboard.press('Escape');
    await page.getByTestId('ribbon-matrix').click();
    await expect(page.getByTestId('raci-matrix')).toBeVisible();
    await shot(page, '17-raci-matrice');
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('raci-matrix')).toHaveCount(0);
    await page.getByTestId('ribbon-raci').click();
  });
}

/** The middle of the third flow drawn, on the screen: where a click chooses it. */
async function middleOfFlow(page: Page): Promise<{ x: number; y: number }> {
  return page.locator('[data-testid^="rf__edge-"] path.react-flow__edge-path').nth(2).evaluate((path) => {
    const line = path as SVGPathElement;
    const point = line.getPointAtLength(line.getTotalLength() / 2);
    const matrix = line.getScreenCTM();
    if (matrix === null) throw new Error('The flow is not drawn');
    return { x: point.x * matrix.a + matrix.e, y: point.y * matrix.d + matrix.f };
  });
}

/**
 * The handles of the flow from the first step to the second, its departure dragged onto the top of its step, the flow
 * drawn as it would be, framed around the two steps; then the menu of an exit of the decision (captures 31 and 32, ADR 091).
 */
async function flowRoute(page: Page): Promise<void> {
  await capture(page, '31-trace-poignees', async () => {
    // Closer, then the view taken back to the top left of the map, where the first two steps are.
    await zoomIn(page, 2);
    const canvas = await page.getByTestId('map-canvas').boundingBox();
    if (canvas === null) throw new Error('No canvas.');
    await page.mouse.move(canvas.x + canvas.width / 2, canvas.y + canvas.height / 2);
    await page.mouse.wheel(0, -3000);
    await page.keyboard.down('Shift');
    await page.mouse.wheel(0, -3000);
    await page.keyboard.up('Shift');
    await settled(page);
    const [from, to] = [await stepOf(page, code(1)).boundingBox(), await stepOf(page, code(2)).boundingBox()];
    if (from === null || to === null) throw new Error('A step is not on the screen.');
    await page.mouse.click((from.x + from.width + to.x) / 2, from.y + from.height / 2);
    const handle = await page.getByTestId('route-end-source').boundingBox();
    if (handle === null) throw new Error('No handle of the departure.');
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    await page.mouse.move(handle.x + handle.width / 2 - 10, handle.y + handle.height / 2 - 10, { steps: 3 });
    const top = await page.getByTestId('route-point-top').boundingBox();
    if (top === null) throw new Error('No point at the top of the step.');
    await page.mouse.move(top.x + top.width / 2, top.y + top.height / 2, { steps: 6 });
    const upper = Math.max(from.y - 60, canvas.y + 2);
    const zone = { x: from.x - 50, y: upper, width: to.x + to.width + 50 - (from.x - 50), height: from.y + from.height + 60 - upper };
    await shot(page, '31-trace-poignees', undefined, true, zone);
    // Escape gives the gesture up: nothing is written on the sample.
    await page.keyboard.press('Escape');
    await page.mouse.up();
    await zoomOut(page, 2);
  });

  await capture(page, '32-menu-fleche', async () => {
    const middle = await middleOfFlow(page);
    await page.mouse.click(middle.x, middle.y, { button: 'right' });
    await expect(page.getByRole('menu').first()).toBeVisible();
    await shot(page, '32-menu-fleche', undefined, true);
    await page.keyboard.press('Escape');
  });
}

/** The detailed map with a sheet to review, then the value stream layer (captures 18 to 21). */
async function detailAndStream(page: Page): Promise<void> {
  await capture(page, '18-carte-detaillee', async () => {
    await stepOf(page, code(2)).dblclick();
    await expect(stepOf(page, code(9))).toBeVisible();
    await zoomOut(page, 1);
    await shot(page, '18-carte-detaillee');
    await stepOf(page, code(10)).click();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('sheet-state')).toBeVisible();
    await shot(page, '19-fiche-a-revoir');
    await page.keyboard.press('Escape');
    await page.getByTestId('breadcrumb-level-0').click();
    await expect(stepOf(page, code(1))).toBeVisible();
  });

  await capture(page, '20-flux-de-valeur', async () => {
    await page.getByTestId('ribbon-stream').click();
    await expect(page.getByTestId('stream-panel')).toBeVisible();
    await shot(page, '20-flux-de-valeur');
    await stepOf(page, code(1)).click({ button: 'right' });
    await page.getByRole('menuitem', { name: label('menu.step_volume') }).click();
    await expect(page.getByTestId('stream-entry-volume')).toBeVisible();
    await shot(page, '21-volume-entrant', undefined, true);
    await page.keyboard.press('Escape');
    await page.getByTestId('ribbon-stream').click();
  });
}

/**
 * The layer « Analyse » on the sample (Makigami, plan 3.1): its panel, the handoffs put forward by their tile, the places
 * where the team can act, the class of a step in its menu, then the medium of a document in the sheet (captures 36 to 40).
 */
async function analysis(page: Page): Promise<void> {
  const layer = page.getByRole('toolbar').getByRole('button', { name: label('ribbon.analysis'), exact: true });
  const panel = page.getByTestId('analysis-panel');
  await capture(page, '36-analyse', async () => {
    await layer.click();
    await expect(panel.getByTestId('analysis-value-handoffs')).toBeVisible();
    await shot(page, '36-analyse');
    await panel.getByTestId('analysis-tile-handoffs').hover();
    await expect(page.getByTestId('handoff-rank').first()).toBeVisible();
    await shot(page, '37-analyse-relais', undefined, true);
    await page.mouse.move(700, 12);
    await panel.locator('.analysis-place').first().scrollIntoViewIfNeeded();
    await shot(page, '38-analyse-ou-agir', panel);
  });

  await capture(page, '39-menu-valeur', async () => {
    await stepOf(page, code(13)).click({ button: 'right' });
    await expect(page.getByRole('menuitemradio', { name: new RegExp(label('menu.value.waste')) })).toBeVisible();
    await shot(page, '39-menu-valeur', undefined, true);
    await page.keyboard.press('Escape');
    await layer.click();
    await expect(panel).toHaveCount(0);
  });

  await capture(page, '40-document-support', async () => {
    await stepOf(page, code(4)).click();
    await page.keyboard.press('Enter');
    await page.getByTestId('sheet-fold-outputs').click(); // a sheet opens folded (essai J10): the click waits for it, then unfolds its outputs
    await page.getByTestId('sheet-outputs').locator('[data-testid$="-edit"]').first().click();
    await expect(page.getByTestId('document-form-medium')).toBeVisible();
    await page.getByTestId('document-form-medium').scrollIntoViewIfNeeded();
    await shot(page, '40-document-support');
    await page.keyboard.press('Escape');
    await page.keyboard.press('Escape');
  });
}

/**
 * The presentation of the sample (ADR 097, captures 45 and 46): a step told from it (Shift+F5), lit, its subtitle (no banner
 * since the essai J2 of 3.2), counters and bar; then the summary of a story told from the start, its lead time for the work the same as its counters.
 */
async function presentation(page: Page): Promise<void> {
  const stage = page.getByTestId('presenter');
  await capture(page, '45-presentation', async () => {
    await stepOf(page, code(4)).click();
    await page.keyboard.press('Shift+F5');
    await expect(stage).toBeVisible();
    // It opens paused (lot J10): started on the step, done by sessions, it runs on to its work by case (the figure of its counter), then a pause.
    for (const key of ['ArrowRight', 'Space']) await page.keyboard.press(key);
    await expect(page.getByTestId('presenter-line')).toContainText(label('presenter.say.per_case').split('}}')[1] ?? '', { timeout: 15_000 });
    await page.keyboard.press('Space');
    await shot(page, '45-presentation');
  });
  await capture(page, '46-presentation-bilan', async () => {
    await page.keyboard.press('Escape');
    await expect(stage).toHaveCount(0);
    await page.getByTestId('ribbon-present').click();
    await expect(stage).toBeVisible();
    // Opened by « Présenter », paused (lot J10): started on the summary, it runs on to its lead time for the work, then a pause for the capture.
    for (const key of ['End', 'Space']) await page.keyboard.press(key);
    await expect(page.getByTestId('presenter-line')).toContainText(label('presenter.say.closing_lead_work').split('{{')[0] ?? '', { timeout: 15_000 });
    await page.keyboard.press('Space');
    await page.waitForTimeout(900);
    await shot(page, '46-presentation-bilan');
  });
  await page.keyboard.press('Escape');
  await expect(stage).toHaveCount(0);
}

/** The analysis by the assistant, the File menu and its dialogs, the languages, the settings of the AI, the account and the import (captures 22 to 30). */
async function menusAndSettings(page: Page): Promise<void> {
  await capture(page, '22-assistant-analyse', async () => {
    await page.getByTestId('ribbon-assistant').click();
    await pasteReply(page, ANALYSIS.request, JSON.stringify(ANALYSIS.reply));
    await expect(page.getByTestId('assistant-reply')).toBeVisible();
    await page.getByTestId('assistant-show-finding').first().click();
    await page.getByTestId('assistant-reply').evaluate((reply) => { reply.scrollIntoView({ block: 'start' }); });
    await shot(page, '22-assistant-analyse');
    await page.getByTestId('ribbon-assistant').click();
  });

  await capture(page, '23-menu-fichier', async () => {
    await page.getByTestId('header-file').click();
    await expect(page.getByTestId('menu-file-file.pdf')).toBeVisible();
    await shot(page, '23-menu-fichier', undefined, true);
    await page.keyboard.press('Escape');
  });

  await capture(page, '24-proprietes', async () => {
    await page.getByTestId('header-file').click();
    await page.getByTestId('menu-file-file.properties').click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await shot(page, '24-proprietes');
    await page.keyboard.press('Escape');
  });

  await capture(page, '25-export-paquet', async () => {
    await page.getByTestId('header-file').click();
    await page.getByTestId('menu-file-file.export').click();
    await expect(page.getByTestId('export-package-without-people')).toBeVisible();
    await shot(page, '25-export-paquet');
    await page.keyboard.press('Escape');
  });

  await capture(page, '33-export-svg', async () => {
    await page.getByTestId('header-file').click();
    await page.getByTestId('menu-file-file.svg').click();
    await expect(page.getByTestId('export-svg-editable')).toBeEnabled();
    await shot(page, '33-export-svg');
    await page.keyboard.press('Escape');
  });

  // The poster for a wall (Makigami M9): its dialog on A4 landscape, the count, the size and the scale said.
  await capture(page, '44-imprimer-en-grand', async () => {
    await page.getByTestId('header-file').click();
    await page.getByTestId('menu-file-file.wall').click();
    await expect(page.getByTestId('wall-print-summary')).toBeVisible();
    await shot(page, '44-imprimer-en-grand');
    await page.keyboard.press('Escape');
  });
  await capture(page, '26-traductions', async () => {
    await page.getByTestId('header-translations').click();
    await expect(page.getByTestId('translations')).toBeVisible();
    await shot(page, '26-traductions');
    await page.keyboard.press('Escape');
  });

  await capture(page, '27-langue-processus', async () => {
    await page.getByTestId('process-language').click();
    await shot(page, '27-langue-processus', undefined, true);
    await page.keyboard.press('Escape');
  });

  await capture(page, '28-parametres-ia', async () => {
    await page.getByTestId('header-assistant').click();
    await expect(page.getByTestId('assistant-settings')).toBeVisible();
    await page.getByTestId('assistant-connector-bedrock').check();
    await page.getByTestId('assistant-region').fill('eu-central-1');
    await page.getByTestId('assistant-profile').fill(FRENCH ? 'mon-profil-sso' : 'my-sso-profile');
    await page.getByTestId('assistant-model').fill('global.anthropic.claude-sonnet-5-5');
    await shot(page, '28-parametres-ia');
    await page.keyboard.press('Escape');
  });

  await capture(page, '29-menu-compte', async () => {
    await page.getByTestId('header-user').click();
    await shot(page, '29-menu-compte', undefined, true);
    await page.keyboard.press('Escape');
  });
}

/**
 * The photos and the target state (Makigami M6, M8, P3e), last since they make a project: the photo of the sample compared
 * with now, the dialog of the target state, then the target whose process sends fewer requests back, compared with its
 * reference (captures 41 to 43).
 */
async function targetState(page: Page, api: Api): Promise<void> {
  const layer = page.getByRole('toolbar').getByRole('button', { name: label('ribbon.analysis'), exact: true });
  const panel = page.getByTestId('analysis-panel');
  await capture(page, '41-photo-comparee', async () => {
    await page.getByTestId('header-brand').click();
    await page.getByTestId(`project-${SAMPLE}-open`).click();
    await expect(stepOf(page, code(1))).toBeVisible();
    await layer.click();
    const photo = panel.locator('.snapshot').first();
    await photo.getByRole('button', { name: label('snapshots.compare') }).click();
    await photo.locator('table').scrollIntoViewIfNeeded();
    await shot(page, '41-photo-comparee', panel);
    await layer.click();
  });

  await capture(page, '42-etat-cible', async () => {
    await page.getByTestId('header-file').click();
    await page.getByTestId('menu-file-file.target').click();
    const dialog = page.getByRole('dialog', { name: label('files.target_title') });
    await dialog.getByLabel(label('project.code')).fill(TARGET);
    await shot(page, '42-etat-cible-creer');
    await dialog.getByRole('button', { name: label('files.target_button') }).click();
    await expect(page).toHaveURL(/\/maps\//);
    await expect(stepOf(page, `${TARGET}-E001`)).toBeVisible();
    // The process wanted: 97 % of the requests compliant, no longer 90 %: fewer sent back to be corrected.
    const target = await api.map(/\/maps\/([^/?#]+)/.exec(page.url())?.[1] ?? '');
    const compliant = target.flows.find((flow) => flow.label === (FRENCH ? 'oui' : 'yes'));
    if (compliant !== undefined) await api.send('PUT', `/api/flows/${compliant.id}/share`, { share: 97 }, compliant.version);
    await page.reload();
    await layer.click();
    await expect(panel.getByTestId('target-comparison')).toBeVisible();
    await panel.getByTestId('target-state').scrollIntoViewIfNeeded();
    await shot(page, '43-etat-cible-compare', panel);
  });
}

/** The dialogs of the home page: the opening of a file in every format it reads, the import of a package (captures 34 and 30). */
async function homeDialogs(page: Page): Promise<void> {
  await capture(page, '34-ouvrir-un-fichier', async () => {
    await page.getByTestId('header-brand').click();
    await page.getByTestId('home-open-pmap').click();
    await expect(page.getByRole('dialog', { name: label('files.open_title') })).toBeVisible();
    await shot(page, '34-ouvrir-un-fichier');
    await page.keyboard.press('Escape');
  });

  await capture(page, '30-import-paquet', async () => {
    await page.getByTestId('header-brand').click();
    await page.getByTestId('home-import-package').click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await shot(page, '30-import-paquet');
    await page.keyboard.press('Escape');
  });
}

test('the captures of the manual', async ({ page, api, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  rmSync(FOLDER, { recursive: true, force: true });
  mkdirSync(FOLDER, { recursive: true });
  await prepare(api);

  await firstProject(page);
  await sampleMap(page);
  await flowRoute(page);
  await detailAndStream(page);
  await analysis(page);
  await presentation(page);
  await menusAndSettings(page);
  await homeDialogs(page);
  await targetState(page, api);

  writeFileSync(join(FOLDER, 'echecs.txt'), failures.length === 0 ? 'aucun\n' : `${failures.join('\n')}\n`, 'utf8');
});
