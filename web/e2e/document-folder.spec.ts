// document-folder.spec.ts
// Journey of the location of a document in the sheet (ADR 104): its path copied by its button; on the desktop mode its folder asked to the host, which shows the folder that holds the document and never the document; on the server mode no folder button.

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, test } from './support/journey.ts';
import { LAUNCH_FILE, MODE, RUN_FOLDER } from './support/run.ts';

/** Where the host of the journeys writes each folder it is asked to show, in the place of the shell of Windows: no journey opens an Explorer. */
const SHOWN_FILE = join(RUN_FOLDER, 'shown-folders.txt');

/** A path of this machine that the run made itself: the document of the journey is said to be this file of the folder of the run. */
const DOCUMENT_PATH = LAUNCH_FILE;

const NAME = 'Tableau des visites';

/** Début (DOS-E001) and Tableau croisé (DOS-E002) on the first lane; the sheet of the second open, one output declared at a path. */
async function layOut(page: Page, api: Api): Promise<Locator> {
  const project = await api.project('DOS', 'Dossiers');
  const lane = (await api.map(project.mapId)).lanes[0]?.id ?? '';
  await api.step(project.mapId, lane, 0, 'Début', 'start');
  await api.step(project.mapId, lane, 1, 'Tableau croisé');
  await openMap(page, project.mapId, 'DOS-E002');
  await openSheetOf(page, 'DOS-E002');
  const outputs = page.getByRole('complementary', { name: 'Fiche de l’étape DOS-E002' }).getByTestId('sheet-outputs');
  await outputs.getByRole('button', { name: 'Ajouter une sortie' }).click();
  await outputs.getByRole('textbox', { name: 'Nom du document' }).fill(NAME);
  await outputs.getByRole('textbox', { name: 'Lien vers le document' }).fill(DOCUMENT_PATH);
  await outputs.getByRole('button', { name: 'Enregistrer le document' }).click();
  // The host typed the location as a path: it is said under the name, which is no link.
  await expect(outputs.getByText(DOCUMENT_PATH)).toBeVisible();
  await expect(outputs.getByRole('link', { name: NAME })).toHaveCount(0);
  return outputs;
}

/** The last folder the host of the journeys was asked to show, as it would have been given to the shell. */
function lastFolderShown(): string | undefined {
  return readFileSync(SHOWN_FILE, 'utf8').trimEnd().split('\n').at(-1);
}

test('the path of a document is copied by its button; its folder is shown by the host on the desktop mode only, never the document (ADR 104)', async ({ page, context, api }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const outputs = await layOut(page, api);

  // The copy button says what it copies, and the path is copied as it was entered.
  await outputs.getByText(NAME, { exact: true }).hover();
  await outputs.getByRole('button', { name: `Copier le chemin de « ${NAME} »` }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Chemin copié.' })).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(DOCUMENT_PATH);

  const folder = outputs.getByRole('button', { name: `Ouvrir le dossier de « ${NAME} »` });
  if (MODE === 'server') {
    // A server never follows a path entered in a map (S-5.4-13): no folder button, the path is still copied.
    await expect(folder).toHaveCount(0);
    return;
  }

  // The path names a file: the host shows the folder that holds it, with its final separator, and never the file.
  await folder.click();
  await expect(page.getByRole('status').filter({ hasText: 'Dossier ouvert dans l’Explorateur de fichiers.' })).toBeVisible();
  expect(lastFolderShown()).toBe(`${dirname(DOCUMENT_PATH)}\\`);
});
