// fileMenu.ts
// The File menu in cascade for the tests: the entry of a command, its group opened first when it has one (ADR 117).

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { fileGroupOf } from '../features/map/state/commands/fileMenu.ts';

/** The entry of a command of the File menu (`file.png`) in the open menu: one click on its group first when the entry is in one that is closed. */
export async function fileEntry(commandId: string): Promise<HTMLElement> {
  const group = fileGroupOf(commandId);
  if (group !== undefined && screen.queryByTestId(`menu-file-${commandId}`) === null) await userEvent.click(screen.getByTestId(`menu-file-cascade-${group}`));
  return screen.getByTestId(`menu-file-${commandId}`);
}

/** Clicks the entry of a command of the File menu, after its group when it has one. */
export async function chooseFileEntry(commandId: string): Promise<void> {
  await userEvent.click(await fileEntry(commandId));
}
