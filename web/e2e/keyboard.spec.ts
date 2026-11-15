// keyboard.spec.ts
// The map from the keyboard on the real host (NF08, S-NF-12, AR-136, AR-137): a step's sheet and RACI panel opened by Enter, taking the focus and giving it back; the pain point and its action plan typed field after field while the host answers (audit 3.1 V3, U1); axe-core on the screens added by the gate P6 (a pain point in a sheet, the properties of a project, the menu of the account).

import type { Locator, Page, Response } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expectAccessible } from './support/axe.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';
import { MODE } from './support/run.ts';

/**
 * A line of four steps; the document of Saisir is used by Valider, 40 % usable as is, with this pain point or none yet
 * (its box in the sheet of Valider).
 */
async function layOut(api: Api, painPoint: string | null = 'Champs ressaisis à la main'): Promise<string> {
  const { mapId } = await api.project('KEY', 'Clavier', 'fr', 'confidential');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const steps = [await api.step(mapId, lane, 0, 'Demande reçue', 'start'), await api.step(mapId, lane, 1, 'Saisir la demande'),
    await api.step(mapId, lane, 2, 'Valider la demande'), await api.step(mapId, lane, 3, 'Demande close', 'end')];
  for (let index = 0; index + 1 < steps.length; index++) await api.flow(mapId, steps[index] ?? '', steps[index + 1] ?? '');
  const made = await api.send<{ outputs: { document: { id: string } }[] }>('POST', `/api/steps/${steps[1] ?? ''}/documents`, {
    direction: 'output', name: 'Formulaire de demande', location: null, wait_hours: null, share_confirmed: null,
  });
  await api.send('PUT', `/api/steps/${steps[2] ?? ''}/uses/${made.outputs[0]?.document.id ?? ''}`, {
    wait_hours: null, percent_complete_accurate: 40, pain_point: painPoint, lost_hours_per_month: painPoint === null ? null : 3,
  }, 0);
  return mapId;
}

/** The node of React Flow that holds a step: what the Tab key focuses. */
const nodeOf = (page: Page, code: string): Locator => page.locator('.react-flow__node').filter({ has: stepOf(page, code) });

/** Whether the element holding the focus is inside the element of this test identifier. */
const focusIn = (page: Page, testId: string) => page.evaluate((id) => document.activeElement?.closest(`[data-testid="${id}"]`) !== null, testId);

test('Enter opens the sheet and, on the RACI layer, the RACI panel of a step focused; each takes the focus and Escape gives it back (S-NF-12, AR-136, AR-137)', async ({ page, api }) => {
  await openMap(page, await layOut(api), 'KEY-E001');
  await nodeOf(page, 'KEY-E002').focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
  await expect.poll(() => focusIn(page, 'sheet-panel')).toBe(true);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
  await expect(nodeOf(page, 'KEY-E002')).toBeFocused();
  // Its menu from the keyboard, the sheet first; Escape gives the focus back to the step.
  await page.keyboard.press('Shift+F10');
  await expect(page.getByRole('menu').getByRole('menuitem', { name: 'Ouvrir la fiche' })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(nodeOf(page, 'KEY-E002')).toBeFocused();
  // On the RACI layer: the panel takes the focus on its first letter, which the key does not give.
  const raci: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'PUT' && request.url().endsWith('/raci')) raci.push(request.url());
  });
  await page.getByTestId('ribbon-raci').click();
  await nodeOf(page, 'KEY-E002').focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('raci-panel')).toBeVisible();
  await expect.poll(() => focusIn(page, 'raci-panel')).toBe(true);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('raci-panel')).toHaveCount(0);
  await expect(nodeOf(page, 'KEY-E002')).toBeFocused();
  expect(raci).toEqual([]);
});

test('the pain point and its action plan typed from the keyboard while the host answers: each field left sends the use, the next one keeps the focus and the typing (audit 3.1 V3, U1)', async ({ page, api }) => {
  // No pain point yet: the first write raises it, which changes the use and its version; the writes after it must carry
  // the version it gave back, not the one read when they were asked.
  await openMap(page, await layOut(api, null), 'KEY-E001');
  // A slow host: the first write of the use waits until the typing has gone on in the next fields.
  let release: () => void = () => undefined;
  const slow = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/steps/*/uses/*', async (route) => {
    if (route.request().method() === 'PUT') await slow;
    await route.continue();
  });
  const responses: Response[] = [];
  page.on('response', (response) => {
    if (response.request().method() === 'PUT' && /\/api\/steps\/[^/]+\/uses\//.test(response.url())) responses.push(response);
  });
  await openSheetOf(page, 'KEY-E003');
  const sheet = page.getByTestId('sheet-panel');
  const name = '« Formulaire de demande »';
  const painPoint = sheet.getByRole('textbox', { name: `Irritant sur ${name}` });
  const lost = sheet.getByRole('spinbutton', { name: `Temps perdu à cause de ${name}, en heures par mois` });
  const action = sheet.getByRole('textbox', { name: `Action décidée sur ${name}` });
  const owner = sheet.getByRole('textbox', { name: `Responsable de l’action sur ${name}` });
  await expect(painPoint).toHaveValue('');
  await painPoint.focus();
  await page.keyboard.type('Champs ressaisis à la main sur papier');
  await page.keyboard.press('Tab');
  await expect(lost).toBeFocused();
  await page.keyboard.type('5');
  await page.keyboard.press('Tab');
  await expect(action).toBeFocused();
  await page.keyboard.type('Formulaire en ligne');
  await page.keyboard.press('Tab');
  await expect(owner).toBeFocused();
  await page.keyboard.type('Accueil');
  release();
  await page.keyboard.press('Tab');
  // Four writes, one after the other, none refused, each with the version of the use the one before gave back; the
  // first raised the pain point, so the chain moved.
  await expect.poll(() => responses.length).toBe(4);
  const writes = await Promise.all(responses.map(async (response) => {
    const reply = response.ok() ? await response.json() as { inputs: { document: { name: string }; use: { version: number } | null }[] } : null;
    return {
      ok: response.ok(),
      sent: Number((response.request().headers()['if-match'] ?? '').replace(/"/g, '')),
      given: reply?.inputs.find((input) => input.document.name === 'Formulaire de demande')?.use?.version ?? -1,
    };
  }));
  expect(writes.map((write) => write.ok)).toEqual([true, true, true, true]);
  expect(writes[0]?.given).toBeGreaterThan(writes[0]?.sent ?? Number.MAX_SAFE_INTEGER);
  expect(writes.slice(1).map((write) => write.sent)).toEqual(writes.slice(0, -1).map((write) => write.given));
  for (const [field, value] of [[painPoint, 'Champs ressaisis à la main sur papier'], [lost, '5'], [action, 'Formulaire en ligne'], [owner, 'Accueil']] as const) {
    await expect(field).toHaveValue(value);
  }
  // Read again from the host: everything typed is there.
  await page.reload();
  await openSheetOf(page, 'KEY-E003');
  for (const [field, value] of [[painPoint, 'Champs ressaisis à la main sur papier'], [lost, '5'], [action, 'Formulaire en ligne'], [owner, 'Accueil']] as const) {
    await expect(field).toHaveValue(value);
  }
});

test('axe-core on the screens added by the gate: a pain point in a sheet, the properties of the project, the menu of the account (S-11-09, S-NF-10)', async ({ page, api }) => {
  await openMap(page, await layOut(api), 'KEY-E001');
  await openSheetOf(page, 'KEY-E003');
  await expect(page.getByTestId('sheet-panel').getByRole('textbox', { name: 'Irritant sur « Formulaire de demande »' })).toHaveValue('Champs ressaisis à la main');
  await expectAccessible(page, 'pain point in a sheet');
  await page.locator('.react-flow__pane').click({ position: { x: 300, y: 600 } });
  await page.getByTestId('header-classification').click();
  await expect(page.getByTestId('project-properties-classification')).toBeFocused();
  await expectAccessible(page, 'properties of the project');
  await page.keyboard.press('Escape');
  if (MODE === 'desktop') {
    await page.getByTestId('header-user').click();
    await expect(page.getByRole('menu')).toBeVisible();
    await expectAccessible(page, 'menu of the account');
  }
});
