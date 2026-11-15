// host.ts
// The host seen from the journeys: the launch address the starter writes, and a host of its own for the journeys of the launch token and of the command « Quit ».

import { spawn, type ChildProcess } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const LAUNCH_FILE_NAME = 'launch-address.txt';

/** The launch address once the starter has written it (the host listens then), within `timeoutMs`. */
export async function readLaunchAddress(file: string, timeoutMs = 120_000): Promise<string> {
  const end = Date.now() + timeoutMs;
  for (;;) {
    try {
      return readFileSync(file, 'utf8').trim();
    } catch (error) {
      if (Date.now() > end) throw new Error(`No launch address in ${file}.`, { cause: error });
      await delay(200);
    }
  }
}

/** A host started by a journey: its folder, its launch address, the end of its process, and its stop. */
export interface OwnHost {
  readonly folder: string;
  readonly launchAddress: string;
  /** Settled when the process of the starter ends, with its exit code: 0 when the host stopped by itself. */
  readonly exited: Promise<number | null>;
  stop(): Promise<void>;
}

const repository = resolve(import.meta.dirname, '..', '..', '..');

/** `dotnet run` starts the host as its own child: on Windows the whole tree is stopped, as Playwright stops its server. */
function stopped(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null || child.pid === undefined) return Promise.resolve();
  const pid = child.pid;
  return new Promise((done) => {
    child.once('exit', () => { done(); });
    if (process.platform === 'win32') spawn('taskkill', ['/pid', String(pid), '/T', '/F'], { stdio: 'ignore' });
    else child.kill();
  });
}

/**
 * Starts another run of the starter (already built by the set-up of Playwright) on `port`, in a new folder: a fresh
 * launch token that no browser has spent yet.
 */
export async function startOwnHost(port: number): Promise<OwnHost> {
  const folder = mkdtempSync(join(tmpdir(), 'e2e-own-host-'));
  const project = join(repository, 'tests', 'Topogenia.EndToEnd.Host', 'Topogenia.EndToEnd.Host.csproj');
  const webRoot = join(repository, 'src', 'Topogenia.Host', 'wwwroot');
  const child = spawn('dotnet', ['run', '--no-build', '--project', project, '--no-launch-profile', '--',
    '--folder', folder, '--port', String(port), '--webroot', webRoot], { stdio: 'ignore', env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' } });
  const exited = new Promise<number | null>((done) => {
    child.once('exit', (code) => { done(code); });
  });
  const launchAddress = await readLaunchAddress(join(folder, LAUNCH_FILE_NAME));
  return {
    folder,
    launchAddress,
    exited,
    stop: async () => {
      await stopped(child);
      for (let attempt = 0; attempt < 50; attempt++) {
        try {
          rmSync(folder, { recursive: true, force: true });
          return;
        } catch {
          await delay(100);
        }
      }
    },
  };
}
