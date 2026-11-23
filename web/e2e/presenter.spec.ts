// presenter.spec.ts
// Journey of the presentation of a map (ADR 097): the sample of the leaves, quantified, played in full screen from « Présenter »; the step shown lit, no banner, its subtitles, the light of a passage born at the edge of its box, the counters, the chapters, the black screen, the dive into a detailed step and back (F-120), Échap; the editor as it was after; captures of each moment for the review of the look.

import { mkdirSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expectAccessible } from './support/axe.ts';
import { expect, openMap, test } from './support/journey.ts';
import { MODE } from './support/run.ts';

/** The package of the sample of the leaves, in French (`samples/conges.*`): the interface does not write the name of its format. */
const SAMPLES = join(import.meta.dirname, '..', '..', 'samples');
const SAMPLE = join(SAMPLES, readdirSync(SAMPLES).find((file) => file.startsWith('conges.')) ?? 'conges');
const CAPTURES = process.env.PRESENTER_CAPTURES ?? '';

/**
 * The sample of the leaves imported as a project of its own; gives its root map. Sent from a page of the application: in
 * server mode, only the browser carries the Windows identity (as `pageTransport` does).
 */
async function imported(page: Page, api: Api): Promise<string> {
  await page.goto('/');
  const sent = await page.evaluate(async (bytes) => {
    const response = await fetch('/api/projects/import?code=CONGES&with_members=false', {
      method: 'POST', credentials: 'same-origin', body: Uint8Array.from(atob(bytes), (sign) => sign.charCodeAt(0)),
      headers: { 'X-Requested-With': 'XMLHttpRequest', 'Content-Type': 'application/octet-stream' },
    });
    return { status: response.status, text: await response.text() };
  }, readFileSync(SAMPLE).toString('base64'));
  expect(sent.status, sent.text).toBe(201);
  const project = JSON.parse(sent.text) as { id: string };
  return (await api.get<{ root_map_id: string }>(`/api/projects/${project.id}`)).root_map_id;
}

/**
 * A real click of the mouse on a flow of the stage, at the middle of its line (its label, when it stands there, is the
 * flow as well): what the pointer reaches is what a presenter clicks, which an event sent to the element does not prove.
 */
async function clickFlow(page: Page, flow: Locator): Promise<void> {
  const point = await flow.locator('.react-flow__edge-path').first().evaluate((path: SVGPathElement) => {
    const middle = path.getPointAtLength(path.getTotalLength() / 2);
    const matrix = path.getScreenCTM();
    if (matrix === null) throw new Error('The flow is not drawn.');
    const at = new DOMPoint(middle.x, middle.y).matrixTransform(matrix);
    return { x: at.x, y: at.y };
  });
  await page.mouse.click(point.x, point.y);
}

/** A capture of the moment for the review, when the run asks for them (PRESENTER_CAPTURES: the folder). */
async function capture(page: Page, name: string): Promise<void> {
  if (CAPTURES === '') return;
  mkdirSync(CAPTURES, { recursive: true });
  await page.screenshot({ path: join(CAPTURES, `${MODE}-${name}.png`) });
}

const stage = (page: Page) => page.getByTestId('presenter');
/** The step shown: the box lit on the map, no banner naming it (essai J2 of 3.2). */
const presented = (page: Page) => page.locator('.presenter-flow .react-flow__node.is-presented');
const line = (page: Page) => page.getByTestId('presenter-line');

test('a map presented: in full screen from « Présenter », told step by step with its light and its counters, closed by Échap (ADR 097)', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  // The whole screen: in full screen, or over the whole window when the browser refuses it.
  const box = await stage(page).boundingBox();
  expect([box?.width, box?.height]).toEqual([page.viewportSize()?.width, page.viewportSize()?.height]);
  await expect(page.getByTestId('presenter-heading')).toHaveCount(0);
  await expect(line(page)).toContainText('Le parcours compte');
  // Every flow of the map drawn: as many as on the canvas of the editor, which stays under the stage (audit 3.2, BUG-01: none was).
  const drawn = await page.locator('.map-canvas:not(.presenter-flow) .react-flow__edge').count();
  expect(drawn).toBeGreaterThan(5);
  await expect(page.locator('.presenter-flow .react-flow__edge-path')).toHaveCount(drawn);
  await capture(page, '01-ouverture');
  await expectAccessible(page, 'the stage of a presentation');

  // It opens paused (lot J10): the presenter moves by the keys, the first step lit, its lane lit on the left.
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Besoin de congés');
  await expect(page.locator('.presenter-lane.is-active')).toHaveCount(1);
  // Its exit shown; the flows ahead faded but read from afar, their labels faded with them (audit 3.2, BUG-03).
  await expect(page.locator('.presenter-flow .react-flow__edge.is-ahead')).toHaveCount(1);
  await expect(page.locator('.presenter-flow .react-flow__edge-path.is-faded').first()).toHaveCSS('opacity', '0.45');
  await expect(page.locator('.presenter-flow .edge-label.is-faded').first()).toHaveCSS('opacity', '0.45');
  await page.waitForTimeout(1300);
  await capture(page, '02-premiere-etape');

  // A passage plays: Page suivante, the step said in full, goes to it; the light runs along the flow, then lands on the next step.
  await page.keyboard.press('PageDown');
  await page.keyboard.press('Space');
  await expect(page.getByTestId('presenter-light')).toBeVisible();
  // Masked by the boxes of the steps: the light is born at the edge of the box it leaves, never on it (essai J2 of 3.2).
  await expect(page.getByTestId('presenter-light-flows')).toHaveAttribute('mask', /^url\(#presenter-light-mask/);
  await page.waitForTimeout(500);
  await capture(page, '03-passage');
  await page.keyboard.press('Space');

  // Farther: a step with figures, the counters run up to it.
  for (let index = 0; index < 3; index++) await page.keyboard.press('ArrowRight');
  await page.waitForTimeout(1300);
  await expect(page.getByTestId('presenter-counters')).toBeVisible();
  await capture(page, '04-etape-chiffree');

  // The chapters: one a stop, the one shown marked; the end shows the summary over the whole tour.
  const chapters = page.locator('.presenter-chapter');
  expect(await chapters.count()).toBeGreaterThan(5);
  await page.keyboard.press('End');
  await expect(page.getByTestId('presenter-progress')).toHaveText('Bilan');
  // The decision of the way run lit by its outline, not by a frame around the diamond (audit 3.2, BUG-02).
  const decision = page.locator('.presenter-flow .react-flow__node.is-lit .step--gateway').first();
  await expect(decision).toHaveCSS('box-shadow', 'none');
  await expect(page.locator('.presenter-flow .react-flow__edge.is-lit .react-flow__edge-path').first()).toHaveAttribute('marker-end', /#map-arrow-lit/);
  await page.waitForTimeout(1300);
  await capture(page, '05-bilan');
  await page.keyboard.press('Space');
  await expect(line(page)).toContainText('De bout en bout : 3,6 jours de délai pour 30 minutes de travail.');
  // The counters say the same figures as the sentence: the work by case, rounded as it is said.
  await expect(page.getByTestId('presenter-counter-work')).toContainText('30');
  await expect(page.getByTestId('presenter-counter-lead')).toContainText('3,6');
  await capture(page, '06-bilan-chiffre');

  // Black screen and back, then Échap: the editor as it was.
  await page.keyboard.press('b');
  await expect(page.getByTestId('presenter-blank')).toBeVisible();
  await page.keyboard.press('b');
  await expect(page.getByTestId('presenter-blank')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
  await expect(page.getByTestId('map-canvas')).toBeVisible();
});

test('a presentation from the step chosen, by Shift+F5, and a step clicked during it becomes the one shown (ADR 097)', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('step-CONGES-E003').click();
  await page.keyboard.press('Shift+F5');
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).not.toContainText('Besoin de congés');
  const first = await presented(page).textContent();
  const paused = await page.getByTestId('presenter-play').getAttribute('aria-label');
  // A step off the tour clicked: that step at once, still paused, not the opening again (audit 3.2, BUG-04).
  await page.locator('.presenter-flow .react-flow__node[data-id]').filter({ hasText: 'Besoin de congés' }).first().click();
  await expect(presented(page)).toContainText('CONGES-E001', { timeout: 1000 });
  await expect(page.getByTestId('presenter-play')).toHaveAttribute('aria-label', paused ?? '');
  await expect(presented(page)).toContainText('Besoin de congés');
  expect(first).not.toContain('Besoin de congés');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
});

test('a detailed step dived into by a double click: its map presented, back to the step by the bar, then Échap leaves all (F-120)', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  const detailed = page.locator('.presenter-flow .react-flow__node[data-id]').filter({ hasText: 'Saisir la demande' }).first();
  await detailed.dblclick();
  // The map of its detail, told from its opening, with the way back in the bar.
  await expect(page.getByTestId('presenter-surface')).toBeVisible();
  await expect(page.locator('.presenter-flow .react-flow__node[data-id]').filter({ hasText: 'Saisir la demande' })).toHaveCount(0);
  await expect(page.locator('.presenter-flow .react-flow__edge-path')).toHaveCount(await page.locator('.presenter-flow .react-flow__edge').count());
  expect(await page.locator('.presenter-flow .react-flow__edge-path').count()).toBeGreaterThan(3);
  await page.waitForTimeout(1300);
  await capture(page, '07-detail');
  await page.getByTestId('presenter-surface').click();
  await expect(presented(page)).toContainText('Saisir la demande');
  await expect(page.getByTestId('presenter-surface')).toHaveCount(0);
  await page.getByTestId('presenter-detail').click();
  await expect(page.getByTestId('presenter-surface')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
  await expect(page.getByTestId('map-canvas')).toBeVisible();
  await expect(page.getByTestId('step-CONGES-E002')).toBeVisible();
});

test('a comment of presentation written on T during the presentation: said at once instead of the sentences of its step, kept for the next one, then translated in another language shown (ADR 099)', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Saisir la demande');
  await page.keyboard.press('t');
  const field = page.getByTestId('presenter-comment-field');
  await expect(field).toBeFocused();
  await expect(page.getByTestId('presenter-caption')).toHaveCount(0);
  // Échap closes the comment, never the presentation, and keeps its text as Entrée does. The key is given to the page here:
  // what the browser itself does of Échap in full screen is not played by this journey (docs/a-peaufiner.md, F-129).
  await field.pressSequentially('Brouillon');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(stage(page)).toBeVisible();
  await expect(line(page)).toHaveText('Brouillon');
  await page.keyboard.press('t');
  await expect(field).toHaveValue('Brouillon');
  await field.fill('');
  // The keys typed stay with the field: Space and the letters move nothing; Maj+Entrée goes to the next line.
  await field.pressSequentially('Le salarié remplit sa demande en ligne.');
  await page.keyboard.press('Shift+Enter');
  await field.pressSequentially('Il joint son planning.');
  await expect(presented(page)).toContainText('Saisir la demande');
  await page.waitForTimeout(300);
  await capture(page, '08-commentaire');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(line(page)).toHaveText('Le salarié remplit sa demande en ligne.');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);

  // The next presentation reads it from the host: the same sentence on the same step.
  await page.reload();
  await expect(page.getByTestId('map-canvas')).toBeVisible();
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(line(page)).toHaveText('Le salarié remplit sa demande en ligne.');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);

  // Shown in English, T writes the translation of the comment (ADR 099 revised): the original recalled above the field,
  // the step says the English at once, and the French original stays as it was.
  await page.getByTestId('process-language').click();
  await page.getByTestId('process-language-en').click();
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(line(page)).not.toHaveText('Le salarié remplit sa demande en ligne.');
  await page.keyboard.press('t');
  await expect(field).toBeFocused();
  await expect(field).toHaveValue('');
  await expect(page.getByTestId('presenter-comment-original')).toContainText('Le salarié remplit sa demande en ligne.');
  await field.pressSequentially('The employee fills in the request online.');
  await page.waitForTimeout(300);
  await capture(page, '09-commentaire-traduction');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(line(page)).toHaveText('The employee fills in the request online.');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
  await page.getByTestId('process-language').click();
  await page.getByTestId('process-language-fr').click();
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(line(page)).toHaveText('Le salarié remplit sa demande en ligne.');
  await page.keyboard.press('Escape');
});

test('the field of a comment of presentation follows the step clicked on the map: its text kept first, the cursor in the field of the step reached (essai J6 of 3.2)', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Saisir la demande');
  await page.keyboard.press('t');
  const field = page.getByTestId('presenter-comment-field');
  await expect(field).toBeFocused();
  await field.pressSequentially('Note laissée en passant.');
  // Another step of the map clicked: the text is kept, the field opens on that step with the cursor in it.
  await page.locator('.presenter-flow .react-flow__node', { hasText: 'Besoin de congés' }).first().click();
  await expect(presented(page)).toContainText('Besoin de congés');
  await expect(field).toBeFocused();
  await expect(page.getByTestId('presenter-comment-editor').locator('label')).toContainText('Besoin de congés');
  // Closed, and back to the step first written: its subtitle says the text typed.
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Saisir la demande');
  await expect(line(page)).toHaveText('Note laissée en passant.');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
});

test('the keys and the refusals of a presentation stay in its stage: U comes back from a detail, a comment refused is said over the map, Tab runs through the bar with its ring of focus (review of 12/11)', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Saisir la demande');

  // D dives into the detailed step, U comes back to it: no mouse needed.
  await page.keyboard.press('d');
  await expect(page.getByTestId('presenter-surface')).toBeVisible();
  await page.keyboard.press('u');
  await expect(page.getByTestId('presenter-surface')).toHaveCount(0);
  await expect(presented(page)).toContainText('Saisir la demande');

  // A comment the host refuses once (the line changed meanwhile): the refusal is said in the stage, the field stays with its text.
  let refused = false;
  await page.route('**/api/steps/*/presentation-comment', async (route) => {
    if (route.request().method() !== 'PUT' || refused) return route.continue();
    refused = true;
    return route.fulfill({ status: 409, contentType: 'application/json', body: JSON.stringify({ code: 'CONFLIT_VERSION', message: 'Modifié par une autre personne.', details: {} }) });
  });
  await page.keyboard.press('t');
  const field = page.getByTestId('presenter-comment-field');
  await expect(field).toBeFocused();
  await field.pressSequentially('Le salarié décrit sa demande.');
  await page.keyboard.press('Enter');
  const alert = stage(page).getByTestId('presenter-alert');
  await expect(alert).toBeVisible();
  await expect(alert).not.toBeEmpty();
  await expect(field).toHaveValue('Le salarié décrit sa demande.');
  // The field stays read-only until the comments are read again after the conflict: Entrée pressed before that is ignored (F-141).
  await expect(field).toBeEditable();
  // Sent again, knowingly: kept this time, the field closes and the step says the comment.
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(line(page)).toHaveText('Le salarié décrit sa demande.');

  // Tab enters the bar: the button focused shows its ring, and thirty more never leave the stage for the editor under it.
  await page.keyboard.press('Tab');
  await expect(page.locator(':focus')).toHaveCSS('outline-style', 'solid');
  for (let index = 0; index < 30; index++) await page.keyboard.press('Tab');
  expect(await page.evaluate(() => document.activeElement?.closest('[data-testid="presenter"]') !== null)).toBe(true);
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
});

test('lot J10: a presentation opens paused; the handoff between two steps takes a comment kept by the host, as a step does; E and R cut the messages of the steps and of the handoffs, and give them back', async ({ page, api }) => {
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  // Paused on the whole map: no step is shown until the presenter starts the story, which Space does and undoes.
  const play = page.getByTestId('presenter-play');
  await expect(play).toHaveAttribute('aria-label', 'Lire');
  await page.waitForTimeout(2500);
  await expect(presented(page)).toHaveCount(0);
  await page.keyboard.press('Space');
  await expect(play).toHaveAttribute('aria-label', 'Pause');
  await page.keyboard.press('Space');
  await expect(play).toHaveAttribute('aria-label', 'Lire');

  // The comment of a step, then E: nothing is said of the step; E again gives it back.
  await page.keyboard.press('Home');
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Saisir la demande');
  await page.keyboard.press('t');
  const field = page.getByTestId('presenter-comment-field');
  await expect(field).toBeFocused();
  await field.pressSequentially('Dit de cette étape.');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(line(page)).toHaveText('Dit de cette étape.');
  await page.keyboard.press('e');
  await expect(line(page)).toHaveCount(0);
  await expect(page.getByTestId('presenter-step-messages')).toHaveAttribute('aria-pressed', 'false');
  await page.keyboard.press('e');
  await expect(line(page)).toHaveText('Dit de cette étape.');

  // The arrow that leaves the step shown, clicked by the mouse on its line, shows its way and the hand; T opens the field
  // on the handoff, named by its two steps; the host keeps its text.
  const arrow = page.locator('.presenter-flow .react-flow__edge.is-ahead').first();
  await expect(arrow).toHaveCSS('cursor', 'pointer');
  const arrowId = await arrow.getAttribute('data-id');
  await clickFlow(page, arrow);
  await expect(page.getByTestId('presenter-comment')).toHaveAttribute('aria-label', 'Commentaire du relais');
  await page.keyboard.press('t');
  await expect(field).toBeFocused();
  await expect(page.getByTestId('presenter-comment-editor').locator('label')).toContainText('→');
  await field.pressSequentially('Dit pendant ce relais.');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('presenter-comment-editor')).toHaveCount(0);
  await expect(line(page)).toHaveText('Dit pendant ce relais.');
  // R cuts the messages of the handoffs, and gives them back.
  await page.keyboard.press('r');
  await expect(line(page)).toHaveCount(0);
  await expect(page.getByTestId('presenter-flow-messages')).toHaveAttribute('aria-pressed', 'false');
  await page.keyboard.press('r');
  await expect(line(page)).toHaveText('Dit pendant ce relais.');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);

  // The next presentation reads it from the host: the same arrow says the same text.
  await page.reload();
  await expect(page.getByTestId('map-canvas')).toBeVisible();
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(presented(page)).toContainText('Saisir la demande');
  await clickFlow(page, page.locator(`.presenter-flow .react-flow__edge[data-id="${arrowId ?? ''}"]`));
  await expect(line(page)).toHaveText('Dit pendant ce relais.');
  await page.keyboard.press('Escape');
  await expect(stage(page)).toHaveCount(0);
});

/** The lane names of the stage at the opening, with their box and its band (F-209); a masked name has no box. A name shortened by « … » is allowed: what counts is that its box stays in its band. */
async function laneNames(page: Page): Promise<{ text: string; shown: boolean; top: number; bottom: number; band: { top: number; bottom: number }; lines: number }[]> {
  return page.locator('.presenter-lane').evaluateAll((lanes) => lanes.map((lane) => {
    const span = lane.querySelector('span') as HTMLElement;
    const band = lane.getBoundingClientRect();
    const box = span.getBoundingClientRect();
    const lineHeight = Number.parseFloat(getComputedStyle(span).lineHeight);
    return {
      text: span.textContent, shown: getComputedStyle(span).display !== 'none', top: box.top, bottom: box.bottom, band: { top: band.top, bottom: band.bottom },
      lines: Math.round(box.height / lineHeight),
    };
  }));
}

test('F-209: in a small window, each lane name fits in its band or is masked, and no two names overlap', async ({ page, api }) => {
  await page.setViewportSize({ width: 1024, height: 576 });
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.waitForTimeout(1300);
  const names = await laneNames(page);
  expect(names.length).toBeGreaterThan(1);
  const drawn = names.filter((name) => name.shown);
  for (const name of drawn) {
    expect(name.top, name.text).toBeGreaterThanOrEqual(name.band.top - 0.5);
    expect(name.bottom, name.text).toBeLessThanOrEqual(name.band.bottom + 0.5);
  }
  for (const [index, name] of drawn.entries()) {
    const next = drawn[index + 1];
    if (next !== undefined) expect(name.bottom, `${name.text} / ${next.text}`).toBeLessThanOrEqual(next.top + 0.5);
  }
});

test('F-209: in a large window, a long lane name keeps its two lines', async ({ page, api }) => {
  await page.setViewportSize({ width: 1440, height: 810 });
  const mapId = await imported(page, api);
  await openMap(page, mapId);
  await page.getByTestId('ribbon-present').click();
  await expect(stage(page)).toBeVisible();
  await page.waitForTimeout(1300);
  const names = await laneNames(page);
  const longest = names.reduce((most, name) => (name.text.length > most.text.length ? name : most));
  expect(longest.shown, longest.text).toBe(true);
  expect(longest.lines, longest.text).toBeGreaterThanOrEqual(2);
  expect(longest.top, longest.text).toBeGreaterThanOrEqual(longest.band.top - 0.5);
  expect(longest.bottom, longest.text).toBeLessThanOrEqual(longest.band.bottom + 0.5);
});
