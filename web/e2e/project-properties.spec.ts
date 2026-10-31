// project-properties.spec.ts
// Journeys of the settings of a project (spec 4.1, 10, ADR 069): the classification chosen at the creation, shown and changed in the properties, printed on the PDF; the properties read by a member who is not a facilitator; the label of the sharing group named in the sheet.

import { readFileSync } from 'node:fs';
import type { Page, Request } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import { pdfTexts } from '../src/test/pdf.ts';
import type { ProjectRead } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';

type ProjectItem = components['schemas']['ProjectListItem'];
type SheetRead = components['schemas']['SheetView'];

/** The PDF of the map shown, downloaded from the File menu: the texts it prints, in the order of the page. */
async function pdfOfTheMap(page: Page): Promise<string[]> {
  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.pdf').click();
  const file = await download;
  return pdfTexts(readFileSync(await file.path()).toString('latin1')).map((text) => text.text);
}

/** The next write of a setting of the project by the page (`classification`, `sharing-group-label`). */
const nextWrite = (page: Page, projectId: string, setting: string): Promise<Request> =>
  page.waitForRequest((request) => request.method() === 'PUT' && new URL(request.url()).pathname === `/api/projects/${projectId}/${setting}`);

/** The project PAIE created in the interface by the administrator, Confidentiel chosen in the place of Interne, the default. */
async function createConfidential(page: Page): Promise<void> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await page.getByLabel('Code court').fill('PAIE');
  await page.getByLabel('Nom en français').fill('Paie mensuelle');
  const level = page.getByRole('combobox', { name: 'Classification' });
  await expect(level).toHaveValue('internal');
  await level.selectOption({ label: 'Confidentiel' });
  await page.getByRole('button', { name: 'Créer le projet' }).click();
  await expect(page).toHaveURL(/\/maps\//);
}

test('the classification chosen at the creation, shown on the home page and in the header, changed by the facilitator in the properties, printed on the PDF (S-4.1-07)', async ({ page, api }) => {
  await createConfidential(page);
  const header = page.getByTestId('header-classification');
  await expect(header).toHaveText('Confidentiel');
  await expect(header).toHaveAccessibleName('Classification : Confidentiel. Propriétés du projet');
  const [project] = await api.get<ProjectItem[]>('/api/projects');
  expect([project?.code, project?.classification]).toEqual(['PAIE', 'confidential']);
  const id = project?.id ?? '';
  const mapId = project?.root_map_id ?? '';
  // A step for the image of the export.
  await api.step(mapId, (await api.map(mapId)).lanes[0]?.id ?? '', 0, 'Calculer la paie');
  await page.getByTestId('header-brand').click();
  await expect(page.getByTestId('project-PAIE-classification')).toHaveText('Confidentiel');
  await page.getByTestId('project-PAIE-open').click();
  await expect(stepOf(page, 'PAIE-E001')).toBeVisible();

  // The facilitator opens the properties from the level in the header, read again at the opening.
  const before = await api.get<ProjectRead>(`/api/projects/${id}`);
  await header.click();
  const dialog = page.getByRole('dialog', { name: 'Propriétés du projet PAIE' });
  const level = dialog.getByRole('combobox', { name: 'Classification' });
  await expect(level).toHaveValue('confidential');
  const save = dialog.getByRole('button', { name: 'Enregistrer' });
  await expect(save).toBeDisabled();
  await level.selectOption({ label: 'Strictement confidentiel' });
  const sent = nextWrite(page, id, 'classification');
  await save.click();
  // Sent with the version read, the If-Match the server mode demands (428 without it).
  expect(await (await sent).headerValue('if-match')).toBe(`"${String(before.version)}"`);
  await expect(page.getByText('Propriétés du projet enregistrées.')).toBeVisible();
  await expect(dialog).toHaveCount(0);
  await expect(header).toHaveText('Strictement confidentiel');
  const after = await api.get<ProjectRead>(`/api/projects/${id}`);
  expect([after.classification, after.sharing_group_label, after.version]).toEqual(['strictly_confidential', null, before.version + 1]);

  // The export prints the new level (S-4.1-08), and the home page shows it.
  expect(await pdfOfTheMap(page)).toContain('Classification : Strictement confidentiel');
  await page.getByTestId('header-brand').click();
  await expect(page.getByTestId('project-PAIE-classification')).toHaveText('Strictement confidentiel');
});

test('a member who is not a facilitator reads the properties without the means to change them (S-4.1-07, B-4-69; the role of a reader given to the page, ADR 067)', async ({ page, api }) => {
  const { id, mapId } = await api.project('LEC', 'Lecture', 'fr', 'confidential');
  const read = await api.get<ProjectRead>(`/api/projects/${id}`);
  await api.send('PUT', `/api/projects/${id}/sharing-group-label`, { label: 'Équipe finance' }, read.version);
  await api.step(mapId, (await api.map(mapId)).lanes[0]?.id ?? '', 0, 'Contrôler');
  const settled = await api.get<ProjectRead>(`/api/projects/${id}`);
  // The bench has one account, administrator, so facilitator of every project (ADR 067, section 6): the detail of the
  // host reaches the page with the role the host gives a reader (`ProjectCommandsTests`), every other value as read then
  // (read by the API of the journey: in server mode only a page carries the Windows identity).
  await page.route(`**/api/projects/${id}`, async (route) => {
    await route.fulfill({ json: { ...(await api.get<ProjectRead>(`/api/projects/${id}`)), role: 'reader' } });
  });
  const writes: string[] = [];
  page.on('request', (request) => {
    if (request.method() !== 'GET' && request.url().includes(`/api/projects/${id}`)) writes.push(`${request.method()} ${request.url()}`);
  });
  await openMap(page, mapId, 'LEC-E001');
  await page.getByTestId('header-classification').click();
  const dialog = page.getByRole('dialog', { name: 'Propriétés du projet LEC' });
  const level = dialog.getByRole('combobox', { name: 'Classification' });
  await expect(level).toHaveValue('confidential');
  await expect(level).toBeDisabled();
  const label = dialog.getByRole('textbox', { name: 'Groupe de partage des documents' });
  await expect(label).toHaveValue('Équipe finance');
  await expect(label).not.toBeEditable();
  await expect(dialog.getByTestId('project-properties-read-only')).toHaveText('Seul un animateur du projet modifie ces propriétés.');
  await expect(dialog.getByRole('button', { name: 'Enregistrer' })).toHaveCount(0);
  await dialog.getByTestId('project-properties-done').click();
  await expect(dialog).toHaveCount(0);
  expect(writes).toEqual([]);
  const kept = await api.get<ProjectRead>(`/api/projects/${id}`);
  expect([kept.classification, kept.sharing_group_label, kept.version]).toEqual(['confidential', 'Équipe finance', settled.version]);
});

test('the label of the sharing group set in the properties names the group of an input from outside in the sheet (B-4-69, B-8.1-44)', async ({ page, api }) => {
  const { id, mapId } = await api.project('DOC', 'Paiements');
  const stepId = await api.step(mapId, (await api.map(mapId)).lanes[0]?.id ?? '', 0, 'Rapprocher les paiements');
  await openMap(page, mapId, 'DOC-E001');
  // The other way into the properties: the File menu of the map.
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.properties').click();
  const dialog = page.getByRole('dialog', { name: 'Propriétés du projet DOC' });
  const label = dialog.getByRole('textbox', { name: 'Groupe de partage des documents' });
  await expect(label).toHaveValue('');
  await label.fill('Équipe finance');
  const before = await api.get<ProjectRead>(`/api/projects/${id}`);
  const sent = nextWrite(page, id, 'sharing-group-label');
  await dialog.getByRole('button', { name: 'Enregistrer' }).click();
  expect(await (await sent).headerValue('if-match')).toBe(`"${String(before.version)}"`);
  await expect(dialog).toHaveCount(0);
  const after = await api.get<ProjectRead>(`/api/projects/${id}`);
  expect([after.sharing_group_label, after.classification, after.version]).toEqual(['Équipe finance', 'internal', before.version + 1]);

  // An input from outside the map in the sheet: its sharing named by the setting of the project (1.x: « DX-FINANCE » in the code).
  await openSheetOf(page, 'DOC-E001');
  const inputs = page.getByRole('complementary', { name: 'Fiche de l’étape DOC-E001' }).getByTestId('sheet-inputs');
  await inputs.getByRole('button', { name: 'Entrée extérieure' }).click();
  const form = inputs.getByRole('group', { name: 'Nouvelle entrée extérieure' });
  await form.getByRole('textbox', { name: 'Nom du document' }).fill('Relevé bancaire');
  const shared = form.getByRole('checkbox', { name: 'Partagé avec le groupe « Équipe finance »' });
  await expect(shared).not.toBeChecked();
  await shared.check();
  await form.getByRole('button', { name: 'Enregistrer le document' }).click();
  await expect(form).toHaveCount(0);
  await expect(inputs.getByText(new RegExp('^extérieur à la carte · partagé · déclaré par '))).toBeVisible();
  const sheet = await api.get<SheetRead>(`/api/steps/${stepId}/sheet`);
  expect(sheet.inputs.map((input) => [input.document.name, input.document.share_confirmed, input.producer])).toEqual([['Relevé bancaire', true, null]]);
});
