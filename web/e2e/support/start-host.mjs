// start-host.mjs
// Starts the host of the journeys for Playwright: the folder of the run emptied, then the starter of the tests built and run on its port, in its mode, with the built interface, and its fictitious account when given.

import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';

const port = process.env.E2E_PORT;
const folder = process.env.E2E_FOLDER;
const mode = process.env.E2E_MODE ?? 'desktop';
if (!port || !folder) throw new Error('E2E_PORT and E2E_FOLDER are set by playwright.config.ts.');

const repository = resolve(import.meta.dirname, '..', '..', '..');
const webRoot = join(repository, 'src', 'Topogenia.Host', 'wwwroot');
if (!existsSync(join(webRoot, 'index.html'))) throw new Error('Build the interface first: npm run build (or npm run e2e).');

rmSync(folder, { recursive: true, force: true });
mkdirSync(folder, { recursive: true });

const project = join(repository, 'tests', 'Topogenia.EndToEnd.Host', 'Topogenia.EndToEnd.Host.csproj');
// E2E_NO_BUILD=1: the starter is already built (several runs side by side must not build the same project at once).
const build = process.env.E2E_NO_BUILD === '1' ? ['--no-build'] : [];
// E2E_ACCOUNT: a fictitious login for the user of the desktop mode (the demonstration, P6-T11), never the account of the machine.
const account = process.env.E2E_ACCOUNT ? ['--account', process.env.E2E_ACCOUNT] : [];
const host = spawn('dotnet', ['run', ...build, '--project', project, '--no-launch-profile', '--', '--folder', folder, '--port', port, '--webroot', webRoot, '--mode', mode, ...account], {
  stdio: 'inherit',
  env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => host.kill());
host.on('exit', (code) => process.exit(code ?? 0));
