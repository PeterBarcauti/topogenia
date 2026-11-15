// scenario.ts
// The walk of the demonstration (P6-T11, S-P-18, S-P-19): the scenario of docs/recette/demonstration played on the real host, a capture of the screen at each step, the exported files kept.

import { test } from '../../e2e/support/journey.ts';
import { createProject, drawLanes, drawSteps, firstLaunch, linkSteps } from './drawing.ts';
import { exportPackage, exports, importPackage, inEnglish, members, translations, trash } from './sharing.ts';
import { emptyFolders } from './walk.ts';
import { detailStep, dissolve, firstSheet, raci, secondSheet } from './work.ts';

test('the scenario of the demonstration: an expense report from the empty project to the detailed map, shared and exported', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  emptyFolders();
  await test.step('1. first launch', () => firstLaunch(page));
  await test.step('2. the project created', () => createProject(page));
  await test.step('3. the lanes', () => drawLanes(page));
  await test.step('4. the steps', () => drawSteps(page));
  await test.step('5. the flows', () => linkSteps(page));
  await test.step('6. a sheet typed', () => firstSheet(page));
  await test.step('7. a sheet validated', () => secondSheet(page));
  await test.step('8. the RACI', () => raci(page));
  await test.step('9. a step detailed', () => detailStep(page));
  await test.step('10. a detail brought back', () => dissolve(page));
  await test.step('11. the translations', () => translations(page));
  await test.step('12. in English', () => inEnglish(page));
  await test.step('13. the exports', () => exports(page));
  const file = await test.step('14. the package', () => exportPackage(page));
  await test.step('15. the package imported', () => importPackage(page, file));
  await test.step('16. the trash', () => trash(page));
  await test.step('17. the members', () => members(page));
});
