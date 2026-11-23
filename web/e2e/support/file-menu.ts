// file-menu.ts
// The File menu in cascade for the journeys: the entry of a command, its group opened first when it has one (ADR 117).

import type { Locator, Page } from '@playwright/test';
import { fileGroupOf } from '../../src/features/map/state/commands/fileMenu.ts';

/** The entry of a command of the File menu (`file.png`) in the open menu: one click on its group first when the entry is in one. */
export async function fileEntry(page: Page, commandId: string): Promise<Locator> {
  const group = fileGroupOf(commandId);
  if (group !== undefined) await page.getByTestId(`menu-file-cascade-${group}`).click();
  return page.getByTestId(`menu-file-${commandId}`);
}

/** Opens the File menu of the header and runs a command of it, its group opened first when it has one. */
export async function runFromFileMenu(page: Page, commandId: string): Promise<void> {
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, commandId)).click();
}
