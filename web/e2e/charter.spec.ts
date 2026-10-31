// charter.spec.ts
// The charter as Chromium draws it on the real host (spec 9.2 to 9.5): lane bodies in alternation with their separator, the lane header raised on hover, the durations in tabular figures.

import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';
import { laneHead, laneOf } from './support/map-editing.ts';

/** Three lanes, one step each; the step of the first lane has a sheet of 90 minutes a run (version 0: a sheet never saved). */
async function layOut(page: Page, api: Api): Promise<void> {
  const { mapId } = await api.project('CHA', 'Charte');
  const lanes = [await laneOf(api, mapId), await api.lane(mapId, 'Achats'), await api.lane(mapId, 'Comptabilité')];
  const ids: string[] = [];
  for (const [index, lane] of lanes.entries()) ids.push(await api.step(mapId, lane, index, `Étape ${String(index + 1)}`));
  await api.send('PUT', `/api/steps/${ids[0] ?? ''}/sheet`, {
    fields: { duration_minutes: { value: null, number: 90 }, purpose: { value: 'Suivre les ventes.', number: null } }, procedure: null, free_text: null,
  }, 0);
  await openMap(page, mapId, 'CHA-E003');
}

/** A computed property of an element, as the browser draws it. */
const style = (locator: Locator, property: string): Promise<string> =>
  locator.evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);

/** The value of a token of the charter on the root of the page. */
const token = (page: Page, name: string): Promise<string> =>
  page.evaluate((property) => getComputedStyle(document.documentElement).getPropertyValue(property).trim(), name);

/** A colour of the charter written as the browser computes it (rgb). */
const rgbOf = (page: Page, colour: string): Promise<string> =>
  page.evaluate((value) => {
    const probe = document.createElement('span');
    probe.style.color = value;
    document.body.append(probe);
    const computed = getComputedStyle(probe).color;
    probe.remove();
    return computed;
  }, colour);

test('the lane bodies: white and --lane-even in alternation, a separator of 1 px in --lane-separator (S-9.2-02)', async ({ page, api }) => {
  await layOut(page, api);
  const bodies = ['CHA-C01', 'CHA-C02', 'CHA-C03'].map((code) => page.getByTestId(`lane-body-${code}`));
  const [odd, even, separator] = await Promise.all(['--lane-odd', '--lane-even', '--lane-separator'].map(async (name) => rgbOf(page, await token(page, name))));
  expect(await Promise.all(bodies.map((body) => style(body, 'background-color')))).toEqual([odd, even, odd]);
  expect(odd).toBe('rgb(255, 255, 255)');
  expect(even).toBe('rgb(245, 245, 245)');
  for (const body of bodies) {
    expect([await style(body, 'border-bottom-width'), await style(body, 'border-bottom-style'), await style(body, 'border-bottom-color')]).toEqual(['1px', 'solid', separator]);
  }
});

test('the header of a lane raised on hover by the shadow of the charter, its « ⋯ » button then shown (S-9.3-01)', async ({ page, api }) => {
  await layOut(page, api);
  const head = laneHead(page, 'CHA-C02');
  const more = page.getByTestId('lane-more-CHA-C02');
  expect(await style(head, 'box-shadow')).toBe('none');
  await expect.poll(() => style(more, 'opacity')).toBe('0');
  await head.hover();
  const raised = await page.evaluate((shadow) => {
    const probe = document.createElement('div');
    probe.style.boxShadow = shadow;
    document.body.append(probe);
    const computed = getComputedStyle(probe).boxShadow;
    probe.remove();
    return computed;
  }, await token(page, '--shadow-raised'));
  await expect.poll(() => style(head, 'box-shadow')).toBe(raised);
  await expect.poll(() => style(more, 'opacity')).toBe('1');
});

test('the durations in tabular figures: on the pictogram of the step and in the field of the sheet (S-9.5-01)', async ({ page, api }) => {
  await layOut(page, api);
  const picto = stepOf(page, 'CHA-E001').getByTestId('picto-duration');
  await expect(picto).toHaveAttribute('aria-label', /1 h 30/);
  expect(await style(picto, 'font-variant-numeric')).toBe('tabular-nums');
  await openSheetOf(page, 'CHA-E001');
  const field = page.getByTestId('sheet-panel').getByRole('spinbutton', { name: 'Durée par exécution' });
  await expect(field).toHaveValue('90');
  expect(await style(field, 'font-variant-numeric')).toBe('tabular-nums');
});
