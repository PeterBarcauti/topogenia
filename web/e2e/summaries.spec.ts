// summaries.spec.ts
// The journey of the summaries of the maps (plan 3.1, lot R) on the fake connector of the starter of the host: asked from the File menu for every map without one, reviewed in the panel, the ones kept saved; the bubble at the opening of a map, folded at the first click; the panel of the summary; a change of the map that makes it stale; up to date, the File menu says so.

import { fileURLToPath } from 'node:url';
import { expect, openMap, test } from './support/journey.ts';
import { fileEntry } from './support/file-menu.ts';

/** A fictitious key: the connectors of the starter answer without calling anyone. */
const KEY = 'sk-ant-fictive-summaries-0000';

test('the summaries are asked from the File menu, reviewed, saved; the bubble opens with the map and folds; a change of the map makes its summary stale (plan 3.1, lot R)', async ({ page, api }) => {
  const settings = await api.get<{ version: number }>('/api/admin/assistant');
  await api.send('PUT', '/api/admin/assistant', { connector: 'anthropic', secret: KEY, allowed_classifications: ['internal'] }, settings.version);
  const project = await api.project('RES', 'Achats');
  const lane = (await api.map(project.mapId)).lanes[0]?.id ?? '';
  const first = await api.step(project.mapId, lane, 0, 'Saisir la demande', 'start');
  const second = await api.step(project.mapId, lane, 1, 'Valider la demande');
  await api.flow(project.mapId, first, second);
  await openMap(page, project.mapId, 'RES-E001');

  // No summary yet: the icon beside the title says where to ask one; no bubble.
  await expect(page.getByTestId('summary-info')).toHaveAttribute('aria-label', 'Aucun résumé : Fichier > Résumer les cartes');
  await expect(page.getByTestId('summary-bubble')).toHaveCount(0);

  await page.getByRole('button', { name: 'Fichier', exact: true }).click();
  await (await fileEntry(page, 'file.summaries')).click();
  await expect(page.getByTestId('assistant-panel').locator('.intent-chip').last()).toHaveText('Résumés');
  const review = page.getByTestId('assistant-summaries');
  await expect(review.getByTestId('assistant-summary')).toHaveCount(1);
  const sentence = review.getByTestId('assistant-summary-text');
  await expect(sentence).toHaveValue('La carte « Achats » suit le dossier de sa première étape à sa fin.');
  // Corrected before it is kept: what is saved is what the person read.
  await sentence.fill('Le service achats saisit la demande puis la valide.');
  await page.getByTestId('assistant-panel').screenshot({ path: fileURLToPath(new URL('../test-results/summaries/volet.png', import.meta.url)) });
  await review.getByTestId('assistant-summaries-save').click();
  await expect(review.getByTestId('assistant-summaries-saved')).toHaveText('Résumés enregistrés : 1');
  const saved = await api.map(project.mapId);
  expect(saved.summary).toMatchObject({ text: 'Le service achats saisit la demande puis la valide.', origin: 'ai', stale: false, version: 1 });

  // The map opened again: the bubble under its title, folded at the first click on the map.
  await page.reload();
  const bubble = page.getByTestId('summary-bubble');
  await expect(bubble).toHaveText('Le service achats saisit la demande puis la valide.');
  await page.screenshot({ path: fileURLToPath(new URL('../test-results/summaries/bulle.png', import.meta.url)) });
  await page.getByTestId('map-canvas').click({ position: { x: 600, y: 400 } });
  await expect(bubble).toHaveCount(0);
  await page.getByTestId('summary-info').hover();
  await expect(bubble).toBeVisible();

  // Its panel; the map changed since: the summary says it is older than the changes.
  await api.step(project.mapId, lane, 2, 'Commander');
  await page.reload();
  await page.getByTestId('summary-info').click();
  const panel = page.getByRole('dialog', { name: 'Résumé de la carte' });
  await expect(panel.getByTestId('summary-text')).toHaveText('Le service achats saisit la demande puis la valide.');
  await expect(panel.getByTestId('summary-stale')).toHaveText('La carte a changé depuis ce résumé.');
  await panel.getByTestId('summary-edit').click();
  await panel.getByTestId('summary-draft').fill('Le service achats saisit, valide puis commande.');
  await panel.getByTestId('summary-save').click();
  await expect(panel.getByTestId('summary-text')).toHaveText('Le service achats saisit, valide puis commande.');
  await expect(panel.getByTestId('summary-stale')).toHaveCount(0);
  await panel.getByTestId('summary-done').click();

  // Every map summarised and up to date: the File menu asks nothing of the model and says so.
  await page.getByRole('button', { name: 'Fichier', exact: true }).click();
  await (await fileEntry(page, 'file.summaries')).click();
  await expect(page.getByTestId('assistant-panel')).toContainText('Toutes les cartes ont un résumé à jour.');
});
