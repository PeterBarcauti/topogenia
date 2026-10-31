// sheets.spec.ts
// Journeys of the sheet of a step (annex B 8.3, spec 11): typed and saved on its own, gauged on its step, validated and read again; outputs offered as inputs and carried by the flow; free text; person responsible and clarification, rewritten or removed (spec 3.0 section 16); detour, isolated step and « Autre… ».

import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';

/** A link to a document: the host types it as a web address (B-4-67); only the local host is written in the journeys. */
const ADDRESS = 'https://localhost/partage/ventes.xlsx';

/** The sheet docked for a step, named after its code (1.x « Fiche de l'étape CODE »). */
const sheetOf = (page: Page, code: string): Locator => page.getByRole('complementary', { name: `Fiche de l’étape ${code}` });

/** The box of an element drawn on the page. */
async function boxOf(locator: Locator): Promise<{ x: number; y: number; width: number; height: number }> {
  const box = await locator.boundingBox();
  if (box === null) throw new Error('The element is not drawn.');
  return box;
}

/**
 * Début (FIC-E001), Tableau croisé (FIC-E002) and Envoyer (FIC-E003) on the first lane, FIC-E002 -> FIC-E003 when asked;
 * the project has a contributor besides its facilitator (1.x `preparer`).
 */
async function layOut(page: Page, api: Api, linked = false): Promise<{ mapId: string; ids: string[] }> {
  const created = await api.send<{ root_map_id: string }>('POST', '/api/projects', {
    code: 'FIC', name: 'Fiches', source_language: 'fr', translations: null, description: null, shared_folder: null, classification: null,
    sharing_group_label: null, members: [{ login: 'TEST\\isabelle', role: 'contributor', is_sponsor: false, is_key_contributor: false }],
  });
  const mapId = created.root_map_id;
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const ids = [await api.step(mapId, lane, 0, 'Début', 'start'), await api.step(mapId, lane, 1, 'Tableau croisé'), await api.step(mapId, lane, 2, 'Envoyer')];
  if (linked) await api.flow(mapId, ids[1] ?? '', ids[2] ?? '');
  await openMap(page, mapId, 'FIC-E003');
  return { mapId, ids };
}

/** A click on the background of the canvas, away from the steps. */
async function clickBackground(page: Page): Promise<void> {
  await page.locator('.react-flow__pane').click({ position: { x: 400, y: 380 } });
}

/** Purpose, two operations (Enter goes to the next one), the nature of the second, the frequency and the duration (1.x first journey). */
async function typeForm(page: Page, sheet: Locator): Promise<void> {
  await sheet.getByRole('textbox', { name: 'Finalité', exact: true }).fill('Suivre les ventes par client.');
  const first = sheet.getByRole('textbox', { name: 'Opération 1', exact: true });
  await first.fill('Extraire les ventes du mois');
  await first.press('Enter');
  await expect(sheet.getByRole('textbox', { name: 'Opération 2', exact: true })).toBeFocused();
  await page.keyboard.type('Contrôler les totaux');
  await sheet.getByRole('combobox', { name: 'Nature de l’opération 2' }).selectOption('check');
  await sheet.getByRole('combobox', { name: 'Fréquence', exact: true }).selectOption('month_end_close');
  await sheet.getByRole('spinbutton', { name: 'Durée par exécution' }).fill('90');
}

/** What the form shows of the sheet typed by `typeForm`. */
async function expectFormRead(sheet: Locator): Promise<void> {
  await expect(sheet.getByRole('textbox', { name: 'Finalité', exact: true })).toHaveValue('Suivre les ventes par client.');
  await expect(sheet.getByRole('textbox', { name: 'Opération 2', exact: true })).toHaveValue('Contrôler les totaux');
  await expect(sheet.getByRole('combobox', { name: 'Nature de l’opération 2' })).toHaveValue('check');
  await expect(sheet.getByRole('combobox', { name: 'Fréquence', exact: true })).toHaveValue('month_end_close');
  await expect(sheet.getByRole('spinbutton', { name: 'Durée par exécution' })).toHaveValue('90');
}

/** The sheet docked on the right: the canvas narrows to its left edge, nothing is covered (B-2.1-40). */
async function expectDocked(page: Page, sheet: Locator, widthBefore: number): Promise<void> {
  const canvas = await boxOf(page.getByTestId('map-canvas'));
  const panel = await boxOf(sheet);
  expect(canvas.width).toBeLessThan(widthBefore);
  expect(canvas.x + canvas.width).toBeLessThanOrEqual(panel.x + 1);
}

test('a sheet is typed, saved on its own, gauged on its step, validated, then read again after a reload (B-8.3-14 to B-8.3-18)', async ({ page, api }) => {
  await layOut(page, api);
  // The start and the end are events: no sheet (B-2.3-14).
  await openSheetOf(page, 'FIC-E001');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
  const widthBefore = (await boxOf(page.getByTestId('map-canvas'))).width;
  // A click on a step with content opens its sheet (B-2.3-13, B-4-01).
  await openSheetOf(page, 'FIC-E002');
  const sheet = sheetOf(page, 'FIC-E002');
  await expect(sheet.getByTestId('sheet-state')).toHaveText('à remplir');
  await expectDocked(page, sheet, widthBefore);
  // ADR 064 point 11: the sentence of introduction of the 1.x is not kept (absent from the mockup).
  await expect(sheet.getByRole('textbox', { name: 'Acteur', exact: true })).toHaveAttribute('placeholder', 'Acteur 1');
  const validate = sheet.getByRole('button', { name: 'Je valide ma fiche' });
  await expect(validate).toBeDisabled();

  await typeForm(page, sheet);
  await expect(sheet.getByTestId('sheet-state')).toHaveText('brouillon');
  await expect(sheet.getByTestId('sheet-saving')).toHaveText('Enregistré');
  // Purpose, procedure, frequency, duration: 4 rubrics out of 7.
  await expect(sheet.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '57');
  await expect(stepOf(page, 'FIC-E002').getByTestId('step-gauge')).toHaveAttribute('aria-label', 'Fiche brouillon, complétée à 57 %');
  await expect(stepOf(page, 'FIC-E003').getByTestId('step-gauge')).toHaveCount(0);

  await validate.click();
  await expect(page.getByText('Fiche validée : elle est visible par l’équipe.')).toBeVisible();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');
  await expect(sheet.getByTestId('sheet-foot-text')).toHaveText(new RegExp('^Fiche validée le .+ par .+\\.$'));
  await expect(stepOf(page, 'FIC-E002').getByTestId('step-gauge')).toHaveAttribute('aria-label', 'Fiche validée, complétée à 57 %');

  // A click on the background closes the sheet (B-2.3-18, B-4-05); after a reload, everything is there.
  await clickBackground(page);
  await expect(sheet).toHaveCount(0);
  await page.reload();
  await expect(stepOf(page, 'FIC-E002').getByTestId('step-gauge')).toHaveAttribute('aria-label', 'Fiche validée, complétée à 57 %');
  await openSheetOf(page, 'FIC-E002');
  await expectFormRead(sheet);

  // The next step of the lane offers to take up the frequency (B-4-27).
  await openSheetOf(page, 'FIC-E003');
  const next = sheetOf(page, 'FIC-E003');
  await next.getByRole('button', { name: 'Reprendre « Clôture mensuelle » (étape FIC-E002)' }).click();
  await expect(next.getByRole('combobox', { name: 'Fréquence', exact: true })).toHaveValue('month_end_close');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
});

/** An output declared by the form of the section Sorties, with its link when given. */
async function declareOutput(outputs: Locator, name: string, address?: string): Promise<void> {
  await outputs.getByRole('button', { name: 'Ajouter une sortie' }).click();
  const field = outputs.getByRole('textbox', { name: 'Nom du document' });
  await field.fill(name);
  if (address === undefined) {
    await field.press('Enter');
    return;
  }
  await outputs.getByRole('textbox', { name: 'Lien vers le document' }).fill(address);
  await outputs.getByRole('button', { name: 'Enregistrer le document' }).click();
}

/** The document is ticked in the next sheet: used, with its wait; an input from outside the map besides. */
async function tickInNextSheet(page: Page): Promise<void> {
  await openSheetOf(page, 'FIC-E003');
  const after = sheetOf(page, 'FIC-E003');
  const inputs = after.getByTestId('sheet-inputs');
  const tick = inputs.getByRole('checkbox', { name: 'Utiliser « Tableau des ventes »' });
  await expect(tick).not.toBeChecked();
  await expect(inputs.getByText('FIC-E002', { exact: true })).toHaveAttribute('title', 'Tableau croisé');
  await expect(after.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '0');
  await tick.click();
  await expect(tick).toBeChecked();
  await expect(after.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '14');
  await expect(page.getByRole('img', { name: '1 document : Tableau des ventes' })).toBeVisible();
  const wait = inputs.getByRole('spinbutton', { name: 'Attente avant réception de « Tableau des ventes », en heures' });
  await wait.fill('48');
  await wait.press('Enter');
  await expect(inputs.getByText(/attente 48 h/)).toBeVisible();

  // An input from outside, that no step of the map produces.
  await inputs.getByRole('button', { name: 'Entrée extérieure' }).click();
  await inputs.getByRole('textbox', { name: 'Nom du document' }).fill('Mail du client');
  await inputs.getByRole('textbox', { name: 'Nom du document' }).press('Enter');
  await expect(inputs.getByText(new RegExp('^extérieur à la carte · déclaré par '))).toBeVisible();
}

test('the outputs of the step before are offered unticked; a tick uses the document and the flow carries it (B-8.3-19)', async ({ page, api }) => {
  await layOut(page, api, true);
  await openSheetOf(page, 'FIC-E002');
  const before = sheetOf(page, 'FIC-E002');
  const outputs = before.getByTestId('sheet-outputs');
  await declareOutput(outputs, 'Tableau des ventes', ADDRESS);
  await expect(outputs.getByRole('link', { name: 'Tableau des ventes' })).toHaveAttribute('href', ADDRESS);
  await expect(outputs.getByText(/^pas encore utilisé · déclaré par /)).toBeVisible();
  await expect(before.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '14');
  // Not used yet: nothing goes through the flow.
  await expect(page.getByTestId('edge-documents')).toHaveCount(0);

  await tickInNextSheet(page);

  // After a reload: the flow carries the document, the producer knows who uses it (B-4-63, B-4-79).
  await page.reload();
  await expect(page.getByRole('img', { name: '1 document : Tableau des ventes' })).toBeVisible();
  await openSheetOf(page, 'FIC-E002');
  await expect(sheetOf(page, 'FIC-E002').getByText(/^utilisé par FIC-E003 · /)).toBeVisible();

  // Unticked: the flow carries it no more.
  await openSheetOf(page, 'FIC-E003');
  await sheetOf(page, 'FIC-E003').getByRole('checkbox', { name: 'Utiliser « Tableau des ventes »' }).click();
  await expect(page.getByTestId('edge-documents')).toHaveCount(0);
});

test('the free text is kept, and closing the sheet while typing loses nothing (B-8.3-20)', async ({ page, api }) => {
  await layOut(page, api);
  await openSheetOf(page, 'FIC-E003');
  const sheet = sheetOf(page, 'FIC-E003');
  await sheet.getByRole('radio', { name: 'Écrire librement' }).click();
  await sheet.getByRole('textbox', { name: 'Description libre de l’étape' }).fill('Le fichier part à la direction, puis une relance le lendemain.');
  // Closed at once: what is left is sent as the panel closes (B-4-45).
  await sheet.getByRole('button', { name: 'Fermer la fiche' }).click();
  await expect(sheet).toHaveCount(0);
  await expect(stepOf(page, 'FIC-E003').getByTestId('step-gauge')).toHaveAttribute('aria-label', 'Fiche brouillon, complétée à 0 %');
  await page.reload();
  await openSheetOf(page, 'FIC-E003');
  // Only the free text is filled: the sheet opens on it (B-4-19, B-4-20).
  await expect(sheet.getByRole('radio', { name: 'Écrire librement' })).toHaveAttribute('aria-checked', 'true');
  await expect(sheet.getByRole('textbox', { name: 'Description libre de l’étape' })).toHaveValue('Le fichier part à la direction, puis une relance le lendemain.');
});

test('the facilitator chooses the person responsible, asks a clarification, and the author takes the sheet up again (B-8.3-21, B-8.3-22)', async ({ page, api }) => {
  await layOut(page, api);
  await openSheetOf(page, 'FIC-E002');
  const sheet = sheetOf(page, 'FIC-E002');
  const responsible = sheet.getByRole('combobox', { name: 'Responsable de la fiche' });
  // The members who may fill: the facilitator (who created the project) and the contributor (B-4-13).
  await expect(responsible.getByRole('option')).toHaveCount(3);
  await expect(responsible.getByRole('option').first()).toHaveText('Personne en particulier');
  await responsible.selectOption({ label: 'isabelle' });
  await expect(responsible.locator('option:checked')).toHaveText('isabelle');
  await page.reload();
  await openSheetOf(page, 'FIC-E002');
  await expect(responsible.locator('option:checked')).toHaveText('isabelle');

  await sheet.getByRole('textbox', { name: 'Outils', exact: true }).fill('Excel, extraction SAP VA05');
  await expect(sheet.getByTestId('sheet-state')).toHaveText('brouillon');
  await expect(sheet.getByTestId('sheet-saving')).toHaveText('Enregistré');
  await sheet.getByRole('button', { name: 'Je valide ma fiche' }).click();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');

  // A question of 1000 characters at most (B-4-56); the sheet goes « à revoir » with its box in the head (B-4-57, B-4-12).
  await sheet.getByRole('button', { name: 'Demander une précision…' }).click();
  const question = sheet.getByRole('textbox', { name: 'Question à l’auteur de la fiche' });
  await expect(question).toHaveAttribute('maxlength', '1000');
  await question.fill('Quelle extraction exactement, celle du 1er ou celle de l’après-clôture ?');
  await sheet.getByRole('button', { name: 'Envoyer la demande' }).click();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('à revoir');
  await expect(sheet.getByRole('note')).toContainText('Précision demandée par l’animateur');
  await expect(sheet.getByRole('note')).toContainText('celle de l’après-clôture ?');
  await expect(stepOf(page, 'FIC-E002').getByTestId('step-gauge')).toHaveAttribute('aria-label', 'Fiche à revoir, complétée à 14 %');

  // The author takes it up: a draft, the clarification stays shown until the next validation (B-4-55).
  await sheet.getByRole('textbox', { name: 'Outils', exact: true }).fill('Excel, extraction SAP VA05 du 1er du mois');
  await expect(sheet.getByTestId('sheet-state')).toHaveText('brouillon');
  await expect(sheet.getByTestId('sheet-foot-text')).toContainText('l’équipe voit la version validée le');
  await expect(sheet.getByRole('note')).toBeVisible();
  await sheet.getByRole('button', { name: 'Je valide ma fiche' }).click();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');
  await expect(sheet.getByRole('note')).toHaveCount(0);
});

test('the facilitator rewrites the question in place, then removes it: the sheet is validated again, after a reload too (spec 3.0 section 16)', async ({ page, api }) => {
  await layOut(page, api);
  await openSheetOf(page, 'FIC-E002');
  const sheet = sheetOf(page, 'FIC-E002');
  await sheet.getByRole('textbox', { name: 'Outils', exact: true }).fill('Excel');
  await expect(sheet.getByTestId('sheet-saving')).toHaveText('Enregistré');
  await sheet.getByRole('button', { name: 'Je valide ma fiche' }).click();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');
  await sheet.getByRole('button', { name: 'Demander une précision…' }).click();
  await sheet.getByRole('textbox', { name: 'Question à l’auteur de la fiche' }).fill('Comment la facture parvient-elle au client ?');
  await sheet.getByRole('button', { name: 'Envoyer la demande' }).click();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('à revoir');

  // Rewritten in place; Escape first gives the field up and leaves the sheet open.
  const note = sheet.getByRole('note');
  await note.getByRole('button', { name: 'Modifier' }).click();
  const field = sheet.getByTestId('sheet-clarification-field');
  await expect(field).toHaveValue('Comment la facture parvient-elle au client ?');
  await field.press('Escape');
  await expect(field).toHaveCount(0);
  await expect(sheet).toBeVisible();
  await note.getByRole('button', { name: 'Modifier' }).click();
  await field.fill('Par quel canal la facture part-elle : échange électronique, e-mail, courrier ?');
  await sheet.getByRole('button', { name: 'Enregistrer' }).click();
  // The field closes once the host has answered (the text typed in the field counts in the text of the box until then).
  await expect(field).toHaveCount(0);
  await expect(note.locator('p')).toHaveText('Par quel canal la facture part-elle : échange électronique, e-mail, courrier ?');
  await expect(sheet.getByTestId('sheet-state')).toHaveText('à revoir');
  await page.reload();
  await openSheetOf(page, 'FIC-E002');
  await expect(sheet.getByRole('note')).toContainText('Par quel canal la facture part-elle');

  // Removed: the sheet « à revoir » is validated again, its step too.
  await sheet.getByRole('note').getByRole('button', { name: 'Retirer' }).click();
  await expect(sheet.getByRole('note')).toHaveCount(0);
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');
  await page.reload();
  await openSheetOf(page, 'FIC-E002');
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');
  await expect(sheet.getByRole('note')).toHaveCount(0);
});

/** 200 points of the path of the one detour, in the coordinates of the page. */
async function detourPoints(page: Page): Promise<{ x: number; y: number }[]> {
  const detours = page.locator('.react-flow__edge-detour');
  await expect(detours).toHaveCount(1);
  return detours.locator('path.react-flow__edge-path').evaluate((path: SVGPathElement) => {
    const length = path.getTotalLength();
    const matrix = path.getScreenCTM();
    if (matrix === null) return [];
    return Array.from({ length: 200 }, (_, index) => {
      const point = path.getPointAtLength((length * index) / 199).matrixTransform(matrix);
      return { x: point.x, y: point.y };
    });
  });
}

test('a flow that skips steps goes round them; a step linked to nothing is flagged; « Autre… » gives its documents (B-8.3-23 to B-8.3-25)', async ({ page, api }) => {
  // The case of the 1.x (18/10): Début (column 0) -> Envoyer (column 2), Tableau croisé (column 1) between them linked to
  // nothing; a straight flow went behind it and made believe in two flows.
  const { mapId, ids } = await layOut(page, api);
  await api.flow(mapId, ids[0] ?? '', ids[2] ?? '');
  await page.reload();
  await expect(stepOf(page, 'FIC-E003')).toBeVisible();

  const box = await boxOf(stepOf(page, 'FIC-E002'));
  const points = await detourPoints(page);
  expect(points).toHaveLength(200);
  expect(points.filter((point) => point.x > box.x && point.x < box.x + box.width && point.y > box.y && point.y < box.y + box.height)).toEqual([]);
  await expect(stepOf(page, 'FIC-E002').getByRole('img', { name: 'Cette étape n’est reliée à aucune autre : tracer les flèches qui la relient au processus.' })).toBeVisible();

  // The isolated step produces a document: the step of arrival finds it by « Autre… ».
  await openSheetOf(page, 'FIC-E002');
  const outputs = sheetOf(page, 'FIC-E002').getByTestId('sheet-outputs');
  await declareOutput(outputs, 'Objectifs des ventes');
  await expect(outputs.getByText(/^pas encore utilisé/)).toBeVisible();
  await openSheetOf(page, 'FIC-E003');
  const sheet = sheetOf(page, 'FIC-E003');
  const inputs = sheet.getByTestId('sheet-inputs');
  await expect(inputs.getByRole('checkbox')).toHaveCount(0);
  await inputs.getByRole('button', { name: 'Autre…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Documents produits par les autres étapes' });
  await expect(dialog.getByText('FIC-E002 · Tableau croisé')).toBeVisible();
  await dialog.getByRole('checkbox', { name: 'Utiliser « Objectifs des ventes »' }).click();
  await expect(dialog.getByRole('checkbox', { name: 'Utiliser « Objectifs des ventes »' })).toBeChecked();
  await dialog.getByTestId('other-documents-done').click();
  await expect(dialog).toHaveCount(0);
  // From elsewhere: another colour, the producer said « d'ailleurs » (B-4-65); the panel stays open.
  const line = inputs.locator('li', { hasText: 'Objectifs des ventes' });
  await expect(line).toHaveClass(/entry--other/);
  await expect(line.getByText('FIC-E002 · d’ailleurs')).toBeVisible();
  await expect(line.getByRole('checkbox')).toBeChecked();
  await expect(sheet).toBeVisible();
});
