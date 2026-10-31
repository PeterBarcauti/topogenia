// exports.spec.ts
// Journeys of the exports (annex B 8.3, spec 11): the whole map as a PNG in high resolution, a PDF A3 landscape with its title, a workbook of three sheets, under the policy of the host.

import { readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import type ExcelType from 'exceljs';
import type { Locator, Page } from '@playwright/test';
import { COLUMN_WIDTH, FREE_BAND, LANE_HEADER_WIDTH, LEFT_MARGIN, STEP_HEIGHT, STEP_WIDTH } from '../src/features/map/model/geometry/constants.ts';
import { pdfTexts } from '../src/test/pdf.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

// exceljs is a CommonJS module: loaded as such in this ES module.
const Excel = createRequire(import.meta.url)('exceljs') as typeof ExcelType;

const COLUMNS = 12;

declare global {
  interface Window {
    violations: string[];
  }
}

/** Every violation of the policy of the host is kept, from the first script of the page on. */
async function watchViolations(page: Page): Promise<void> {
  await page.addInitScript(() => {
    window.violations = [];
    document.addEventListener('securitypolicyviolation', (event) => window.violations.push(`${event.violatedDirective} ${event.blockedURI}`));
  });
}

/** Twelve steps in a row on the first lane, so that the last ones are off the screen; an A for the last one on a second lane. */
async function layOut(page: Page, api: Api, projectName = 'Clôture mensuelle'): Promise<void> {
  await watchViolations(page);
  const { mapId } = await api.project('EXP', projectName, 'fr', 'confidential');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const direction = await api.lane(mapId, 'Direction');
  const ids: string[] = [];
  for (let column = 0; column < COLUMNS; column++) ids.push(await api.step(mapId, first, column, `Étape ${column + 1}`));
  for (let index = 0; index + 1 < COLUMNS; index++) await api.flow(mapId, ids[index] ?? '', ids[index + 1] ?? '');
  const last = (await api.map(mapId)).steps.find((step) => step.id === ids[COLUMNS - 1]);
  await api.send('PUT', `/api/steps/${last?.id ?? ''}/raci`, { lane_id: direction, role: 'A' }, last?.version);
  await openMap(page, mapId, 'EXP-E001');
  // The last step is off the screen (the canvas of the 2.0 draws every element, ADR 066 section 2).
  await expect(stepOf(page, 'EXP-E012')).not.toBeInViewport();
}

/** Runs an entry of the File menu and gives the file the browser receives. */
async function exported(page: Page, entry: 'png' | 'pdf' | 'xlsx'): Promise<{ name: string; content: Buffer }> {
  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await page.getByTestId(`menu-file-file.${entry}`).click();
  const file = await download;
  await expect(page.getByText('Export terminé : le fichier est dans le dossier Téléchargements.')).toBeVisible();
  return { name: file.suggestedFilename(), content: readFileSync(await file.path()) };
}

/** The policy of the host stays in force and nothing broke it: an inline script added now is refused, and is the only violation. */
async function expectPolicyKept(page: Page): Promise<void> {
  expect(await page.evaluate(() => window.violations)).toEqual([]);
  await page.evaluate(() => {
    const script = document.createElement('script');
    script.textContent = 'window.violations.push("ran")';
    document.head.appendChild(script);
  });
  await expect.poll(() => page.evaluate(() => window.violations)).toEqual(['script-src-elem inline']);
}

const today = (): string => new Date().toISOString().slice(0, 10);

/** The margin round a captured map (`export/capture.ts`), and the resolution of an export of this size (`pixelRatio`: 2 below 12 000 px). */
const CAPTURE_MARGIN = 24;
const RATIO = 2;

interface Box { x: number; y: number; width: number; height: number }

/** The box of the step of a column of row 0 in the image: the lane headers start the map, the step sits in the free band of its row. */
const boxOf = (column: number): Box => ({
  x: (CAPTURE_MARGIN + LANE_HEADER_WIDTH + LEFT_MARGIN + column * COLUMN_WIDTH) * RATIO,
  y: (CAPTURE_MARGIN + FREE_BAND) * RATIO,
  width: STEP_WIDTH * RATIO,
  height: STEP_HEIGHT * RATIO,
});

/** The mean ink (0 white, 255 black) of each box of the image, the PNG drawn in a canvas of the page. */
function inksOf(page: Page, png: Buffer, boxes: Box[]): Promise<number[]> {
  return page.evaluate(async ([base64, all]) => {
    const image = new Image();
    image.src = `data:image/png;base64,${base64}`;
    await image.decode();
    const canvas = document.createElement('canvas');
    [canvas.width, canvas.height] = [image.width, image.height];
    const context = canvas.getContext('2d');
    if (context === null) return [];
    context.drawImage(image, 0, 0);
    return all.map((box) => {
      const { data } = context.getImageData(box.x, box.y, box.width, box.height);
      let ink = 0;
      for (let index = 0; index < data.length; index += 4) ink += 255 - ((data[index] ?? 0) + (data[index + 1] ?? 0) + (data[index + 2] ?? 0)) / 3;
      return ink / (data.length / 4);
    });
  }, [png.toString('base64'), boxes] as const);
}

test('PNG of the whole map, off-screen steps included, in high resolution, under the policy of the host (B-8.3-11)', async ({ page, api }, testInfo) => {
  await layOut(page, api);
  const { name, content } = await exported(page, 'png');
  // Kept in the results of the run, for the eye.
  writeFileSync(testInfo.outputPath('map.png'), content);
  expect(name).toBe(`EXP_Cloture-mensuelle_${today()}.png`);
  expect(content.subarray(1, 4).toString('ascii')).toBe('PNG');
  const [width, height] = [content.readUInt32BE(16), content.readUInt32BE(20)];
  // Twelve columns of 220 at a resolution of two at least: far wider than the screen.
  expect(width).toBeGreaterThan(2 * (COLUMNS - 1) * 220 + 2 * 170);
  expect(height).toBeGreaterThan(2 * 2 * 120);
  // Back to the screen as it was: the view has not moved to the last step.
  await expect(stepOf(page, 'EXP-E012')).not.toBeInViewport();
  await expect(stepOf(page, 'EXP-E001')).toBeInViewport();
  await expectPolicyKept(page);
  // Every step is in the image, those off the screen as sharp as the first one; the free column after them is bare.
  const inks = await inksOf(page, content, Array.from({ length: COLUMNS + 1 }, (_, column) => boxOf(column)));
  const [first = 0, bare = 0] = [inks[0], inks[COLUMNS]];
  for (const ink of inks.slice(0, COLUMNS)) expect(ink).toBeGreaterThan(first * 0.85);
  expect(first).toBeGreaterThan(5);
  expect(bare).toBeLessThan(first / 3);
});

test('PDF A3 landscape with the title of the map in Inter, whatever its language, under the policy of the host (B-8.3-12, AR-52)', async ({ page, api }, testInfo) => {
  // A name in Polish and Turkish: letters outside Latin-1, which the standard fonts of a PDF cannot print.
  await layOut(page, api, 'Księgowość – Şube');
  const { name, content } = await exported(page, 'pdf');
  // Kept in the results of the run, for the eye.
  writeFileSync(testInfo.outputPath('map.pdf'), content);
  expect(name).toBe(`EXP_Ksiegowosc-Sube_${today()}.pdf`);
  const text = content.toString('latin1');
  expect(text.startsWith('%PDF-')).toBe(true);
  // A3 landscape = 1190.55 x 841.89 points.
  const [, width, height] = /\/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]/.exec(text) ?? [];
  expect(Number(width)).toBeCloseTo(1190.55, 1);
  expect(Number(height)).toBeCloseTo(841.89, 1);
  expect(text).toContain('/Subtype /Image');
  // The faces of Inter read from the application under its policy, written into the file (AR-52).
  const [title, , , classification] = pdfTexts(text);
  expect([title?.text, title?.face]).toEqual(['EXP - Księgowość – Şube', 'Inter-Bold']);
  // The classification of the project is printed on it (AR-127, S-4.1-08).
  expect(classification?.text).toBe('Classification : Confidentiel');
  await expectPolicyKept(page);
});

test('workbook of three sheets: steps with their lane and RACI, links, matrix, under the policy of the host (B-8.3-13)', async ({ page, api }) => {
  await layOut(page, api);
  const { name, content } = await exported(page, 'xlsx');
  expect(name).toBe(`EXP_Cloture-mensuelle_${today()}.xlsx`);
  const book = new Excel.Workbook();
  await book.xlsx.load(content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength) as ArrayBuffer);
  expect(book.worksheets.map((sheet) => sheet.name)).toEqual(['Étapes', 'Liens', 'Matrice RACI']);
  const steps = book.getWorksheet('Étapes');
  expect(steps?.rowCount).toBe(COLUMNS + 1);
  const headers = (steps?.getRow(1).values ?? []) as unknown[];
  const last = (steps?.getRow(COLUMNS + 1).values ?? []) as unknown[];
  const cell = (header: string): unknown => last[headers.indexOf(header)];
  expect(['Code', 'Chemin', 'Étape', 'Type', 'Réalise', 'Approuve'].map(cell)).toEqual(['EXP-E012', '12', 'Étape 12', 'Étape', 'Acteur 1', 'Direction']);
  expect(book.getWorksheet('Liens')?.rowCount).toBe(COLUMNS);
  const matrix = book.getWorksheet('Matrice RACI');
  expect((matrix?.getRow(1).values as unknown[]).slice(1)).toEqual(['Code', 'Étape', 'Acteur 1', 'Direction']);
  expect((matrix?.getRow(COLUMNS + 1).values as unknown[]).slice(1)).toEqual(['EXP-E012', 'Étape 12', 'R', 'A']);
  // The classification is in the print header of each sheet (AR-127).
  for (const sheet of book.worksheets) expect(sheet.headerFooter.oddHeader).toContain('Confidentiel');
  await expectPolicyKept(page);
});

/** A box of the screen in the image of an export: back to the map by the view, then from the left of the lanes and the top of the first, margin 24, resolution 2. */
async function inImage(page: Page, element: Locator): Promise<Box> {
  const [box, pane, view] = await Promise.all([
    element.boundingBox(),
    page.locator('.react-flow').boundingBox(),
    page.locator('.react-flow__viewport').evaluate((viewport) => {
      const matrix = new DOMMatrixReadOnly(getComputedStyle(viewport).transform);
      return { x: matrix.m41, y: matrix.m42, zoom: matrix.m11 };
    }),
  ]);
  if (box === null || pane === null) throw new Error('The element or the canvas is not drawn.');
  const flowX = (box.x - pane.x - view.x) / view.zoom;
  const flowY = (box.y - pane.y - view.y) / view.zoom;
  return {
    x: Math.round((flowX + LANE_HEADER_WIDTH + CAPTURE_MARGIN) * RATIO), y: Math.round((flowY + CAPTURE_MARGIN) * RATIO),
    width: Math.round((box.width / view.zoom) * RATIO), height: Math.round((box.height / view.zoom) * RATIO),
  };
}

/** A step with a sheet begun (its gauge), a detailed step brought back into the flow (its frame), a step without anything. */
async function layOutFrameAndGauge(page: Page, api: Api): Promise<void> {
  await watchViolations(page);
  const { mapId } = await api.project('XPF', 'Cadre et jauge');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const ids = [await api.step(mapId, lane, 0, 'Recevoir'), await api.step(mapId, lane, 1, 'Saisir'), await api.step(mapId, lane, 2, 'Classer')];
  await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(mapId, ids[1] ?? '', ids[2] ?? '');
  await api.send('PUT', `/api/steps/${ids[0] ?? ''}/sheet`, { fields: { purpose: { value: 'Recevoir la demande', number: null } }, procedure: [], free_text: null }, 0);
  const version = async (id: string) => (await api.map(mapId)).steps.find((step) => step.id === id)?.version;
  const subMap = (await api.send<{ id: string | null }>('POST', `/api/steps/${ids[1] ?? ''}/detail`, undefined, await version(ids[1] ?? ''))).id ?? '';
  const subLane = (await api.map(subMap)).lanes[0]?.id ?? '';
  await api.flow(subMap, await api.step(subMap, subLane, 0, 'Préparer'), await api.step(subMap, subLane, 1, 'Vérifier'));
  await api.send('POST', `/api/steps/${ids[1] ?? ''}/dissolve`, undefined, await version(ids[1] ?? ''));
  await openMap(page, mapId, 'XPF-E001');
  await expect(page.getByTestId('dissolution-frame')).toBeVisible();
  await expect(stepOf(page, 'XPF-E001').getByTestId('step-gauge')).toBeVisible();
}

test('the PNG on the white of the page, the margin of the lanes drawn, neither the frame nor the gauge (B-2.14-04, B-2.12-25, B-2.9-20, B-2.10-26)', async ({ page, api }) => {
  await layOutFrameAndGauge(page, api);
  const frame = await inImage(page, page.getByTestId('dissolution-frame'));
  const gauge = await inImage(page, stepOf(page, 'XPF-E001').getByTestId('step-gauge'));
  // The same band at the bottom of a step without a sheet begun.
  const plainStep = await inImage(page, stepOf(page, 'XPF-E003'));
  const plain = { ...gauge, x: plainStep.x + (gauge.x - (await inImage(page, stepOf(page, 'XPF-E001'))).x) };
  const margin = { x: CAPTURE_MARGIN * RATIO + 8, y: (CAPTURE_MARGIN + 40) * RATIO, width: (LANE_HEADER_WIDTH - 16) * RATIO, height: 40 * RATIO };
  const { content } = await exported(page, 'png');
  const [corner, laneMargin, frameTop, frameLeft, gaugeInk, plainInk] = await inksOf(page, content, [
    { x: 0, y: 0, width: 8, height: 8 }, margin,
    // The top of its left side only: the flow into the steps brought back crosses it lower down.
    { ...frame, height: 20 * RATIO }, { ...frame, width: 6 * RATIO, height: 40 * RATIO },
    gauge, plain,
  ]);
  // The ground of the page (--surface, white), the pastel margin of the lane with its name, kept in the image.
  expect(corner).toBeLessThan(0.5);
  expect(laneMargin).toBeGreaterThan(5);
  // No frame: its label and its dashed border would ink the band above the steps brought back, and its left side.
  expect(frameTop).toBeLessThan(0.5);
  expect(frameLeft).toBeLessThan(0.5);
  // No gauge: the foot of the step with a sheet begun is drawn as the one of the step without.
  expect(Math.abs((gaugeInk ?? 0) - (plainInk ?? 0))).toBeLessThan(2);
  await expectPolicyKept(page);
});
