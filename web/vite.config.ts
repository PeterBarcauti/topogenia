// vite.config.ts
// Build of the interface into the host's static files, its version and the day of its release, the proxy of development to the API, and the unit test settings (vitest, jsdom).

import { readFileSync, writeFileSync } from 'node:fs';
import { Agent } from 'node:http';
import { join } from 'node:path';
import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

/**
 * The package a module of the bundle comes from, by its path in the lock file (`node_modules/a/node_modules/@b/c`), and
 * the rest of its path in it; null for a module of the application.
 */
export function packageOf(id: string): { readonly path: string; readonly file: string } | null {
  const normal = id.replace(/\\/g, '/');
  const first = normal.indexOf('/node_modules/');
  if (first < 0) return null;
  const parts = normal.slice(first + 1).split('/');
  let end = 0;
  for (let index = 0; index < parts.length; index++) {
    if (parts[index] !== 'node_modules') continue;
    end = parts[index + 1]?.startsWith('@') === true ? index + 3 : index + 2;
  }
  return { path: parts.slice(0, end).join('/'), file: parts.slice(end).join('/') };
}

/**
 * P7-T03 (F-31, F-41): the packages whose code the built interface carries, by their path in the lock file, with the
 * files already bundled they are served from as they are (`*.min.js`, which carry other packages), written beside
 * package.json (`served-packages.json`) for `build/third-party-notices.mjs` and the SBOM.
 */
function servedPackages(): Plugin {
  return {
    name: 'served-packages',
    apply: 'build',
    generateBundle(_options, bundle) {
      const served = new Map<string, Set<string>>();
      for (const output of Object.values(bundle)) {
        if (output.type !== 'chunk') continue;
        for (const id of output.moduleIds) {
          const found = packageOf(id);
          if (found === null) continue;
          const bundles = served.get(found.path) ?? new Set<string>();
          if (/\.min\.js$/.test(found.file)) bundles.add(found.file);
          served.set(found.path, bundles);
        }
      }
      const list = [...served].sort(([a], [b]) => a.localeCompare(b)).map(([path, bundles]) => ({ path, bundled: [...bundles].sort() }));
      writeFileSync(join(import.meta.dirname, 'served-packages.json'), `${JSON.stringify(list, null, 2)}\n`);
    },
  };
}

/**
 * B-0-09: in server mode the host authenticates by Negotiate (NTLM), whose exchange must stay on one connection: the
 * proxy of development keeps a single connection alive.
 */
export const API_PROXY_AGENT = new Agent({ keepAlive: true, maxSockets: 1, keepAliveMsecs: 60_000 });

const MANIFEST = JSON.parse(readFileSync(join(import.meta.dirname, 'package.json'), 'utf8')) as { version: string; releaseDate?: unknown };

/** B-0-10: the version of the application, read from package.json and written into the bundle as `__APP_VERSION__`. */
export const APP_VERSION = MANIFEST.version;

/**
 * ADR 111: the day of the release, `releaseDate` of package.json, written by hand at the publication (`yyyy-mm-dd`, a day
 * of the calendar, no hour nor zone); anything else stops the build, which never takes the day from its clock.
 */
export function releaseDayOf(manifest: { readonly releaseDate?: unknown }): string {
  const day = manifest.releaseDate;
  const parts = typeof day === 'string' ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(day) : null;
  const [year = NaN, month = NaN, date = NaN] = (parts ?? []).slice(1).map(Number);
  const read = new Date(Date.UTC(year, month - 1, date));
  if (parts === null || read.getUTCFullYear() !== year || read.getUTCMonth() !== month - 1 || read.getUTCDate() !== date) {
    throw new Error(`The releaseDate of package.json is not a day written yyyy-mm-dd: ${String(day)}.`);
  }
  return day as string;
}

/** ADR 111: the day of the release, written into the bundle as `__APP_RELEASE_DATE__`. */
export const APP_RELEASE_DATE = releaseDayOf(MANIFEST);

export default defineConfig({
  plugins: [react(), servedPackages()],
  define: { __APP_VERSION__: JSON.stringify(APP_VERSION), __APP_RELEASE_DATE__: JSON.stringify(APP_RELEASE_DATE) },
  build: {
    outDir: '../src/Topogenia.Host/wwwroot',
    emptyOutDir: true,
    assetsInlineLimit: 0,
  },
  server: {
    proxy: { '/api': { target: 'http://127.0.0.1:5080', agent: API_PROXY_AGENT } },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}', 'scripts/**/*.test.ts'],
    // One worker a logical core starves the first render of React Flow in each file past the 5 s of a test (P6-T06); half of them run faster.
    maxWorkers: '50%',
    // Screens of 2 to 4 s alone passed 5 s whenever the computer did something else (F-34: 1, 13 then 9 timeouts in three
    // runs of the same code, every file green alone). No test measures its speed against this limit; a test that hangs still fails.
    testTimeout: 15_000,
  },
});
