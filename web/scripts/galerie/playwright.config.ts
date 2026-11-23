// playwright.config.ts
// The journey of the gallery of templates: the real host in desktop mode, under a fictitious account, on a base of its own, every published template opened for good and its image taken, at one and a half times the density of the screen.

import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
import base from '../../playwright.config.ts';
import { MODE } from '../../e2e/support/run.ts';

/** The fictitious user of the images: no real login nor name of machine on an image. */
const DEMO_ACCOUNT = 'DEMO\\camille';

const web = resolve(import.meta.dirname, '..', '..');
const server = base.webServer;
if (server === undefined || Array.isArray(server)) throw new Error('The journeys start one host.');
if (MODE !== 'desktop') throw new Error('The images of the gallery are taken in the desktop mode only (E2E_MODE unset).');
if (!process.env.GALERIE_SITE) throw new Error('GALERIE_SITE names the folder of the site, which is only read.');
if (!process.env.GALERIE_IMAGES) throw new Error('GALERIE_IMAGES names the folder of the images, outside the repository.');

export default defineConfig({
  ...base,
  testDir: '.',
  testMatch: 'galerie.ts',
  outputDir: join(web, 'test-results', 'galerie'),
  timeout: 12 * 60_000,
  globalSetup: join(web, 'e2e', 'support', 'global-setup.ts'),
  projects: (base.projects ?? []).map((project) => ({ ...project, use: { ...project.use, viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1.5 } })),
  use: { ...base.use, actionTimeout: 10_000, trace: 'off' },
  webServer: { ...server, cwd: web, env: { ...server.env, E2E_ACCOUNT: DEMO_ACCOUNT } },
});
