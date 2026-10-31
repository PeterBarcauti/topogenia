// playwright.config.ts
// Journeys of the browser (annex B 8.3, spec 11): the real host started by the starter of the tests, in desktop mode on SQLite or in server mode on SQL Server (E2E_MODE), the built interface, Chromium or Edge (E2E_BROWSER) at 1366x768; the measures of NF03 and NF04 apart (E2E_MEASURE).

import { defineConfig, devices } from '@playwright/test';
import { BASE_URL, BROWSER_CHANNEL, BROWSER_OPTIONS, HOST_PORT, MODE, RUN_FOLDER, SESSION_FILE, VIEWPORT } from './e2e/support/run.ts';

/**
 * E2E_MEASURE=1 plays the measures of NF03 and NF04 (`e2e/measures/*.measure.ts`, S-NF-04 to S-NF-06) instead of the
 * journeys: durations are no stable assertion under load (ADR 040), so they are played apart and their results written.
 */
const MEASURE = process.env.E2E_MEASURE === '1';

export default defineConfig({
  testDir: './e2e',
  testMatch: MEASURE ? 'measures/*.measure.ts' : '**/*.spec.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: MEASURE ? 600_000 : 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],
  globalSetup: './e2e/support/global-setup.ts',
  use: {
    baseURL: BASE_URL,
    locale: 'fr-FR',
    viewport: VIEWPORT,
    storageState: SESSION_FILE,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  // NF01: E2E_BROWSER=msedge plays the same journeys in the Edge of the machine.
  projects: [{ name: `${BROWSER_CHANNEL ?? 'chromium'}-${MODE}`, use: { ...devices['Desktop Chrome'], viewport: VIEWPORT, ...BROWSER_OPTIONS } }],
  webServer: {
    // The interface is built beforehand (`npm run e2e` builds it, B-0-08); the starter is built and run here.
    command: 'node e2e/support/start-host.mjs',
    url: `${BASE_URL}/api/health`,
    ignoreHTTPSErrors: true,
    timeout: 180_000,
    reuseExistingServer: false,
    stdout: 'pipe',
    env: { E2E_MODE: MODE, E2E_PORT: String(HOST_PORT), E2E_FOLDER: RUN_FOLDER, E2E_NO_BUILD: process.env.E2E_NO_BUILD ?? '' },
  },
});
