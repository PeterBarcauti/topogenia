// playwright.config.ts
// The walk of the demonstration (P6-T11, prompt 8): its scenario played on the real host in desktop mode, under a fictitious account, as the journeys are, outside of them.

import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
import base from '../../playwright.config.ts';
import { MODE } from '../../e2e/support/run.ts';

/** The fictitious user of the demonstration (prompt 4-5): no real login nor name of machine on a capture. */
const DEMO_ACCOUNT = 'DEMO\\camille';

const web = resolve(import.meta.dirname, '..', '..');
const server = base.webServer;
if (server === undefined || Array.isArray(server)) throw new Error('The journeys start one host.');
if (MODE !== 'desktop') throw new Error('The demonstration runs in desktop mode only (E2E_MODE unset).');

export default defineConfig({
  ...base,
  testDir: '.',
  testMatch: 'scenario.ts',
  outputDir: join(web, 'test-results', 'demonstration'),
  // One walk of some thirty screens, with its exports.
  timeout: 15 * 60_000,
  globalSetup: join(web, 'e2e', 'support', 'global-setup.ts'),
  // No trace: stopping one of a walk this long takes minutes; a failure leaves its screenshot.
  use: { ...base.use, acceptDownloads: true, actionTimeout: 15_000, trace: 'off' },
  webServer: { ...server, cwd: web, env: { ...server.env, E2E_ACCOUNT: DEMO_ACCOUNT } },
});
