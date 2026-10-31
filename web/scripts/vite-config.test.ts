// vite-config.test.ts
// The settings of the build and of the development: unit tests under jsdom, the proxy of /api on a single connection, the version (B-0-05, B-0-09, B-0-10).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import config, { API_PROXY_AGENT, APP_VERSION } from '../vite.config.ts';

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
    const { version } = JSON.parse(readFileSync(join(import.meta.dirname, '../package.json'), 'utf8')) as { version: string };
    expect(APP_VERSION).toBe(version);
    expect(config.define).toEqual({ __APP_VERSION__: JSON.stringify(version) });
    expect(__APP_VERSION__).toBe(version);
  });
});
