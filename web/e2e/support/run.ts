// run.ts
// The run of the journeys: its mode, the port and the folder of the host, its address, the file of the launch address and the session kept by the set-up.

import { tmpdir } from 'node:os';
import { join } from 'node:path';

/**
 * The mode of the host (spec 11): `desktop` by default (SQLite, launch token), or `server` with `E2E_MODE=server`
 * (SQL Server, Windows authentication over HTTPS on `localhost`).
 */
export const MODE: 'desktop' | 'server' = process.env.E2E_MODE === 'server' ? 'server' : 'desktop';

/** The port of the host of the journeys (the 1.x used 5099). */
export const HOST_PORT = Number(process.env.E2E_PORT ?? '5198');

/**
 * The port of the second host that a journey starts for itself (the launch token, S-11-05): derived from the port of the
 * run, so that runs side by side never share it.
 */
export const OWN_HOST_PORT = HOST_PORT + 1000;

/**
 * The port of the host that the journey of the command « Quit » starts and stops (S-5.2-16): neither the one of the run
 * nor the one of the launch token, so that it never stops a host another journey uses.
 */
export const QUIT_HOST_PORT = HOST_PORT + 2000;

/** The folder of the product for the run: SQLite file, backups, logs; emptied at each start of the host. */
export const RUN_FOLDER = process.env.E2E_FOLDER ?? join(tmpdir(), `e2e-host-${MODE}-${HOST_PORT}`);

/**
 * The address of the application: in desktop mode the only authority it accepts (`Host` exactly `127.0.0.1:port`,
 * spec 5.2); in server mode `localhost`, an allowed host, over HTTPS (the server mode refuses HTTP, ADR 045).
 */
export const BASE_URL = MODE === 'server' ? `https://localhost:${HOST_PORT}` : `http://127.0.0.1:${HOST_PORT}`;

/** Written by the starter once the host listens (`tests/Topogenia.EndToEnd.Host`). */
export const LAUNCH_FILE = join(RUN_FOLDER, 'launch-address.txt');

/** The cookies of the browser once the launch token is exchanged, given to every journey (none in server mode). */
export const SESSION_FILE = join(RUN_FOLDER, 'browser-session.json');

/** The reference viewport of the journeys (annex B 0, B-0-07). */
export const VIEWPORT = { width: 1366, height: 768 } as const;

/**
 * The browser of the journeys (NF01): the Chromium of Playwright by default, or with `E2E_BROWSER=msedge` the Edge
 * installed on the machine (its channel; nothing is downloaded nor installed). Edge is Chromium: the same options hold.
 */
export const BROWSER_CHANNEL: 'msedge' | undefined = process.env.E2E_BROWSER === 'msedge' ? 'msedge' : undefined;

/**
 * Chromium sends the Windows identity only to the servers it is told to (as the 1.x did); the certificate of the server
 * mode is made for the run, never installed, so its errors are ignored by the browser of the journeys alone.
 */
export const BROWSER_OPTIONS = {
  ...(BROWSER_CHANNEL === undefined ? {} : { channel: BROWSER_CHANNEL }),
  ...(MODE === 'server' ? { ignoreHTTPSErrors: true, launchOptions: { args: ['--auth-server-allowlist=localhost'] } } : {}),
};
