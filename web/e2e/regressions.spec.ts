// regressions.spec.ts
// Defects of the map reported on the 1.x, replayed so that they never come back (annex B 8.3): flows kept after a detail, a drop kept in its lane, a flow deleted by its key, two quick arrows.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, edges, labelInput, laneOf, middleOfFlow, stepIn, zone } from './support/map-editing.ts';

interface Placed {
  readonly lane: number;
  readonly column: number;
  readonly row?: number;
}

/** A map of three lanes (the first one, B, C), its steps placed and linked; the ids of its lanes and steps. */
async function layOut(page: Page, api: Api, code: string, steps: readonly Placed[], links: readonly (readonly [number, number])[] = []) {
  const { mapId } = await api.project(code, code);
  await api.lane(mapId, 'B');
  await api.lane(mapId, 'C');
  const lanes = [await laneOf(api, mapId, 0), await laneOf(api, mapId, 1), await laneOf(api, mapId, 2)];
  const ids: string[] = [];
  for (const [index, placed] of steps.entries()) ids.push(await api.step(mapId, lanes[placed.lane] ?? '', placed.column, `E${String(index)}`, 'task', placed.row ?? 0));
  for (const [source, target] of links) await api.flow(mapId, ids[source] ?? '', ids[target] ?? '');
  await openMap(page, mapId, `${code}-E001`);
  return { mapId, lanes, ids };
}

/** The flows stay drawn: their number, and a path for each. */
async function expectFlowsDrawn(page: Page, count: number): Promise<void> {
  await expect(edges(page)).toHaveCount(count);
  for (const path of await edges(page).locator('path.react-flow__edge-path').all()) await expect(path).toHaveAttribute('d', /^M -?\d/);
}

// The defect showed once out of two in the 1.x: the journey is played several times.
for (let run = 1; run <= 4; run++) {
  test(`detailing several steps keeps every flow drawn (B-8.3-47, B-2.1-29, B-2.3-03, B-2.3-30, run ${String(run)})`, async ({ page, api }) => {
    const { mapId } = await api.project('FLE', 'Fleches');
    const lane = await laneOf(api, mapId);
    const ids: string[] = [];
    for (let column = 0; column < 5; column++) ids.push(await api.step(mapId, lane, column, `Etape ${String(column)}`));
    for (let index = 0; index < 4; index++) await api.flow(mapId, ids[index] ?? '', ids[index + 1] ?? '');
    await openMap(page, mapId, 'FLE-E001');
    await expectFlowsDrawn(page, 4);

    await stepOf(page, 'FLE-E002').click();
    await stepOf(page, 'FLE-E003').click({ modifiers: ['Shift'] });
    await expect(page.locator('.react-flow__node-step.selected')).toHaveCount(2);
    await zone(page, 'Édition').getByRole('button', { name: 'Grouper', exact: true }).click();
    // The new step is chosen and named at once (B-2.3-30, B-2.9-10).
    await expect(labelInput(page)).toBeFocused();
    await expect(stepOf(page, 'FLE-E006')).toHaveClass(/is-selected/);
    // Three flows: Etape 0 -> block -> Etape 3 -> Etape 4. They stay, while and after the block is named.
    await expectFlowsDrawn(page, 3);
    await page.keyboard.type('Bloc');
    await page.keyboard.press('Enter');
    await expect(stepOf(page, 'FLE-E006').getByTestId('step-label')).toHaveText('Bloc');
    await expectFlowsDrawn(page, 3);
  });
}

test('dragging the only step of the second row of a lane keeps it in its lane (B-8.3-48)', async ({ page, api }) => {
  const { mapId, lanes, ids } = await layOut(page, api, 'GLI', [{ lane: 0, column: 0 }, { lane: 0, column: 0, row: 1 }, { lane: 1, column: 0 }]);
  const box = await boxOf(stepOf(page, 'GLI-E002'));
  await page.mouse.move(box.x + 40, box.y + 30);
  await page.mouse.down();
  await page.mouse.move(box.x + 40 + 220, box.y + 30, { steps: 12 });
  await page.mouse.up();
  await expect.poll(async () => stepIn(await api.map(mapId), 'GLI-E002').column).toBe(1);
  const moved = stepIn(await api.map(mapId), 'GLI-E002');
  expect([moved.id, moved.lane_id]).toEqual([ids[1], lanes[0]]);
});

test('a flow is chosen by a click and deleted with Delete (B-8.3-49, B-2.3-31, B-2.3-32)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api, 'FLS', [{ lane: 0, column: 0 }, { lane: 0, column: 1 }, { lane: 0, column: 2 }], [[0, 1], [1, 2]]);
  const flowId = (await api.map(mapId)).flows[0]?.id ?? '';
  const middle = await middleOfFlow(page, flowId);
  await page.mouse.click(middle.x, middle.y);
  await expect(page.getByTestId(`rf__edge-${flowId}`)).toHaveClass(/selected/);
  await page.keyboard.press('Delete');
  await expect(edges(page)).toHaveCount(1);
  expect((await api.map(mapId)).flows.map((flow) => flow.id)).not.toContain(flowId);
  expect((await api.map(mapId)).flows).toHaveLength(1);
});

test('two quick presses of the right arrow move two columns on (B-8.3-50, B-2.5-18)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api, 'CLA', [{ lane: 0, column: 0 }, { lane: 1, column: 0 }]);
  await stepOf(page, 'CLA-E001').click();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect.poll(async () => stepIn(await api.map(mapId), 'CLA-E001').column).toBe(2);
});
