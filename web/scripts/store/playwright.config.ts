// playwright.config.ts
// The captures of the listing of the Microsoft Store (ADR 088): the sample project of a first launch shown on the real host in desktop mode, under a fictitious account, in French then in English, at twice the density of the screen.

import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
import base from '../../playwright.config.ts';
import { MODE } from '../../e2e/support/run.ts';

/** The fictitious user of the captures, as in the demonstration: no real login nor name of machine on a capture. */
const DEMO_ACCOUNT = 'DEMO\\camille';

const web = resolve(import.meta.dirname, '..', '..');
const server = base.webServer;
if (server === undefined || Array.isArray(server)) throw new Error('The journeys start one host.');
if (MODE !== 'desktop') throw new Error('The captures of the Store show the desktop mode only (E2E_MODE unset).');
if (!process.env.STORE_CAPTURES) throw new Error('STORE_CAPTURES names the folder of the captures, outside the repository.');

export default defineConfig({
  ...base,
  testDir: '.',
  testMatch: 'captures.ts',
  outputDir: join(web, 'test-results', 'store'),
  timeout: 10 * 60_000,
  globalSetup: join(web, 'e2e', 'support', 'global-setup.ts'),
  // A screen of 1600 x 900 at twice its density: 3200 x 1800, within what the Store takes (1366 x 768 to 3840 x 2160).
  projects: (base.projects ?? []).map((project) => ({ ...project, use: { ...project.use, viewport: { width: 1600, height: 900 }, deviceScaleFactor: 2 } })),
  use: { ...base.use, actionTimeout: 15_000, trace: 'off' },
  webServer: { ...server, cwd: web, env: { ...server.env, E2E_ACCOUNT: DEMO_ACCOUNT } },
});
