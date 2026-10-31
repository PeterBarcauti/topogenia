// package.spec.ts
// Journeys of the package of exchange (spec 7, 11; S-11-06): exported from the File menu, imported as a new project through its preview (from the home page, the File menu, or the dialog of a .pmap, AR-154), then in the place of a project that goes to the trash.

import { readFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

interface Laid { projectId: string; mapId: string; ids: string[] }

/** Purchases: three steps on two lanes, two flows, an A on the last step, the free text of the sheet of the second. */
async function layOut(api: Api): Promise<Laid> {
  const { id: projectId, mapId } = await api.project('PKG', 'Achats');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const accounting = await api.lane(mapId, 'Comptabilité');
  const ids = [
    await api.step(mapId, first, 0, 'Recevoir la facture'),
    await api.step(mapId, first, 1, 'Contrôler la facture'),
    await api.step(mapId, accounting, 2, 'Payer'),
  ];
  await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(mapId, ids[1] ?? '', ids[2] ?? '', 'conforme');
  const last = (await api.map(mapId)).steps.find((step) => step.id === ids[2]);
  await api.send('PUT', `/api/steps/${ids[2] ?? ''}/raci`, { lane_id: first, role: 'A' }, last?.version);
  // A sheet never saved has no version: its first save sends 0 (ADR 064).
  const sheet = await api.get<{ version: number | null }>(`/api/steps/${ids[1] ?? ''}/sheet`);
  await api.send('PUT', `/api/steps/${ids[1] ?? ''}/sheet`, { fields: {}, procedure: [], free_text: 'Vérifier le bon de commande.' }, sheet.version ?? 0);
  return { projectId, mapId, ids };
}

/** File > Export the project, people left out (the default): the file the browser receives. */
async function exportPackage(page: Page): Promise<{ name: string; path: string }> {
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.export').click();
  await expect(page.getByTestId('export-package-without-people')).toBeChecked();
  const download = page.waitForEvent('download');
  await page.getByTestId('export-package-confirm').click();
  const file = await download;
  await expect(page.getByText(`Paquet téléchargé : ${file.suggestedFilename()}.`)).toBeVisible();
  return { name: file.suggestedFilename(), path: await file.path() };
}

/** What a map holds, by readable codes without the project: labels, flows, RACI, free texts of the sheets. */
async function contentOf(api: Api, mapId: string): Promise<unknown> {
  const map = await api.map(mapId);
  const codeOf = (id: string) => map.steps.find((step) => step.id === id)?.code.replace(`${map.project_code}-`, '');
  const texts: Record<string, string | null> = {};
  for (const step of map.steps) {
    const sheet = await api.get<{ content: { free_text: string | null } }>(`/api/steps/${step.id}/sheet`);
    texts[step.code.replace(`${map.project_code}-`, '')] = sheet.content.free_text;
  }
  return {
    lanes: map.lanes.map((lane) => lane.name),
    steps: map.steps.map((step) => [step.code.replace(`${map.project_code}-`, ''), step.label, step.column]).sort(),
    flows: map.flows.map((flow) => [codeOf(flow.source_step_id), codeOf(flow.target_step_id), flow.label]).sort(),
    raci: map.raci.map((entry) => [codeOf(entry.step_id), map.lanes.find((lane) => lane.id === entry.lane_id)?.name, entry.role]),
    texts,
  };
}

test('a project exported from the File menu comes back as a new project under a new code, through its preview (S-11-06)', async ({ page, api }) => {
  const laid = await layOut(api);
  await openMap(page, laid.mapId, 'PKG-E001');
  const { name, path } = await exportPackage(page);

  await page.goto('/');
  await page.getByTestId('home-import-package').click();
  const extension = await page.getByTestId('import-package-file').getAttribute('accept');
  expect(name).toBe(`PKG_Achats_${new Date().toISOString().slice(0, 10)}${extension ?? ''}`);
  await page.getByTestId('import-package-file').setInputFiles(path);
  await page.getByTestId('import-package-check').click();
  // The code of the package is that of a project: the preview asks for another one, nothing is written yet.
  await expect(page.getByText('Le code PKG est déjà celui d’un projet : en choisir un autre.')).toBeVisible();
  await expect(page.getByTestId('import-package-confirm')).toBeDisabled();
  expect(await api.get<unknown[]>('/api/projects')).toHaveLength(1);
  await page.getByTestId('import-package-code').fill('pkgb');
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText('Le projet prendra le code PKGB.')).toBeVisible();
  await expect(page.getByTestId('package-preview')).toContainText('Cartes : 1 · Couloirs : 2 · Étapes : 3 · Liens : 2 · Fiches : 1');
  await expect(page.getByText('Le paquet a été exporté sans les personnes.')).toBeVisible();
  await page.getByTestId('import-package-confirm').click();

  // The root map of the new project opens, its readable codes rewritten (ADR 052).
  await expect(stepOf(page, 'PKGB-E001')).toBeVisible();
  const projects = await api.get<{ code: string; root_map_id: string }[]>('/api/projects');
  const copy = projects.find((project) => project.code === 'PKGB');
  expect(page.url()).toContain(`/maps/${copy?.root_map_id ?? ''}`);
  expect(await contentOf(api, copy?.root_map_id ?? '')).toEqual(await contentOf(api, laid.mapId));
});

test('the File menu of the map imports a package too; a package chosen in the dialog of a .pmap goes to its import (AR-154)', async ({ page, api }) => {
  const laid = await layOut(api);
  await openMap(page, laid.mapId, 'PKG-E001');
  const { name, path } = await exportPackage(page);

  // « Ouvrir un fichier .pmap… » given the package under its own name (the browser keeps a download under another): no
  // text shown, the import of the package takes it, with its check.
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.open').click();
  await page.getByTestId('open-pmap-file').setInputFiles({ name, mimeType: 'application/octet-stream', buffer: readFileSync(path) });
  const extension = name.slice(name.lastIndexOf('.'));
  const dialog = page.getByRole('dialog', { name: `Importer un paquet ${extension}` });
  await expect(dialog.getByTestId('import-package-recognized')).toHaveText(`« ${name} » est un paquet ${extension}, pas un fichier .pmap : il s’importe ici, après vérification.`);
  await dialog.getByTestId('import-package-check').click();
  await expect(dialog.getByText('Le code PKG est déjà celui d’un projet : en choisir un autre.')).toBeVisible();
  await dialog.getByTestId('import-package-cancel').click();
  await expect(dialog).toHaveCount(0);

  // « Importer un paquet… » of the File menu: a new project, as from the home page.
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.import').click();
  await page.getByTestId('import-package-file').setInputFiles(path);
  await page.getByTestId('import-package-check').click();
  await page.getByTestId('import-package-code').fill('pkgc');
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText('Le projet prendra le code PKGC.')).toBeVisible();
  await page.getByTestId('import-package-confirm').click();
  await expect(stepOf(page, 'PKGC-E001')).toBeVisible();
  expect((await api.get<{ code: string }[]>('/api/projects')).map((project) => project.code).sort()).toEqual(['PKG', 'PKGC']);
});

test('a package takes the place of a project, which goes to the trash under a derived code (S-11-06)', async ({ page, api }) => {
  const laid = await layOut(api);
  await openMap(page, laid.mapId, 'PKG-E001');
  const before = await contentOf(api, laid.mapId);
  const { path } = await exportPackage(page);
  // A change made after the export, which the replacement takes away.
  const step = (await api.map(laid.mapId)).steps.find((candidate) => candidate.id === laid.ids[2]);
  await api.send('PATCH', `/api/steps/${laid.ids[2] ?? ''}`, { label: 'Payer le fournisseur' }, step?.version);

  await page.reload();
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.replace').click();
  await page.getByTestId('import-package-file').setInputFiles(path);
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText('Le projet actuel part à la corbeille sous le code PKGA.')).toBeVisible();
  await page.getByTestId('import-package-confirm').click();

  // The root map of the new project opens, with the label of the package (new identifiers for everything, ADR 052).
  await expect(page).not.toHaveURL(new RegExp(laid.mapId));
  await expect(stepOf(page, 'PKG-E003').getByTestId('step-label')).toHaveText('Payer');
  const projects = await api.get<{ id: string; code: string; root_map_id: string }[]>('/api/projects');
  expect(projects.map((project) => project.code)).toEqual(['PKG']);
  expect(projects[0]?.id).not.toBe(laid.projectId);
  expect(await contentOf(api, projects[0]?.root_map_id ?? '')).toEqual(before);
  const trash = await api.get<{ projects: { id: string; code: string }[] }>('/api/trash');
  expect(trash.projects.map((project) => [project.id, project.code])).toEqual([[laid.projectId, 'PKGA']]);
});
