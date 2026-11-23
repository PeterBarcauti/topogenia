// insert-and-bpmn.spec.ts
// Journeys of the 3.3 on the real host, in both modes: a .pmap inserted on a flow as a detailed step, which cuts that flow, undone by one Ctrl+Z (ADR 108); the map so assembled seen in BPMN 2.0, its sub-process entered, then exported as a .bpmn valid against the official schemas of the OMG (ADR 106).

import { readFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import { xsdErrors } from '../src/test/bpmnChecks.ts';
import type { Api } from './support/api.ts';
import { expectAccessible } from './support/axe.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

const MINIMAL = readFileSync(new URL('../../samples/minimal.pmap', import.meta.url), 'utf-8');

interface Layout {
  readonly mapId: string;
  readonly laneId: string;
  readonly flowId: string;
}

/** A project « INS », one lane, two steps E001 -> E002 joined by a flow; the map is not opened. */
async function layOut(api: Api): Promise<Layout> {
  const { mapId } = await api.project('INS', 'Achats groupés');
  const laneId = (await api.map(mapId)).lanes[0]?.id ?? '';
  const first = await api.step(mapId, laneId, 0, 'Commande reçue');
  const second = await api.step(mapId, laneId, 1, 'Contrôler la facture');
  return { mapId, laneId, flowId: await api.flow(mapId, first, second) };
}

/** The flows of the map read on the host, as « E001>E003 », sorted. */
async function arrows(api: Api, mapId: string): Promise<string[]> {
  const map = await api.map(mapId);
  const code = new Map(map.steps.map((step) => [step.id, step.code.replace('INS-', '')]));
  return map.flows.map((flow) => `${code.get(flow.source_step_id) ?? ''}>${code.get(flow.target_step_id) ?? ''}`).sort();
}

/** A right click on the line of the flow, between its two steps (a straight flow has no height, which Playwright takes for hidden). */
async function flowMenu(page: Page): Promise<void> {
  const [from, to] = [await stepOf(page, 'INS-E001').boundingBox(), await stepOf(page, 'INS-E002').boundingBox()];
  if (from === null || to === null) throw new Error('A step is not on the screen.');
  await page.mouse.click((from.x + from.width + to.x) / 2, from.y + from.height / 2, { button: 'right' });
}

test('a file inserted on a flow cuts it, the step detailed by the file; one Ctrl+Z gives the flow back (ADR 108)', async ({ page, api }) => {
  const { mapId, flowId } = await layOut(api);
  await openMap(page, mapId, 'INS-E002');
  await flowMenu(page);
  await page.getByTestId('menu-flow').getByRole('menuitem', { name: 'Insérer un fichier sur cette flèche…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Insérer un fichier comme étape détaillée' });
  await expect(dialog.getByTestId('insert-pmap-confirm')).toBeDisabled();

  // The file read before anything is written: what it holds, the label proposed, the place on the flow chosen.
  await dialog.getByTestId('insert-pmap-content').fill(MINIMAL);
  await expect(dialog.getByTestId('insert-pmap-preview')).toContainText('Étapes : 4 · Couloirs : 2 · Liens : 3 · Cartes : 1');
  await expect(dialog.getByTestId('insert-pmap-label')).toHaveValue("Demande d'achat");
  await expect(dialog.getByTestId('insert-pmap-after')).toHaveValue(`flow:${flowId}`);
  await expect(dialog.getByTestId('insert-pmap-after').locator('option:checked')).toHaveText('Sur la flèche Commande reçue → Contrôler la facture');
  expect(await arrows(api, mapId)).toEqual(['E001>E002']);
  await expectAccessible(page, "le dialogue d'insertion");

  await dialog.getByTestId('insert-pmap-confirm').click();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByText('Fichier inséré. Étapes : 4 · Cartes : 1')).toBeVisible();
  // The flow cut in two around the new step, which the view shows; its detail holds the map of the file.
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E003', 'E003>E002']);
  await expect(stepOf(page, 'INS-E003')).toBeInViewport();
  await expect(stepOf(page, 'INS-E003')).toHaveClass(/\bis-macro\b/);
  const inserted = (await api.map(mapId)).steps.find((step) => step.code === 'INS-E003');
  expect(inserted?.label).toBe("Demande d'achat");
  const detail = await api.map(inserted?.sub_map_id ?? '');
  expect(detail.steps.map((step) => step.label).sort()).toEqual(['Besoin identifié', 'Commande envoyée', 'Passer la commande', 'Saisir la demande']);

  // One Ctrl+Z: the step and its detail gone, the flow whole again.
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 450 } });
  await page.keyboard.press('Control+z');
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E002']);
  expect((await api.map(mapId)).flows.map((flow) => flow.id)).toEqual([flowId]);
  await expect(stepOf(page, 'INS-E003')).toHaveCount(0);
});

/** The file inserted on the flow by the host itself, with the version its preview reads. */
async function assemble(api: Api, layout: Layout): Promise<void> {
  const preview = await api.send<{ structure_version: number }>('POST', `/api/maps/${layout.mapId}/insert-pmap/preview`, { content: MINIMAL });
  await api.send('POST', `/api/maps/${layout.mapId}/insert-pmap`, {
    content: MINIMAL, with_sheets: true, label: "Demande d'achat", lane_id: layout.laneId, column: null, row: null, flow_id: layout.flowId,
  }, preview.structure_version);
}

test('the map assembled, seen in BPMN 2.0: its sub-process entered, the file exported valid against the schemas of the OMG (ADR 106)', async ({ page, api }) => {
  const layout = await layOut(api);
  await assemble(api, layout);
  await openMap(page, layout.mapId, 'INS-E003');

  // The shortcut of the map, the map holding the focus.
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 450 } });
  await page.keyboard.press('Alt+b');
  const drawing = page.getByTestId('bpmn-drawing');
  await expect(drawing).toBeVisible();
  await expect(page.getByTestId('map-canvas')).toHaveCount(0);
  await expect(page.getByTestId('bpmn-badge')).toHaveText('Schéma BPMN 2.0 · lecture seule');
  const kinds = async (): Promise<Record<string, string>> => Object.fromEntries(await Promise.all((await drawing.getByTestId('bpmn-node').all())
    .map(async (node): Promise<[string, string]> => [(await node.getAttribute('data-step')) ?? '', (await node.getAttribute('data-kind')) ?? ''])));
  // A task done in the lane of a person, no tool named, is a manual task (ADR 106); the step of the file, a sub-process.
  expect(await kinds()).toEqual({ 'INS-E001': 'manualTask', 'INS-E003': 'subProcess', 'INS-E002': 'manualTask' });
  await expect(drawing.getByTestId('bpmn-flow')).toHaveCount(2);
  await expectAccessible(page, 'la vue BPMN');

  // The .bpmn of the map, its sub-processes with it: valid against the five official schemas, the detail inside.
  await page.getByTestId('bpmn-export-open').click();
  const dialog = page.getByTestId('export-bpmn');
  await expect(dialog.getByTestId('export-bpmn-subprocesses')).toBeChecked();
  const download = page.waitForEvent('download');
  await dialog.getByTestId('export-bpmn-confirm').click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^INS_Achats-groupes_\d{4}-\d{2}-\d{2}\.bpmn$/);
  const xml = readFileSync(await file.path(), 'utf-8');
  expect(await xsdErrors(xml)).toEqual([]);
  expect(xml).toContain('<bpmn:subProcess id="Step_INS-E003"');
  expect(xml).toContain('Passer la commande');

  // A double click enters the detail, the view kept; Alt+B goes back to the map, there.
  await drawing.locator('[data-testid="bpmn-node"][data-step="INS-E003"]').dblclick();
  await expect(page.getByTestId('breadcrumb').locator('[aria-current="page"]')).toHaveText("Demande d'achat");
  await expect(page.getByTestId('bpmn-drawing').getByTestId('bpmn-node')).toHaveCount(4);
  await page.keyboard.press('Alt+b');
  await expect(page.getByTestId('map-canvas')).toBeVisible();
  await expect(page.getByTestId('bpmn-drawing')).toHaveCount(0);
});
