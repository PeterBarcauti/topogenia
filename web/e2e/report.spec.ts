// report.spec.ts
// The journey of the report told by the AI (plan 3.1, L4) on the fake connector of the starter of the host: asked from the File menu, its summary shown in the panel, saved as a Markdown whose diagrams are written by the application and read by Mermaid, nothing written in the project.

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { expect, openMap, test } from './support/journey.ts';

/** A fictitious key: the connectors of the starter answer without calling anyone. */
const KEY = 'sk-ant-fictive-report-0000';

test('the report is asked from the File menu, shown in the panel and saved as a Markdown with its diagrams; the map stays as it was (plan 3.1, L4)', async ({ page, api }) => {
  const settings = await api.get<{ version: number }>('/api/admin/assistant');
  await api.send('PUT', '/api/admin/assistant', { connector: 'anthropic', secret: KEY, allowed_classifications: ['internal'] }, settings.version);
  const project = await api.project('RAP', 'Achats');
  const lane = (await api.map(project.mapId)).lanes[0]?.id ?? '';
  const first = await api.step(project.mapId, lane, 0, 'Saisir la demande', 'start');
  const second = await api.step(project.mapId, lane, 1, 'Valider la demande');
  await api.flow(project.mapId, first, second);
  const before = await api.map(project.mapId);
  await openMap(page, project.mapId, 'RAP-E001');

  await page.getByRole('button', { name: 'Fichier', exact: true }).click();
  await page.getByRole('menu', { name: 'Menu Fichier' }).getByRole('menuitem', { name: 'Rapport raconté par l’IA (Markdown)' }).click();
  // The chip of the request names the practice, never its key.
  await expect(page.getByTestId('assistant-panel').locator('.intent-chip').last()).toHaveText('Rapport');
  const report = page.getByTestId('assistant-report');
  await expect(report).toContainText('Le processus suit ses étapes dans l\'ordre de la carte.');

  // The panel kept in test-results for the eye.
  await page.getByTestId('assistant-panel').screenshot({ path: fileURLToPath(new URL('../test-results/report/volet.png', import.meta.url)) });
  const download = page.waitForEvent('download');
  await report.getByTestId('assistant-report-save').click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^RAP_.+\.md$/);
  const text = readFileSync(await file.path(), 'utf-8');
  expect(text).toContain('Saisir la demande');
  const diagrams = [...text.matchAll(/```mermaid\n([\s\S]*?)```/g)].map((block) => block[1] ?? '');
  expect(diagrams.length).toBeGreaterThanOrEqual(3);
  expect(diagrams.some((diagram) => diagram.includes('flowchart LR'))).toBe(true);

  // Every diagram read by the Mermaid of the tests, in the page of the application (its policy allows no script from outside).
  const viewer = await page.context().newPage();
  await viewer.setContent('<!doctype html><html><body></body></html>');
  await viewer.addScriptTag({ path: new URL('../node_modules/mermaid/dist/mermaid.min.js', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1') });
  const refused = await viewer.evaluate(async (all) => {
    const engine = (window as unknown as { mermaid: { parse: (text: string) => Promise<unknown> } }).mermaid;
    const faults: string[] = [];
    for (const diagram of all) await engine.parse(diagram).catch((error: unknown) => faults.push(String(error)));
    return faults;
  }, diagrams);
  expect(refused).toEqual([]);
  await viewer.close();

  // An export: nothing written in the project.
  const after = await api.map(project.mapId);
  expect([after.version, after.steps.length, after.flows.length]).toEqual([before.version, before.steps.length, before.flows.length]);
});
