// trash.spec.ts
// Journey of the trash (1.x corbeille.spec.ts): a project put in the trash once its code is typed, restored, then deleted for good, its code free again.

import type { Page } from '@playwright/test';
import { expect, test } from './support/journey.ts';

/** The projects listed on the home page, one line each. */
const projectLines = (page: Page) => page.locator('.project-list > li');

/** Puts a project in the trash from its line: « Supprimer… », its code typed, « Mettre à la corbeille ». */
async function putInTrash(page: Page, code: string): Promise<void> {
  await page.getByTestId(`project-${code}`).getByRole('button', { name: 'Supprimer…' }).click();
  const dialog = page.getByRole('dialog', { name: `Supprimer le projet ${code}` });
  await dialog.getByRole('textbox').fill(code);
  await dialog.getByRole('button', { name: 'Mettre à la corbeille' }).click();
  await expect(dialog).toHaveCount(0);
}

test('a project goes to the trash once its code is typed, is restored, then deleted for good (B-8.3-08, B-8.3-09, B-8.3-10)', async ({ page, api }) => {
  // Request of 20/10/2026 (1.x): a replaced project must no longer clutter the home page.
  await api.project('VIEUX', 'Ventes - ancienne version');
  await api.project('NEUF', 'Ventes - version actuelle');
  await page.goto('/');
  await expect(projectLines(page)).toHaveCount(2);

  // The button arms only with the exact code (B-1.3-22); the typing is put in capitals.
  await page.getByTestId('project-VIEUX').getByRole('button', { name: 'Supprimer…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Supprimer le projet VIEUX' });
  await expect(dialog).toContainText('restaurable pendant 30 jours');
  const confirm = dialog.getByRole('button', { name: 'Mettre à la corbeille' });
  await expect(confirm).toBeDisabled();
  await dialog.getByRole('textbox').fill('NEUF');
  await expect(confirm).toBeDisabled();
  await dialog.getByRole('textbox').fill('vieu');
  await expect(confirm).toBeDisabled();
  await dialog.getByRole('textbox').fill('vieux');
  await expect(dialog.getByRole('textbox')).toHaveValue('VIEUX');
  await expect(confirm).toBeEnabled();
  await confirm.click();
  await expect(page.getByText('Projet VIEUX mis à la corbeille.')).toBeVisible();

  // The home page shows only the live project; the old one is in the trash, with the day it is erased.
  await expect(projectLines(page)).toHaveCount(1);
  await expect(projectLines(page)).toContainText('NEUF');
  const trash = page.getByTestId('trash');
  await expect(trash.getByRole('heading')).toHaveText('Corbeille (1)');
  await expect(trash).toContainText('effacé définitivement le');

  // Restore (B-1.3-19): the project is back, the trash is empty and hidden.
  await trash.getByRole('button', { name: 'Restaurer' }).click();
  await expect(page.getByText('Projet VIEUX restauré.')).toBeVisible();
  await expect(projectLines(page)).toHaveCount(2);
  await expect(trash).toHaveCount(0);

  // In the trash again, then deleted for good by the administrator, after a confirmation (B-1.3-20).
  await putInTrash(page, 'VIEUX');
  await trash.getByRole('button', { name: 'Supprimer définitivement' }).click();
  const erase = page.getByRole('dialog', { name: 'Supprimer définitivement VIEUX' });
  await expect(erase).toContainText('Le code VIEUX redeviendra libre.');
  await erase.getByRole('button', { name: 'Supprimer définitivement' }).click();
  await expect(page.getByText('Projet VIEUX supprimé définitivement.')).toBeVisible();
  await expect(trash).toHaveCount(0);
  await expect(projectLines(page)).toHaveCount(1);
  expect((await api.get<{ projects: unknown[] }>('/api/trash')).projects).toEqual([]);

  // The code is free again.
  await api.project('VIEUX', 'Ventes - code réutilisé');
  await page.reload();
  await expect(projectLines(page)).toHaveCount(2);
  await expect(page.getByTestId('project-VIEUX')).toContainText('Ventes - code réutilisé');
});
