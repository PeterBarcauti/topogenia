// vite-config.test.ts
// The settings of the build and of the development: unit tests under jsdom, the proxy of /api on a single connection, the version and the day of its release (B-0-05, B-0-09, B-0-10, ADR 111).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import config, { API_PROXY_AGENT, APP_RELEASE_DATE, APP_VERSION, releaseDayOf } from '../vite.config.ts';

const manifest = JSON.parse(readFileSync(join(import.meta.dirname, '../package.json'), 'utf8')) as { version: string; releaseDate: string };

describe('the settings of vite', () => {
  it('run the unit tests under vitest with jsdom (B-0-05)', () => {
    expect(config.test?.environment).toBe('jsdom');
    expect(config.test?.include).toEqual(['src/**/*.test.{ts,tsx}', 'scripts/**/*.test.ts']);
  });

  it('send /api to the port of the API through one connection kept alive, for the exchange of NTLM (B-0-09)', () => {
    const proxy = config.server?.proxy?.['/api'];
    expect(proxy).toMatchObject({ target: 'http://127.0.0.1:5080', agent: API_PROXY_AGENT });
    expect(API_PROXY_AGENT).toMatchObject({ maxSockets: 1, keepAlive: true });
  });

  it('write the version of package.json into the bundle under a neutral name (B-0-10)', () => {
    expect(APP_VERSION).toBe(manifest.version);
    expect(config.define?.__APP_VERSION__).toBe(JSON.stringify(manifest.version));
    expect(__APP_VERSION__).toBe(manifest.version);
  });

  it('write the day of the release of package.json, a day without hour (ADR 111)', () => {
    expect(APP_RELEASE_DATE).toBe(manifest.releaseDate);
    expect(APP_RELEASE_DATE).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    // Nothing else enters the bundle: the version and its day, both read from package.json, never from the clock.
    expect(config.define).toEqual({ __APP_VERSION__: JSON.stringify(manifest.version), __APP_RELEASE_DATE__: JSON.stringify(manifest.releaseDate) });
  });

  it('the day built into the bundle is the one vite.config.ts reads from package.json, whatever the clock says', () => {
    // Neither the calendar nor the global replaced by src/test/setup.ts: the function of vite.config.ts itself, a day later than today.
    vi.useFakeTimers();
    try {
      vi.setSystemTime(new Date('2099-01-02T10:00:00Z'));
      expect(config.define?.__APP_RELEASE_DATE__).toBe(JSON.stringify(releaseDayOf(manifest)));
      expect(APP_RELEASE_DATE).toBe(releaseDayOf(manifest));
    } finally {
      vi.useRealTimers();
    }
  });

  it('refuse a day of release that is not a day of the calendar, an hour or a zone with it, or none', () => {
    expect(releaseDayOf({ releaseDate: '2028-02-29' })).toBe('2028-02-29');
    const refused = [{ releaseDate: '2026-11-21T10:00:00Z' }, { releaseDate: '2026-11-21+01:00' }, { releaseDate: '2026-02-30' }, { releaseDate: '21/11/2026' }, { releaseDate: 20261121 }, {}];
    for (const written of refused) expect(() => releaseDayOf(written)).toThrow('releaseDate');
  });
});
