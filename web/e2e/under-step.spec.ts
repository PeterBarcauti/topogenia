// under-step.spec.ts
// Journey of what hangs under a step (AR-58, AR-128): in the browser, the frequency of a detailed step, its RACI badges and the label of a gateway leave the flows of their bottom point in sight, arrowhead included.

import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import type { Box, Point } from './support/map-editing.ts';

/**
 * The case of the demonstration (P6-T11): Dépense engagée -> Saisir la note (detailed, monthly) -> Valider la note ->
 * Conforme ? and the flow back « non » to Saisir la note, which reaches it by its bottom point from its right; an A and an I for it.
 */
async function layOut(api: Api): Promise<{ mapId: string; back: string }> {
  const { mapId } = await api.project('SOUS', 'Sous les étapes');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const manager = await api.lane(mapId, 'Manager');
  const accounting = await api.lane(mapId, 'Comptabilité');
  const ids = [
    await api.step(mapId, first, 0, 'Dépense engagée', 'start'), await api.step(mapId, first, 1, 'Saisir la note'),
    await api.step(mapId, manager, 2, 'Valider la note'), await api.step(mapId, accounting, 3, 'Conforme ?', 'decision'),
  ];
  for (let index = 0; index + 1 < ids.length; index++) await api.flow(mapId, ids[index] ?? '', ids[index + 1] ?? '');
  const back = await api.flow(mapId, ids[3] ?? '', ids[1] ?? '', 'non');
  const step = async () => (await api.map(mapId)).steps.find((candidate) => candidate.id === ids[1]);
  // Detailed, its map holding a step: only then is it shown detailed, with its frequency (spec 9.4).
  const subMap = (await api.send<{ id: string | null }>('POST', `/api/steps/${ids[1] ?? ''}/detail`, undefined, (await step())?.version)).id ?? '';
  await api.step(subMap, (await api.map(subMap)).lanes[0]?.id ?? '', 0, 'Photographier les justificatifs');
  await api.send('PUT', `/api/steps/${ids[1] ?? ''}/sheet`, { fields: { frequency: { value: 'monthly', number: null } }, procedure: [], free_text: null }, 0);
  await api.send('PUT', `/api/steps/${ids[1] ?? ''}/raci`, { lane_id: manager, role: 'A' }, (await step())?.version);
  await api.send('PUT', `/api/steps/${ids[1] ?? ''}/raci`, { lane_id: accounting, role: 'I' }, (await step())?.version);
  return { mapId, back };
}

/** Points of the path of a flow on the page, every 4 px over its last `length` px (from its end) or its first ones. */
async function pathPoints(page: Page, flowId: string, length: number, fromEnd: boolean): Promise<Point[]> {
  return page.getByTestId(`rf__edge-${flowId}`).locator('path.react-flow__edge-path').evaluate((path: SVGPathElement, [span, end]) => {
    const total = path.getTotalLength();
    const matrix = path.getScreenCTM();
    if (matrix === null) return [];
    return Array.from({ length: Math.floor(span / 4) + 1 }, (_, index) => {
      const at = end ? total - index * 4 : index * 4;
      return path.getPointAtLength(Math.max(0, Math.min(total, at))).matrixTransform(matrix);
    }).map((point) => ({ x: point.x, y: point.y }));
  }, [length, fromEnd] as const);
}

/** The boxes on the page of the elements found. */
const boxesOf = async (locator: Locator): Promise<Box[]> =>
  (await locator.evaluateAll((elements) => elements.map((element) => element.getBoundingClientRect())))
    .map((rect) => ({ x: rect.x, y: rect.y, width: rect.width, height: rect.height }));

/** The points inside a box, the arrowhead taken as 6 px each side of its line. */
const under = (points: Point[], boxes: Box[], halfWidth = 0): Point[] => points.filter((point) => boxes.some((box) =>
  point.x + halfWidth > box.x && point.x - halfWidth < box.x + box.width && point.y > box.y && point.y < box.y + box.height));

test('the flow back to a detailed step keeps its arrowhead in sight: its frequency and its RACI badges go aside (AR-128)', async ({ page, api }) => {
  const { mapId, back } = await layOut(api);
  await openMap(page, mapId, 'SOUS-E002');
  await page.getByRole('toolbar').getByRole('button', { name: 'RACI', exact: true }).click();
  const detailed = stepOf(page, 'SOUS-E002');
  await expect(detailed).toHaveClass(/\bis-under-left\b/);
  await expect(detailed.getByTestId('step-frequency')).toHaveText('Mensuelle');
  await expect(detailed.locator('.raci-badge')).toHaveText(['A Manager', 'I Comptabilité']);
  // The last 120 px of the flow back: its arrowhead (6 px each side), its vertical into the step, its run in the band under it.
  const end = await pathPoints(page, back, 120, true);
  const badges = [...await boxesOf(detailed.getByTestId('step-frequency')), ...await boxesOf(detailed.locator('.raci-badge'))];
  expect(badges).toHaveLength(3);
  expect(under(end.slice(0, 3), badges, 6)).toEqual([]);
  expect(under(end, badges)).toEqual([]);
  // The decision leaves by its bottom point: its label and its « A ? » go right of the axis, off the flow that leaves.
  const decision = stepOf(page, 'SOUS-E004');
  await expect(decision).toHaveClass(/\bis-under-right\b/);
  const start = await pathPoints(page, back, 60, false);
  expect(under(start, [...await boxesOf(decision.getByTestId('step-label')), ...await boxesOf(decision.locator('.raci-badge'))])).toEqual([]);
});
