// codeql-local.mjs
// Runs CodeQL on the C# and the TypeScript of the repository when its CLI is already there, the SARIF reports in TestResults/codeql/; without it, says how to get it and stops, installing nothing (ADR 049, ADR 073, ADR 076).
// node build/codeql-local.mjs

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import { delimiter, join } from 'node:path';
import { root } from './components.mjs';

// The CLI found in the PATH of the session, started directly, never through a shell (ADR 076).
const codeql = (process.env.PATH ?? '').split(delimiter).filter(Boolean).map((folder) => join(folder, 'codeql.exe')).find(existsSync);
if (!codeql) {
  console.log('The CodeQL CLI is not on this machine: nothing was analysed, nothing was installed.');
  console.log('To run this analysis: take the CodeQL bundle (CLI and its query packs) from the releases of github/codeql-action,');
  console.log('unzip it in a folder of your own, put that folder in the PATH of the session, then run this script again.');
  process.exit(3);
}

const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' };
function codeqlRun(what, args) {
  const result = spawnSync(codeql, args, { cwd: root, env, stdio: 'inherit', shell: false, windowsHide: true });
  if (result.error || result.status !== 0) {
    console.error(`codeql ${what} failed (${result.error?.message ?? result.status})`);
    process.exit(1);
  }
}

const out = join(root, 'TestResults', 'codeql');
mkdirSync(out, { recursive: true });
const solution = join(root, 'Topogenia.sln');

// C#: the database is made while the solution builds; TypeScript: from the sources of the interface, without a build.
const csharp = join(out, 'db-csharp');
codeqlRun('database create (csharp)', ['database', 'create', csharp, '--overwrite', '--language=csharp', '--source-root', root,
  '--command', `dotnet build "${solution}" -c Release`]);
const typescript = join(out, 'db-typescript');
codeqlRun('database create (typescript)', ['database', 'create', typescript, '--overwrite', '--language=javascript-typescript',
  '--source-root', join(root, 'web', 'src')]);

// The suites of security and quality of the bundle.
codeqlRun('database analyze (csharp)', ['database', 'analyze', csharp, 'codeql/csharp-queries:codeql-suites/csharp-security-and-quality.qls',
  '--format=sarif-latest', '--output', join(out, 'csharp.sarif')]);
codeqlRun('database analyze (typescript)', ['database', 'analyze', typescript, 'codeql/javascript-queries:codeql-suites/javascript-security-and-quality.qls',
  '--format=sarif-latest', '--output', join(out, 'typescript.sarif')]);
console.log(`SARIF reports: ${out}`);
