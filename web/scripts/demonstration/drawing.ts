// drawing.ts
// The first chapters of the walk of the demonstration: the home page of a first launch, the project created, its lanes named and typed, its steps drawn by double clicks, then linked by the command bar.

import type { Page } from '@playwright/test';
import { expect, stepOf } from '../../e2e/support/journey.ts';
import { laneHead } from '../../e2e/support/map-editing.ts';
import { addStep, backToStart, code, CODE, lane, linkByCommand, pan, rest, shot, shotWithoutToasts, typeName } from './walk.ts';

/** Step 1: the home page of a first launch, no project yet; the user is known by its Windows account (here a fictitious one). */
export async function firstLaunch(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByTestId('home-empty')).toContainText('Aucun projet pour l’instant');
  await rest(page);
  await shot(page, '01-accueil-premier-lancement');
}

/** Step 2: « Nouveau projet », its code, its name in both languages, a description and two members. */
export async function createProject(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await page.getByLabel('Code court').fill(CODE.toLowerCase());
  await page.getByLabel('Nom en français').fill('Notes de frais');
  await page.getByLabel('Nom en anglais').fill('Expense reports');
  await page.getByLabel('Description').fill('Le parcours d’une note de frais, de la dépense engagée par un salarié à son remboursement.');
  await page.getByTestId('member-add').click();
  await page.getByTestId('member-0-login').fill('DEMO\\karim');
  await page.getByTestId('member-0-role').selectOption('contributor');
  await page.getByTestId('member-add').click();
  await page.getByTestId('member-1-login').fill('DEMO\\ines');
  await page.getByTestId('member-1-role').selectOption('reader');
  await page.getByTestId('member-1-sponsor').check();
  await shot(page, '02-nouveau-projet');
  await page.getByRole('button', { name: 'Créer le projet' }).click();
  await expect(page).toHaveURL(/\/maps\//);
  await expect(page.getByTestId('map-empty')).toContainText('Cette carte est vide.');
  await rest(page);
  await shot(page, '03-carte-vide');
}

/** The type of a lane chosen in its menu of actions; the capture of the menu taken when asked. */
async function laneType(page: Page, laneCode: string, type: string, capture?: string): Promise<void> {
  await laneHead(page, laneCode).hover();
  await page.getByTestId(`lane-more-${laneCode}`).click();
  const entry = page.getByRole('menuitemradio', { name: `Type : ${type}` });
  await expect(entry).toBeVisible();
  if (capture !== undefined) await shot(page, capture);
  await entry.click();
  await expect(page.getByRole('menu')).toHaveCount(0);
}

/** A lane added under the last one, named at once. */
async function addLane(page: Page, name: string): Promise<void> {
  await page.locator('[data-testid^="lane-add-"]').last().click();
  await typeName(page, 'lane-name-input', name);
}

/** Step 3: « Acteur 1 » renamed, three lanes added, each one typed: two persons, a department, a system. */
export async function drawLanes(page: Page): Promise<void> {
  await laneHead(page, lane(1)).dblclick();
  await typeName(page, 'lane-name-input', 'Salarié');
  await addLane(page, 'Manager');
  await addLane(page, 'Comptabilité');
  await addLane(page, 'Logiciel de frais');
  await laneType(page, lane(1), 'Personne ou rôle');
  await laneType(page, lane(2), 'Personne ou rôle');
  await laneType(page, lane(3), 'Service', '04-menu-du-couloir');
  await laneType(page, lane(4), 'Système');
  await expect(laneHead(page, lane(4))).toContainText('Logiciel de frais');
}

/** Step 4: ten steps by double clicks in their cells, the type of each chosen on the ribbon; linked to nothing yet, each one is flagged. */
export async function drawSteps(page: Page): Promise<void> {
  await addStep(page, lane(1), 0, 'Dépense engagée', 'Début');
  await addStep(page, lane(1), 1, 'Saisir la note de frais', 'Étape');
  await addStep(page, lane(2), 2, 'Valider la note');
  await addStep(page, lane(3), 3, 'Note conforme ?', 'Décision');
  await addStep(page, lane(3), 4, 'Payer et archiver', 'Parallèle');
  await rest(page);
  await shot(page, '05-etapes-a-relier');
  // The map goes on right of the screen: the view is moved by the middle button when a cell is out of it.
  await addStep(page, lane(4), 5, 'Préparer le virement', 'Étape');
  await addStep(page, lane(3), 5, 'Archiver les justificatifs');
  await addStep(page, lane(4), 6, 'Attendre la date de paiement', 'Attente');
  await addStep(page, lane(3), 7, 'Paiement fait et note archivée', 'Parallèle');
  await addStep(page, lane(1), 8, 'Note remboursée', 'Fin');
  await expect(stepOf(page, code(10))).toContainText('Note remboursée');
  await expect(stepOf(page, code(10)).getByRole('img', { name: /n’est reliée à aucune autre/ })).toBeVisible();
}

/** Step 5: the eleven flows typed in the command bar by the short codes, two of them with a label; the map read again. */
export async function linkSteps(page: Page): Promise<void> {
  await linkByCommand(page, [
    'E1 > E2', 'E2 > E3', 'E3 > E4', 'E4 > E5 : oui', 'E4 > E2 : non', 'E5 > E6',
    'E5 > E7', 'E6 > E8', 'E8 > E9', 'E7 > E9', 'E9 > E10',
  ], '06-barre-de-commande');
  await expect(page.locator('.react-flow__edge')).toHaveCount(11);
  await expect(page.getByRole('img', { name: /n’est reliée à aucune autre/ })).toHaveCount(0);
  await rest(page);
  await shotWithoutToasts(page, '07-carte-reliee');
  await pan(page, -1100);
  await rest(page);
  await shot(page, '08-carte-reliee-suite');
  await backToStart(page);
}
