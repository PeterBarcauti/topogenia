// assistant-panel.spec.ts
// Journeys of the panel of the assistant (spec 3.0 sections 3.2, 3.4, 8), on the fake connector of the starter of the host, which plays its replies from the request: a question answered and its step shown, an analysis and its objects, a slow reply told and stopped, a process described from the home page whose proposal is corrected once and becomes a project, the copy and paste, the files joined to a request.

import type { Page } from '@playwright/test';
import { chooseProcessLanguage, expect, openMap, stepOf, test } from './support/journey.ts';
import type { Api } from './support/api.ts';
import { docx } from './support/office.ts';

/** A fictitious key: the connectors of the starter answer without calling anyone. */
const KEY = 'sk-ant-fictive-panel-0000';

async function useConnector(api: Api, connector: 'anthropic' | 'copy_paste'): Promise<void> {
  const settings = await api.get<{ version: number }>('/api/admin/assistant');
  await api.send('PUT', '/api/admin/assistant', { connector, secret: connector === 'anthropic' ? KEY : null, allowed_classifications: ['internal'] }, settings.version);
}

/** A project of two steps in one lane, its map open, the panel opened by its button. */
async function openPanel(page: Page, api: Api): Promise<{ mapId: string }> {
  const project = await api.project('ACH', 'Achats');
  const lane = (await api.map(project.mapId)).lanes[0]?.id ?? '';
  await api.step(project.mapId, lane, 0, 'Saisir la demande');
  await api.step(project.mapId, lane, 1, 'Valider la demande');
  await openMap(page, project.mapId, 'ACH-E001');
  await page.getByTestId('ribbon-assistant').click();
  await expect(page.getByTestId('assistant-tiles')).toBeVisible();
  return { mapId: project.mapId };
}

test('the panel answers a question about the map, shows the step it names, and opens and closes by Ctrl+I with its thread kept (spec 3.0 section 3.4)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  await openPanel(page, api);
  await expect(page.getByTestId('assistant-destination')).toHaveText('Envoyé à Claude par l’API Anthropic · claude-opus-5-5 · projet Interne');
  await page.getByTestId('assistant-tile-ask').click();
  await expect(page.getByTestId('assistant-intent')).toHaveText('Poser une question');
  await page.getByTestId('assistant-field').fill('Combien d’étapes compte la carte ?');
  await page.getByTestId('assistant-field').press('Enter');
  const reply = page.getByTestId('assistant-reply');
  await expect(reply).toContainText('La carte compte 2 étapes ; la première est ACH-E001.');
  await reply.getByTestId('assistant-code').click();
  await expect(page.getByTestId('assistant-selection')).toHaveText('1 étape sélectionnée');
  await expect(stepOf(page, 'ACH-E001')).toBeVisible();

  await page.getByTestId('assistant-field').press('Control+i');
  await expect(page.getByTestId('assistant-panel')).toHaveCount(0);
  await page.locator('.react-flow__pane').click();
  await page.keyboard.press('Control+i');
  await expect(page.getByTestId('assistant-reply')).toContainText('ACH-E001');
});

test('an analysis names its objects and shows them; a slow reply tells its progress and can be stopped (spec 3.0 sections 3.4, 5)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  await openPanel(page, api);
  await page.getByTestId('assistant-tile-analyze').click();
  await page.getByTestId('assistant-send').click();
  const finding = page.getByTestId('assistant-finding');
  await expect(finding).toContainText('Étapes sans fiche');
  await finding.getByTestId('assistant-show-finding').click();
  await expect(page.getByTestId('assistant-selection')).toHaveText('2 étapes sélectionnées');

  await page.getByTestId('assistant-field').fill('Une réponse lente, s’il vous plaît.');
  await page.getByTestId('assistant-field').press('Enter');
  await expect(page.getByTestId('assistant-progress')).toContainText('Réflexion');
  await expect(page.getByTestId('assistant-progress')).toContainText('Étape de réflexion 1.');
  await page.getByTestId('assistant-stop').click();
  await expect(page.getByTestId('assistant-failure')).toHaveText('Échange arrêté.Réessayer');
  await expect(page.getByTestId('assistant-send')).toBeVisible();
});

test('from the home page the administrator describes a process: its proposal, corrected once, becomes a project (spec 3.0 sections 3.2, 7)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  await page.goto('/');
  await page.getByTestId('home-describe-ai').click();
  await expect(page.getByTestId('describe-panel')).toBeVisible();
  await page.getByTestId('assistant-tile-create').click();
  await page.getByTestId('assistant-field').fill('Décris une demande d’achat invalide : le demandeur saisit, le responsable valide, les achats commandent.');
  await page.getByTestId('assistant-field').press('Enter');
  const proposal = page.getByTestId('assistant-proposal');
  await expect(proposal).toContainText("Le seuil de validation n'est pas précisé.");
  await expect(proposal.locator('dd')).toHaveText(['1', '3', '6', '5', '1']);
  await proposal.getByTestId('assistant-create-project').click();
  const opening = page.getByTestId('open-pmap');
  await expect(opening).toBeVisible();
  await opening.getByTestId('open-pmap-code').fill('DACH');
  await page.getByTestId('open-pmap-confirm').click();
  await page.getByTestId('pmap-created-open').click();
  await expect(stepOf(page, 'DACH-E001')).toBeVisible();
  await expect(stepOf(page, 'DACH-E006')).toBeVisible();
});

test('without a model the panel copies the prompt, then reads the reply pasted from another AI (spec 3.0 section 5)', async ({ page, api }) => {
  await useConnector(api, 'copy_paste');
  await openPanel(page, api);
  await expect(page.getByTestId('assistant-destination')).toContainText('Copier-coller : rien ne part de');
  await page.getByTestId('assistant-field').fill('Résume la carte.');
  await page.getByTestId('assistant-field').press('Enter');
  const copied = page.getByTestId('assistant-copied');
  await expect(copied).toBeVisible();
  await copied.getByTestId('assistant-pasted').fill('Voici :\n```json\n{"form":"answer","message":"Deux étapes : ACH-E001 puis ACH-E002."}\n```');
  await copied.getByTestId('assistant-read-pasted').click();
  const reply = page.getByTestId('assistant-reply');
  await expect(reply).toContainText('Réponse collée');
  await expect(reply.getByTestId('assistant-code')).toHaveText(['ACH-E001', 'ACH-E002']);
});

test('a proposal on a map with content is previewed, applied in one operation, and undone by one Ctrl+Z (spec 3.0 sections 3.1-5 to 7)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  await openPanel(page, api);
  await page.getByTestId('assistant-tile-create').click();
  await page.getByTestId('assistant-field').fill('Remplace cette carte par le processus d’achat complet.');
  await page.getByTestId('assistant-field').press('Enter');
  await page.getByTestId('assistant-preview').click();
  await expect(page.getByTestId('assistant-changes')).toContainText('Suppression');
  await expect(stepOf(page, 'ACH-E001')).toHaveClass(/is-ai-removed/);
  await page.getByTestId('assistant-apply').click();
  await expect(page.getByTestId('assistant-applied')).toBeVisible();
  await expect(stepOf(page, 'ACH-E003')).toBeVisible();
  await expect(stepOf(page, 'ACH-E008')).toBeVisible();
  await expect(stepOf(page, 'ACH-E001')).toHaveCount(0);
  // One operation: one Ctrl+Z on the map gives back the map as it was.
  await page.locator('.react-flow__pane').click();
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'ACH-E001')).toBeVisible();
  await expect(stepOf(page, 'ACH-E002')).toBeVisible();
  await expect(stepOf(page, 'ACH-E003')).toHaveCount(0);
});

test('a translation by the assistant into Portuguese: asked from Traductions, previewed in place, applied beside the French texts, the map shown locked in Portuguese, undone by one Ctrl+Z (ADR 083)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  const { mapId } = await openPanel(page, api);
  const lane = (await api.map(mapId)).lanes[0];
  await page.getByTestId('header-translations').click();
  await page.getByTestId('translations-assistant-pt').click();
  await page.getByRole('button', { name: 'Traduire en Português' }).click();
  await page.getByTestId('assistant-preview').click();
  // The preview shows the texts on the map in Portuguese, nothing written, no step framed; the card sums them up.
  await expect(page.getByTestId('map-translation-preview')).toContainText('Aperçu de la traduction en Português : rien n’est encore écrit.');
  await expect(stepOf(page, 'ACH-E001')).toContainText('PT Saisir la demande');
  await expect(page.locator('.is-ai-changed')).toHaveCount(0);
  await expect(page.getByTestId('assistant-translation')).toContainText('textes traduits en Português, affichés sur la carte.');
  await expect(page.getByTestId('assistant-refine')).toHaveCount(0);
  await page.getByTestId('assistant-translation-review').click();
  await expect(page.getByTestId('assistant-translation').getByRole('row').filter({ hasText: 'ACH-E001' })).toContainText('PT Saisir la demande');
  expect((await api.map(mapId)).steps.every((step) => step.translations.pt === undefined)).toBe(true);
  await page.getByTestId('assistant-apply').click();
  await expect(page.getByTestId('assistant-applied')).toBeVisible();
  // The process shows in Portuguese at once, its version of origin locked; the French texts stay as they were.
  await expect(page.getByTestId('process-language')).toContainText('Português');
  await expect(page.getByTestId('map-translation')).toContainText('Affiché en Português');
  await expect(stepOf(page, 'ACH-E001')).toContainText('PT Saisir la demande');
  await expect(stepOf(page, 'ACH-E002')).toContainText('PT Valider la demande');
  await expect(page.getByTestId(`lane-head-${lane?.code ?? ''}`)).toContainText(`PT ${lane?.name ?? ''}`);
  expect((await api.map(mapId)).steps.map((step) => step.label).sort()).toEqual(['Saisir la demande', 'Valider la demande']);
  // One operation: one Ctrl+Z takes the translations back; the texts not translated show their original, to translate.
  await page.locator('.react-flow__pane').click();
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'ACH-E001')).not.toContainText('PT Saisir');
  await expect(stepOf(page, 'ACH-E001').getByTestId('step-label')).toHaveClass(/untranslated/);
  await page.getByTestId('map-translation-origin').click();
  await expect(page.getByTestId('map-translation')).toHaveCount(0);
});

test('in another language than its own, the map is locked: a name typed is its translation, the label of origin unchanged (ADR 083)', async ({ page, api }) => {
  const { mapId } = await openPanel(page, api);
  await chooseProcessLanguage(page, 'de');
  await expect(page.getByTestId('map-translation')).toContainText('Affiché en Deutsch');
  // No structure in another language: neither a lane added nor the menu of a lane.
  await expect(page.getByTestId('lane-add-0')).toHaveCount(0);
  await expect(page.locator('.lane-more')).toHaveCount(0);
  await stepOf(page, 'ACH-E001').dblclick();
  await page.getByTestId('step-label-input').fill('Anfrage erfassen');
  await page.getByTestId('step-label-input').press('Enter');
  await expect(stepOf(page, 'ACH-E001')).toContainText('Anfrage erfassen');
  const step = (await api.map(mapId)).steps.find((candidate) => candidate.code === 'ACH-E001');
  expect([step?.label, step?.translations.de]).toEqual(['Saisir la demande', 'Anfrage erfassen']);
});

test('a map split into levels is applied in place and undone; brought back to one level, it is only opened as a new project (ADR 082)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  const project = await api.project('NIV', 'Niveaux');
  const lane = (await api.map(project.mapId)).lanes[0]?.id ?? '';
  for (const [column, label] of ['Recevoir la demande', 'Vérifier la demande', 'Valider la demande'].entries()) await api.step(project.mapId, lane, column, label);
  await openMap(page, project.mapId, 'NIV-E001');
  await page.getByTestId('ribbon-assistant').click();
  await page.getByTestId('assistant-tile-modify').click();
  await expect(page.getByTestId('assistant-field')).toHaveValue('Répartis cette carte en niveaux de dix étapes au plus.');
  await page.getByTestId('assistant-field').press('Enter');
  await page.getByTestId('assistant-preview').click();
  await expect(page.getByTestId('assistant-changes')).toContainText('carte détaillée');
  await expect(page.getByTestId('assistant-changes')).toContainText('NIV-E002Vérifier la demandeniveau');
  await page.getByTestId('assistant-apply').click();
  await expect(page.getByTestId('assistant-applied')).toBeVisible();
  await expect(stepOf(page, 'NIV-E004')).toContainText('Préparer');
  await expect(stepOf(page, 'NIV-E002')).toHaveCount(0);
  // One operation: one Ctrl+Z gives the flat map back, Ctrl+Y the levels again.
  await page.locator('.react-flow__pane').click();
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'NIV-E002')).toBeVisible();
  await page.keyboard.press('Control+y');
  await expect(stepOf(page, 'NIV-E004')).toBeVisible();

  await page.getByTestId('assistant-field').fill('Ramène le processus sur un seul niveau.');
  await page.getByTestId('assistant-field').press('Enter');
  await page.getByTestId('assistant-proposal').last().getByTestId('assistant-preview').click();
  const card = page.getByTestId('assistant-new-project');
  await expect(card).toContainText('Remontent d’un niveau : NIV-E002, NIV-E003');
  await expect(card.getByTestId('assistant-new-project-missing')).toContainText('NIV-E004Préparerétape détaillée');
  await expect(page.getByTestId('assistant-proposal').last().getByTestId('assistant-apply')).toHaveCount(0);
  await card.getByTestId('assistant-new-project-open').click();
  const opening = page.getByTestId('open-pmap');
  await opening.getByTestId('open-pmap-code').fill('PLAT');
  await page.getByTestId('open-pmap-confirm').click();
  await expect(stepOf(page, 'PLAT-E003')).toContainText('Valider la demande');
  await expect(stepOf(page, 'PLAT-E001')).toContainText('Recevoir la demande');
});

test('files joined go with their request, a Word file read by the host and a PDF as it is, a former format refused, and again with their turn (spec 3.0 section 3.4, lot 4)', async ({ page, api }) => {
  await useConnector(api, 'anthropic');
  await openPanel(page, api);
  const chooser = page.getByTestId('assistant-attach-input');
  await chooser.setInputFiles([
    { name: 'procédure.docx', mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', buffer: docx(['Recevoir la demande d’achat', 'Valider le bon de commande']) },
    { name: 'plan.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7 plan') },
  ]);
  const pills = page.getByTestId('assistant-composer').getByTestId('assistant-file-pill');
  await expect(pills).toHaveCount(2);
  await expect(pills.first()).toContainText('procédure.docx');
  await chooser.setInputFiles({ name: 'ancien.doc', mimeType: 'application/msword', buffer: Buffer.from('ancien') });
  await expect(page.getByTestId('toasts')).toContainText('« ancien.doc » est dans un ancien format d’Office : enregistrez-le au format docx');
  await expect(pills).toHaveCount(2);

  await page.getByTestId('assistant-field').fill('Que décrivent ces fichiers ?');
  await page.getByTestId('assistant-field').press('Enter');
  const reply = page.getByTestId('assistant-reply');
  await expect(reply).toContainText('procédure.docx (word, « Recevoir la demande d’achat Valider le b »)');
  await expect(reply).toContainText('plan.pdf (pdf, 13 octets)');
  await expect(page.getByTestId('assistant-request').getByTestId('assistant-file-pill')).toHaveCount(2);
  await expect(pills).toHaveCount(0);

  await page.getByTestId('assistant-field').fill('Et ensuite ?');
  await page.getByTestId('assistant-field').press('Enter');
  await expect(page.getByTestId('assistant-reply').nth(1)).toContainText('2 d\'un échange précédent');
});
