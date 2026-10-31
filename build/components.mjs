// components.mjs
// The third-party components of the product as they ship: NuGet packages of the restore graphs, npm packages the built interface serves (web/served-packages.json) and those carried inside the files already bundled it serves, each with its licence.

import { existsSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = fileURLToPath(new URL('..', import.meta.url));
const nugetRoot = process.env.NUGET_PACKAGES ?? join(homedir(), '.nuget', 'packages');

/** Projects whose restore graph ships with the product, and test projects (never shipped). */
const SHIPPED = ['src/Topogenia.Host'];
// The tests of the core stay with it: the public repository has neither them nor the domain (ADR 074).
const CORE_TESTS = 'tests/Topogenia.Domain.Tests';
const TESTS = ['tests/Topogenia.Architecture.Tests', CORE_TESTS, 'tests/Topogenia.Application.Tests',
  'tests/Topogenia.Persistence.Tests', 'tests/Topogenia.Host.Tests', 'tests/Topogenia.Interchange.Tests', 'tests/Topogenia.EndToEnd.Host']
  .filter((project) => project !== CORE_TESTS || existsSync(join(root, 'src', 'Topogenia.Domain')));
/** The package of the core, restored by the public repository: a component of the product, not of a third party. */
const CORE_PACKAGE = 'Topogenia.Core';

/**
 * The licences of the packages carried inside a bundled file that are neither in the lock nor carried with their
 * package.json: recorded at the build from the licence each package declares (the machine of the build reads nothing on
 * the Internet), to confirm online at the recette (F-31).
 */
const RECORDED = {
  'asn1.js': 'MIT', 'bn.js': 'MIT', brorand: 'MIT', 'browser-pack': 'MIT', 'browserify-aes': 'MIT', 'browserify-cipher': 'MIT',
  'browserify-des': 'MIT', 'browserify-rsa': 'MIT', 'browserify-sign': 'ISC', 'buffer-xor': 'MIT', 'cipher-base': 'MIT',
  'create-ecdh': 'MIT', 'create-hash': 'MIT', 'create-hmac': 'MIT', 'crypto-browserify': 'MIT', 'des.js': 'MIT',
  'diffie-hellman': 'MIT', events: 'MIT', evp_bytestokey: 'MIT', 'hash-base': 'MIT', 'hash.js': 'MIT', 'hmac-drbg': 'MIT',
  'md5.js': 'MIT', 'miller-rabin': 'MIT', 'minimalistic-assert': 'ISC', 'minimalistic-crypto-utils': 'MIT', 'parse-asn1': 'ISC',
  pbkdf2: 'MIT', process: 'MIT', 'public-encrypt': 'MIT', randombytes: 'MIT', randomfill: 'MIT', ripemd160: 'MIT',
  'safer-buffer': 'MIT', 'sha.js': '(MIT AND BSD-3-Clause)', 'stream-browserify': 'MIT', 'timers-browserify': 'MIT', util: 'MIT',
};

/**
 * Parts of a carried package under another permissive licence than its own, read in its code (F-41, R4-m3): the
 * attribution names both.
 */
const PARTS = { 'hash.js': 'Apache-2.0', uuid: 'BSD-3-Clause' };

export function nugetLicence(id, version) {
  const nuspec = join(nugetRoot, id.toLowerCase(), version, `${id.toLowerCase()}.nuspec`);
  if (!existsSync(nuspec)) return 'unknown';
  const text = readFileSync(nuspec, 'utf8');
  const expression = /<license type="expression">([^<]+)<\/license>/.exec(text)?.[1];
  if (expression) return expression;
  const file = /<license type="file">([^<]+)<\/license>/.exec(text)?.[1];
  if (file) return `file: ${file}`;
  const url = /<licenseUrl>([^<]+)<\/licenseUrl>/.exec(text)?.[1];
  return url ? `url: ${url}` : 'unknown';
}

function nugetPackages(project) {
  const assets = join(root, project, 'obj', 'project.assets.json');
  if (!existsSync(assets)) throw new Error(`Restore ${project} first (missing ${assets})`);
  return Object.entries(JSON.parse(readFileSync(assets, 'utf8')).libraries)
    .filter(([key, value]) => value.type === 'package' && key.split('/')[0] !== CORE_PACKAGE)
    .map(([key]) => key.split('/'));
}

const nameOf = (path) => path.replace(/^.*node_modules\//, '');

/** The packages carried in the source map of a bundled file: their names, and the package.json some of them carry. */
function carriedBy(mapFile) {
  const map = JSON.parse(readFileSync(mapFile, 'utf8'));
  const carried = new Map();
  map.sources.forEach((source, index) => {
    const at = source.lastIndexOf('node_modules/');
    if (at < 0) return;
    const [scope, rest] = source.slice(at + 'node_modules/'.length).split('/');
    const id = scope.startsWith('@') ? `${scope}/${rest}` : scope;
    // browserify carries a package.json as a module: `module.exports={...}`.
    const licence = source.endsWith(`node_modules/${id}/package.json`)
      ? JSON.parse(map.sourcesContent[index].replace(/^module\.exports\s*=\s*/, '').replace(/;?\s*$/, '')).license : undefined;
    const bundled = /\.min\.js$/.test(source) ? source.slice(source.lastIndexOf(`node_modules/${id}/`)) : null;
    const known = carried.get(id) ?? { licence: undefined, bundled: false };
    carried.set(id, { licence: licence ?? known.licence, bundled: known.bundled || bundled !== null });
  });
  return carried;
}

/** Every component, by ecosystem: `{ nuget, npm, carried }`, each with its id, version, licence and usage. */
export function components() {
  const nuget = new Map();
  for (const [list, usage] of [[SHIPPED, 'livré'], [TESTS, 'tests']]) {
    for (const project of list) {
      for (const [id, version] of nugetPackages(project)) {
        const key = `${id}@${version}`;
        if (!nuget.has(key) || usage === 'livré') nuget.set(key, { id, version, licence: nugetLicence(id, version), usage });
      }
    }
  }

  const lock = JSON.parse(readFileSync(join(root, 'web', 'package-lock.json'), 'utf8'));
  const servedFile = join(root, 'web', 'served-packages.json');
  if (!existsSync(servedFile)) throw new Error('Build the interface first (npm run build writes web/served-packages.json)');
  const served = JSON.parse(readFileSync(servedFile, 'utf8'));
  const servedPaths = new Set(served.map((item) => item.path));
  const npm = Object.entries(lock.packages)
    .filter(([path]) => path.startsWith('node_modules/'))
    .map(([path, value]) => ({
      id: nameOf(path), version: value.version, licence: value.license ?? 'unknown', path,
      usage: value.dev || value.devOptional ? 'développement' : servedPaths.has(path) ? 'livré' : 'non servi',
    }));

  // The lock, by name: a licence for a carried package of the same name, and the dependencies of a carried bundle.
  const byName = new Map();
  for (const [path, value] of Object.entries(lock.packages)) {
    if (path !== '' && !byName.has(nameOf(path))) byName.set(nameOf(path), { path, value });
  }
  const closure = (name, seen = new Set()) => {
    const entry = byName.get(name);
    if (entry === undefined || seen.has(name)) return seen;
    seen.add(name);
    for (const dependency of Object.keys(entry.value.dependencies ?? {})) closure(dependency, seen);
    return seen;
  };

  const carried = new Map();
  const carry = (id, host, licence, through) => {
    if (carried.has(id) || id === host.id) return;
    const known = byName.get(id)?.value.license ?? licence ?? RECORDED[id];
    if (known === undefined) throw new Error(`No licence for ${id}, carried in ${host.file}: record it in RECORDED`);
    const recorded = byName.get(id)?.value.license === undefined && licence === undefined;
    const part = PARTS[id];
    carried.set(id, {
      id, licence: part ? `(${known} AND ${part})` : known, usage: 'livré', host: host.id,
      version: `dans ${host.id} ${host.version}${through ? `, par ${through}` : ''}${recorded ? ', licence relevée' : ''}`,
    });
  };
  for (const item of served) {
    for (const file of item.bundled) {
      const host = { id: nameOf(item.path), version: lock.packages[item.path].version, file };
      const mapFile = join(root, 'web', item.path, `${file}.map`);
      // The packages of the interface installed (npm ci): without them, what a bundle carries is unknown, never left out.
      if (!existsSync(mapFile)) throw new Error(`${mapFile} is missing: install the packages of the interface (npm ci in web)`);
      const inside = carriedBy(mapFile);
      for (const [id, { licence }] of inside) carry(id, host, licence, null);
      // A bundle inside the bundle, without a map of its own (jszip.min.js): it carries its dependencies of the lock.
      for (const [id, { bundled }] of inside) {
        if (!bundled || id === host.id) continue;
        for (const dependency of closure(id)) carry(dependency, host, undefined, id);
      }
    }
  }
  return { nuget: [...nuget.values()], npm, carried: [...carried.values()] };
}
