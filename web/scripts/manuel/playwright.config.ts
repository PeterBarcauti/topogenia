// playwright.config.ts
// The captures of the user manual: the real host in desktop mode, under a fictitious account, the sample project and a small project of purchases, in French, at one and a half times the density of the screen.

import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
import base from '../../playwright.config.ts';
import { MODE } from '../../e2e/support/run.ts';

/** The fictitious user of the captures, as in the demonstration: no real login nor name of machine on a capture. */
const DEMO_ACCOUNT = 'DEMO\\camille';

const web = resolve(import.meta.dirname, '..', '..');
const server = base.webServer;
if (server === undefined || Array.isArray(server)) throw new Error('The journeys start one host.');
if (MODE !== 'desktop') throw new Error('The captures of the manual show the desktop mode only (E2E_MODE unset).');
if (!process.env.MANUEL_CAPTURES) throw new Error('MANUEL_CAPTURES names the folder of the captures, outside the repository.');

export default defineConfig({
  ...base,
  testDir: '.',
  testMatch: 'captures.ts',
  outputDir: join(web, 'test-results', 'manuel'),
  timeout: 15 * 60_000,
  globalSetup: join(web, 'e2e', 'support', 'global-setup.ts'),
  projects: (base.projects ?? []).map((project) => ({ ...project, use: { ...project.use, viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1.5 } })),
  use: { ...base.use, actionTimeout: 10_000, trace: 'off' },
  webServer: { ...server, cwd: web, env: { ...server.env, E2E_ACCOUNT: DEMO_ACCOUNT } },
});
