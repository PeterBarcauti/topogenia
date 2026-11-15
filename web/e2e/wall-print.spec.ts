// wall-print.spec.ts
// Journey of the poster of a map (Makigami M9): « Imprimer en grand… » of the File menu, A4 landscape on two pages across; the PDF saved with as many sheets A4 landscape as the dialog said, each page named under it, its image drawn by the browser from the image SVG and not blank, under the policy of the host.

import { readFileSync, writeFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import { pdfTexts } from '../src/test/pdf.ts';
import { expect, openMap, test } from './support/journey.ts';

declare global {
  interface Window {
    violations: string[];
  }
}

const today = (): string => new Date().toISOString().slice(0, 10);

/** The JPEG images of a PDF, in the order of the file: the bytes of each stream of a DCT image. */
function jpegsOf(content: Buffer): Buffer[] {
  const text = content.toString('latin1');
  const images: Buffer[] = [];
  for (const match of text.matchAll(/\/Filter \/DCTDecode[\s\S]*?stream\n/g)) {
    const start = match.index + match[0].length;
    images.push(content.subarray(start, text.indexOf('\nendstream', start)));
  }
  return images;
}

/** The mean ink of a JPEG (0 white, 255 black) and its size, drawn in a canvas of the page. */
function inkOf(page: Page, jpeg: Buffer): Promise<{ ink: number; width: number; height: number }> {
  return page.evaluate(async (base64) => {
    const image = new Image();
    image.src = `data:image/jpeg;base64,${base64}`;
    await image.decode();
    const canvas = document.createElement('canvas');
    [canvas.width, canvas.height] = [image.width, image.height];
    const context = canvas.getContext('2d');
    if (context === null) return { ink: 0, width: 0, height: 0 };
    context.drawImage(image, 0, 0);
    const { data } = context.getImageData(0, 0, image.width, image.height);
    let ink = 0;
    for (let index = 0; index < data.length; index += 4) ink += 255 - ((data[index] ?? 0) + (data[index + 1] ?? 0) + (data[index + 2] ?? 0)) / 3;
    return { ink: ink / (data.length / 4), width: image.width, height: image.height };
  }, jpeg.toString('base64'));
}

test('the map printed large: as many sheets A4 landscape as said, each named, its image drawn and not blank, under the policy of the host (Makigami M9)', async ({ page, api }, testInfo) => {
  await page.addInitScript(() => {
    window.violations = [];
    document.addEventListener('securitypolicyviolation', (event) => window.violations.push(`${event.violatedDirective} ${event.blockedURI}`));
  });
  const { mapId } = await api.project('MUR', 'Clôture mensuelle', 'fr', 'internal');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const second = await api.lane(mapId, 'Comptabilité');
  const ids: string[] = [];
  for (let column = 0; column < 8; column++) ids.push(await api.step(mapId, column % 2 === 0 ? first : second, column, `Étape ${String(column + 1)}`));
  for (let index = 0; index + 1 < ids.length; index++) await api.flow(mapId, ids[index] ?? '', ids[index + 1] ?? '');
  await openMap(page, mapId, 'MUR-E001');

  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.wall').click();
  const dialog = page.getByRole('dialog', { name: 'Imprimer en grand' });
  await expect(dialog.getByRole('radio', { name: 'A4 paysage' })).toBeChecked();
  await dialog.getByRole('combobox', { name: 'Pages en largeur' }).selectOption('2');
  const summary = (await dialog.getByTestId('wall-print-summary').textContent()) ?? '';
  const [, count = '0', across = '0', down = '0'] = /^Pages : (\d+) \((\d+) × (\d+)\)/.exec(summary) ?? [];
  expect([Number(across), Number(count)]).toEqual([2, 2 * Number(down)]);

  const download = page.waitForEvent('download');
  await dialog.getByRole('button', { name: 'Créer le PDF' }).click();
  const file = await download;
  await expect(page.getByText('Export terminé : le fichier est dans le dossier Téléchargements.')).toBeVisible();
  const content = readFileSync(await file.path());
  // Kept in the results of the run, for the eye.
  writeFileSync(testInfo.outputPath('poster.pdf'), content);
  expect(file.suggestedFilename()).toBe(`MUR_Cloture-mensuelle-affiche_${today()}.pdf`);
  const text = content.toString('latin1');
  expect(text.match(/\/Type \/Page\b/g)).toHaveLength(Number(count));
  // A4 landscape = 841.89 x 595.28 points.
  const [, width, height] = /\/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]/.exec(text) ?? [];
  expect([Number(width), Number(height)].map((value) => Math.round(value))).toEqual([842, 595]);
  const names = pdfTexts(text, 210).filter((line) => line.face === 'Inter-Bold').map((line) => line.text);
  expect(names[0]).toBe(`A1 (1/${count})`);
  expect(names).toHaveLength(Number(count));
  // Each page its image, drawn at 150 dots per inch from the image SVG: the first one carries the map, not a white sheet.
  const jpegs = jpegsOf(content);
  expect(jpegs).toHaveLength(Number(count));
  const drawn = await inkOf(page, jpegs[0] ?? Buffer.alloc(0));
  expect(drawn.width).toBe(Math.round((277 / 25.4) * 150));
  expect(drawn.ink).toBeGreaterThan(2);
  expect(await page.evaluate(() => window.violations)).toEqual([]);
});
