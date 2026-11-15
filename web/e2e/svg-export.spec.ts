// svg-export.spec.ts
// The journey of the image SVG (plan 3.1, L7): the sample of the documentation exported as a PNG and as an image SVG of the same frame, the SVG drawn by the browser with every text of its steps, its copy of the map inside it on option, reopened from the File menu as the same map; the image and its drawing kept in test-results for the eye.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { Locator, Page } from '@playwright/test';
import { expect, stepOf, test } from './support/journey.ts';

const REPOSITORY = new URL('../../', import.meta.url);
const SAMPLE = fileURLToPath(new URL('samples/cloture-mensuelle.pmap', REPOSITORY));
const OUT = fileURLToPath(new URL('../test-results/svg-export/', import.meta.url));

async function openFileMenu(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Fichier', exact: true }).click();
  return page.getByRole('menu', { name: 'Menu Fichier' });
}

/** The sample opened from the home page, its root map shown. */
async function openSample(page: Page): Promise<void> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Ouvrir un fichier…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Ouvrir un fichier' });
  await dialog.locator('input[type="file"]').setInputFiles(SAMPLE);
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await page.getByRole('dialog', { name: 'Projet créé' }).getByRole('button', { name: 'Ouvrir la carte' }).click();
  await expect(stepOf(page, 'VIS-E004')).toBeVisible();
}

/** The PNG of the map, as the File menu gives it. */
async function png(page: Page): Promise<Buffer> {
  const download = page.waitForEvent('download');
  await (await openFileMenu(page)).getByRole('menuitem', { name: 'Exporter en image PNG' }).click();
  return readFileSync(await (await download).path());
}

/** The image SVG of the map, through its dialog, its copy ticked or not. */
async function svg(page: Page, editable: boolean): Promise<{ name: string; text: string }> {
  await (await openFileMenu(page)).getByRole('menuitem', { name: 'Exporter en image SVG…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Exporter en image SVG' });
  const box = dialog.getByRole('checkbox', { name: 'Inclure une copie modifiable' });
  await expect(box).not.toBeChecked();
  if (editable) await box.check();
  const download = page.waitForEvent('download');
  await dialog.getByRole('button', { name: 'Exporter', exact: true }).click();
  const file = await download;
  await expect(page.getByText('Export terminé : le fichier est dans le dossier Téléchargements.').last()).toBeVisible();
  return { name: file.suggestedFilename(), text: readFileSync(await file.path(), 'utf-8') };
}

/** The width and the height of a PNG, read in its header. */
const sizeOf = (image: Buffer): { width: number; height: number } => ({ width: image.readUInt32BE(16), height: image.readUInt32BE(20) });

test('the image SVG has the frame of the PNG, draws every step, carries its copy on option and reopens as the same map (plan 3.1, L7)', async ({ page, browser }) => {
  test.setTimeout(120_000);
  mkdirSync(OUT, { recursive: true });
  await openSample(page);
  const image = await png(page);
  const plain = await svg(page, false);
  const editable = await svg(page, true);
  writeFileSync(`${OUT}carte.png`, image);
  writeFileSync(`${OUT}carte.svg`, editable.text);

  expect(plain.name).toMatch(/^VIS_.+_\d{4}-\d{2}-\d{2}\.svg$/);
  expect(plain.text).not.toContain('<metadata');
  expect(editable.text).toMatch(/<metadata id="[a-z]+">[A-Za-z0-9+/=]+<\/metadata>/);
  expect(editable.text).not.toMatch(/<foreignObject|<script|var\(--|\bon\w+=/);
  // The same frame as the PNG, drawn there at twice the resolution.
  const frame = /<svg[^>]* width="(\d+)" height="(\d+)"/.exec(editable.text);
  const { width, height } = sizeOf(image);
  expect([Number(frame?.[1]) * 2, Number(frame?.[2]) * 2]).toEqual([width, height]);

  // The image drawn by the browser in a page of its own: every step there with its code.
  const viewer = await browser.newPage();
  await viewer.setContent(`<!doctype html><html><body style="margin:0">${editable.text.replace(/^<\?xml[^>]*>/, '')}</body></html>`);
  const codes = await viewer.locator('svg g.step text').allTextContents();
  expect(codes.filter((text) => /^VIS-E\d{3}$/.test(text)).sort()).toEqual(['VIS-E001', 'VIS-E002', 'VIS-E003', 'VIS-E004', 'VIS-E005', 'VIS-E006', 'VIS-E007', 'VIS-E008']);
  await viewer.screenshot({ path: `${OUT}carte-svg-dessinee.png`, fullPage: true });
  await viewer.close();

  // Reopened from the File menu: the dialog reads the copy, the project opens as the same map under another code.
  await (await openFileMenu(page)).getByRole('menuitem', { name: 'Ouvrir un fichier…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Ouvrir un fichier' });
  await dialog.locator('input[type="file"]').setInputFiles({ name: editable.name, mimeType: 'image/svg+xml', buffer: Buffer.from(editable.text) });
  await expect(dialog.getByRole('textbox', { name: 'Code court' })).toHaveValue('VIS');
  await dialog.getByRole('textbox', { name: 'Code court' }).fill('SVG');
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await expect(stepOf(page, 'SVG-E004')).toBeVisible();
  // Its sheets came with it: the pictograms of the detailed step.
  await expect(stepOf(page, 'SVG-E003').getByTestId('step-pictos')).toBeVisible();
});
