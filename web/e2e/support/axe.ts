// axe.ts
// axe-core on the page as shown (spec 11, NF08: WCAG 2.2 AA), once its dialogs and menus have faded in; each violation said with its rule and the elements in fault.

import { AxeBuilder } from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';

/** The rules of WCAG 2.0, 2.1 and 2.2 at levels A and AA (NF08). */
const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/** No violation on the page as shown; each one said with its rule and the elements in fault, so that a failure reads at once. */
export async function expectAccessible(page: Page, what: string): Promise<void> {
  // The page as it stays: a dialog or a menu fades in for 150 to 200 ms (spec 9.5), its contrast is read once it is in.
  // Waited until no finite animation runs, those started meanwhile too; one cancelled (the transition of the button that
  // opened the dialog, cut by the dialog) waits no more.
  await page.evaluate(async () => {
    for (let round = 0; round < 10; round += 1) {
      const running = document.getAnimations().filter((animation) => animation.playState === 'running' && animation.effect?.getComputedTiming().iterations !== Infinity);
      if (running.length === 0) return;
      await Promise.all(running.map((animation) => animation.finished.catch(() => undefined)));
    }
  });
  const results = await new AxeBuilder({ page }).withTags(WCAG).analyze();
  const found = results.violations.map((violation) => `${violation.id} (${violation.impact ?? ''}): ${violation.nodes.map((node) => node.target.join(' ')).join(' | ')}`);
  expect(found, what).toEqual([]);
}
