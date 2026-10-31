// accessibility.spec.ts
// axe-core on the main screens and their floating parts (spec 11, NF08: WCAG 2.2 AA; S-11-09): no violation, on the real host, in the reference viewport.

import type { Api } from './support/api.ts';
import { expectAccessible } from './support/axe.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';

/** A map of two lanes, four steps of several types, a labelled flow, a RACI letter and a filled sheet. */
async function layOut(api: Api): Promise<{ projectId: string; mapId: string }> {
  const { id: projectId, mapId } = await api.project('ACC', 'Accessibilité', 'fr', 'internal');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const second = await api.lane(mapId, 'Direction');
  const start = await api.step(mapId, first, 0, 'Demande reçue', 'start');
  const check = await api.step(mapId, first, 1, 'Vérifier la demande');
  const decide = await api.step(mapId, second, 2, 'Accord ?', 'decision');
  const end = await api.step(mapId, first, 3, 'Demande close', 'end');
  await api.flow(mapId, start, check);
  await api.flow(mapId, check, decide);
  await api.flow(mapId, decide, end, 'oui');
  const step = (await api.map(mapId)).steps.find((candidate) => candidate.id === check);
  await api.send('PUT', `/api/steps/${check}/raci`, { lane_id: second, role: 'A' }, step?.version);
  await api.send('PUT', `/api/steps/${check}/sheet`, { fields: {}, procedure: [{ text: 'Lire la demande', tool: null, nature: null }], free_text: null }, 0);
  return { projectId, mapId };
}

test('the home page, with its projects and its trash, and the screens of a project (S-11-09, B-1.3, B-1.4, B-1.5)', async ({ page, api }) => {
  const { projectId } = await layOut(api);
  const other = await api.project('OLD', 'Ancien projet');
  const project = await api.get<{ version: number }>(`/api/projects/${other.id}`);
  await api.send('DELETE', `/api/projects/${other.id}?confirmation=OLD`, undefined, project.version);
  await page.goto('/');
  await expect(page.getByTestId('home')).toBeVisible();
  await expect(page.getByTestId('trash')).toBeVisible();
  await expectAccessible(page, 'home and trash');
  await page.getByTestId('home-import-package').click();
  await expectAccessible(page, 'import of a package');
  await page.getByTestId('import-package-cancel').click();
  await page.getByTestId('home-open-pmap').click();
  await expectAccessible(page, 'open a .pmap');
  await page.getByTestId('open-pmap-cancel').click();
  await page.goto('/projects/new');
  await expect(page.getByRole('heading').first()).toBeVisible();
  await expectAccessible(page, 'new project');
  await page.goto(`/projects/${projectId}/members`);
  await expect(page.getByRole('heading').first()).toBeVisible();
  await expectAccessible(page, 'members');
});

test('the map, its ribbon, its menus, its command bar, its sheet and its RACI (S-11-09, B-2.1-02, B-2.1-07, B-2.1-21, B-2.4-02, B-2.4-05)', async ({ page, api }) => {
  const { mapId } = await layOut(api);
  await openMap(page, mapId, 'ACC-E001');
  await expectAccessible(page, 'map');
  await stepOf(page, 'ACC-E002').click({ button: 'right' });
  await expect(page.getByRole('menu')).toBeVisible();
  await expectAccessible(page, 'menu of a step');
  await page.keyboard.press('Escape');
  await page.keyboard.press('Control+k');
  await expect(page.getByTestId('command-bar')).toBeVisible();
  await expectAccessible(page, 'command bar');
  await page.keyboard.press('Escape');
  await openSheetOf(page, 'ACC-E002');
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
  await expectAccessible(page, 'sheet panel');
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });
  await page.getByTestId('ribbon-raci').click();
  await stepOf(page, 'ACC-E002').click();
  await expect(page.getByTestId('raci-panel')).toBeVisible();
  await expectAccessible(page, 'RACI layer and panel');
  await page.keyboard.press('Escape');
  await page.getByTestId('ribbon-matrix').click();
  await expect(page.getByTestId('raci-matrix')).toBeVisible();
  await expectAccessible(page, 'RACI matrix');
});

test('the dialogs of the header, a notification, and the interface in English (S-11-09, B-1.2-13, B-1.2-16, B-1.2-19)', async ({ page, api }) => {
  const { mapId } = await layOut(api);
  await openMap(page, mapId, 'ACC-E001');
  await page.getByTestId('header-file').click();
  await expect(page.getByRole('menu')).toBeVisible();
  await expectAccessible(page, 'File menu');
  await page.keyboard.press('Escape');
  await page.getByTestId('header-translations').click();
  await expect(page.getByTestId('translations')).toBeVisible();
  await expectAccessible(page, 'translations');
  await page.getByTestId('translations-done').click();
  // A notification: Ctrl+S says that everything is saved by itself (B-2.5-05).
  await page.keyboard.press('Control+s');
  await expect(page.getByRole('status').filter({ hasText: 'enregistr' })).toBeVisible();
  await expectAccessible(page, 'notification');
  // The two languages of the header (ADR 083): the list of the process, the dialog of the interface.
  await page.getByTestId('process-language').click();
  await expect(page.getByTestId('process-language-list')).toBeVisible();
  await expectAccessible(page, 'languages of the process');
  await page.keyboard.press('Escape');
  await page.getByTestId('header-user').click();
  await page.getByRole('menuitem', { name: 'Langue de l’interface' }).click();
  await expect(page.getByTestId('interface-language')).toBeVisible();
  await expectAccessible(page, 'language of the interface');
  await page.getByTestId('interface-language-en').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expectAccessible(page, 'map in English');
});
