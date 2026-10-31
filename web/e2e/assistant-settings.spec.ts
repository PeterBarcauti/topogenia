// assistant-settings.spec.ts
// Journeys of the settings of the assistant (spec 3.0 sections 4, 6, 8), on the fake connectors of the starter of the host: the administrator chooses Claude by the Anthropic API, types a key, tests the connection and saves; the key is never read back, nor written in the audit; Bedrock asks for its region, a model refused says so.

import type { Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import { expect, test } from './support/journey.ts';

type Settings = components['schemas']['AssistantSettingsView'];
type AuditEntry = components['schemas']['AuditEntry'];

/** A fictitious key: the connectors of the starter answer without calling anyone. */
const KEY = 'sk-ant-fictive-journey-0000';

/** The dialog opened by the gear of the header, once the settings are read. */
async function openSettings(page: Page) {
  await page.goto('/');
  await page.getByTestId('header-assistant').click();
  const dialog = page.getByRole('dialog', { name: 'Paramètres de l’IA' });
  await expect(dialog.getByTestId('assistant-connector')).toBeVisible();
  return dialog;
}

test('the administrator chooses Claude by the Anthropic API, tests the connection and saves; the key is never read back nor audited (spec 3.0 section 4)', async ({ page, api }) => {
  const before = await api.get<Settings>('/api/admin/assistant');
  // The audit outlives the journeys: only the events of this one are read, those of the settings put back to none
  // before each journey left out (a journey before may have chosen a connector).
  const start = encodeURIComponent(new Date().toISOString());
  expect([before.connector, before.has_secret, before.connectors]).toEqual(['none', false, ['none', 'copy_paste', 'anthropic', 'bedrock']]);
  const dialog = await openSettings(page);
  await expect(dialog.getByTestId('assistant-connector-none')).toBeChecked();
  await dialog.getByTestId('assistant-connector-anthropic').check();
  await expect(dialog.getByLabel('Modèle')).toHaveValue('claude-opus-5-5');
  await dialog.getByLabel('Clé de l’API').fill(KEY);
  await dialog.getByRole('button', { name: 'Tester la connexion' }).click();
  await expect(dialog.getByTestId('assistant-tested')).toHaveText('Connexion réussie : claude-opus-5-5 a répondu en 1,3 s (21 jetons envoyés, 4 reçus).');

  // Sent with the version read, the If-Match the server mode demands (428 without it).
  const sent = page.waitForRequest((request) => request.method() === 'PUT' && new URL(request.url()).pathname === '/api/admin/assistant');
  await dialog.getByRole('button', { name: 'Enregistrer' }).click();
  expect(await (await sent).headerValue('if-match')).toBe(`"${String(before.version)}"`);
  await expect(page.getByText('Paramètres de l’IA enregistrés.')).toBeVisible();
  await expect(dialog).toHaveCount(0);

  const after = await api.get<Settings>('/api/admin/assistant');
  expect([after.connector, after.model, after.has_secret, after.secret_hint, after.version]).toEqual(['anthropic', 'claude-opus-5-5', true, '0000', before.version + 1]);
  expect(JSON.stringify(after)).not.toContain(KEY);

  // Opened again: the key kept is named by its last characters, its field empty.
  const again = await openSettings(page);
  await expect(again.getByLabel('Clé de l’API')).toHaveValue('');
  await expect(again.getByText('Clé enregistrée, finissant par 0000 : laisser vide pour la garder.')).toBeVisible();
  await expect(again.getByRole('button', { name: 'Enregistrer' })).toBeDisabled();
  expect(await page.content()).not.toContain(KEY);

  // The audit names the change by its fields and the test by its outcome, never the key.
  const audit = await api.get<AuditEntry[]>(`/api/admin/audit?kind=assistant.settings_changed&from=${start}`);
  expect(audit.map((entry) => entry.details)).toEqual(['{"connector":"anthropic","fields":["connector","model","secret"]}']);
  const tested = await api.get<AuditEntry[]>(`/api/admin/audit?kind=assistant.connection_tested&from=${start}`);
  expect(tested.length).toBeGreaterThan(0);
  expect(JSON.stringify([...audit, ...tested])).not.toContain(KEY);
});

test('Bedrock asks for its region before it is saved, and a model refused by the provider says so (spec 3.0 sections 4, 5)', async ({ page, api }) => {
  const dialog = await openSettings(page);
  await dialog.getByTestId('assistant-connector-bedrock').check();
  await expect(dialog.getByLabel('Modèle')).toHaveValue('global.anthropic.claude-sonnet-5-5');
  await expect(dialog.getByLabel('Clé de l’API')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Enregistrer' }).click();
  await expect(dialog.getByTestId('assistant-error')).toBeVisible();
  expect((await api.get<Settings>('/api/admin/assistant')).connector).toBe('none');

  await dialog.getByLabel('Région AWS').fill('eu-west-3');
  await dialog.getByLabel('Modèle').fill('refused-model');
  await dialog.getByRole('button', { name: 'Tester la connexion' }).click();
  await expect(dialog.getByTestId('assistant-test-failed')).toContainText('Le fournisseur refuse la clé ou l’identité.');
  await dialog.getByLabel('Modèle').fill('global.anthropic.claude-sonnet-5-5');
  await dialog.getByRole('button', { name: 'Enregistrer' }).click();
  await expect(dialog).toHaveCount(0);
  const saved = await api.get<Settings>('/api/admin/assistant');
  expect([saved.connector, saved.region, saved.aws_profile, saved.has_secret]).toEqual(['bedrock', 'eu-west-3', null, false]);
});
