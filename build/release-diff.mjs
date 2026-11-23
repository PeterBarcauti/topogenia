// release-diff.mjs
// Compares a package as audited with the same package rebuilt on the day of its publication (pre-audit 68, P2-02; audit 3.4, P2-01): once the day of the release and the names of the files renamed by their content are put back, every file must be the same, byte for byte; the files rebuilt at each build (SBOM, endpoints, block map) are checked against the package they describe.
// node build/release-diff.mjs <folder of the package audited> <folder of the package published> <day audited> <day published>   (each the ZIP or the MSIX unpacked; days written yyyy-mm-dd)

import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { brotliDecompressSync, gunzipSync } from 'node:zlib';

const fail = (message) => {
  console.error(message);
  process.exit(1);
};
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const [audited, published, dayAudited, dayPublished] = process.argv.slice(2);
if (process.argv.length !== 6 || !DAY.test(dayAudited) || !DAY.test(dayPublished)) {
  fail('node build/release-diff.mjs <folder of the package audited> <folder of the package published> <day audited yyyy-mm-dd> <day published yyyy-mm-dd>');
}
for (const folder of [audited, published]) if (!existsSync(folder) || !statSync(folder).isDirectory()) fail(`${folder}: not a folder`);

const sha = (bytes) => createHash('sha256').update(bytes).digest();
const hex = (bytes) => sha(bytes).toString('hex');
const base64 = (bytes) => sha(bytes).toString('base64');

/** Every file of a folder, by its path written with forward slashes. */
const filesOf = (folder) => readdirSync(folder, { recursive: true, withFileTypes: true }).filter((entry) => entry.isFile())
  .map((entry) => relative(folder, join(entry.parentPath, entry.name)).replaceAll('\\', '/')).sort();

/** The name of a built asset without the fingerprint Vite gives it (`index-BDfV0jEJ.js` -> `index.js`), compressed copies kept apart. */
const FINGERPRINT = /^(wwwroot\/assets\/.+)-[A-Za-z0-9_-]{8}(\.[a-z0-9]+(?:\.br|\.gz)?)$/;
const keyOf = (path) => path.replace(FINGERPRINT, '$1$2');
/** The bundle of the interface, the one file that carries the day of the release. */
const BUNDLE = 'wwwroot/assets/index.js';
const SBOM = 'sbom.cdx.json';
const ENDPOINTS = 'Topogenia.staticwebassets.endpoints.json';
const BLOCK_MAP = 'AppxBlockMap.xml';
/** Files written anew at each build, each checked against the package it describes rather than byte for byte. */
const REBUILT = new Set([SBOM, ENDPOINTS, BLOCK_MAP]);
const COMPRESSED = /\.(br|gz)$/;

const problems = [];

// 1. The files of both packages, paired: by their path when both have it, else (an asset renamed by its fingerprint) by
// their path without the fingerprint, which must then name one file on each side.
const pathsBefore = filesOf(audited);
const pathsAfter = filesOf(published);
const inAfter = new Set(pathsAfter);
const inBefore = new Set(pathsBefore);
const pairs = pathsBefore.filter((path) => inAfter.has(path)).map((path) => [path, path]);
const leftAfter = pathsAfter.filter((path) => !inBefore.has(path));
for (const path of pathsBefore.filter((name) => !inAfter.has(name))) {
  const candidates = leftAfter.filter((other) => keyOf(other) === keyOf(path) && keyOf(other) !== other);
  const rivals = pathsBefore.filter((other) => !inAfter.has(other) && keyOf(other) === keyOf(path));
  if (candidates.length === 1 && rivals.length === 1) pairs.push([path, candidates[0]]);
  else problems.push(`${path}: absent from the package published`);
}
const paired = new Set(pairs.map(([, after]) => after));
for (const path of leftAfter) if (!paired.has(path)) problems.push(`${path}: absent from the package audited`);

// 2. The names that changed: a file renamed by its content changes the files that name it, and only by that name.
const nameOf = (path) => path.split('/').pop();
const renamed = pairs.filter(([before, after]) => before !== after && !COMPRESSED.test(before))
  .map(([before, after]) => [nameOf(before), nameOf(after)]);
const putBack = (bytes, withDay) => {
  let text = bytes.toString('latin1');
  for (const [before, after] of renamed) text = text.replaceAll(before, after);
  if (withDay) text = text.replaceAll(dayAudited, dayPublished);
  return Buffer.from(text, 'latin1');
};

// 3. Every file the same, byte for byte, once put back; a compressed copy checked by what it decompresses to.
const inflate = (path, bytes) => (path.endsWith('.br') ? brotliDecompressSync(bytes) : gunzipSync(bytes));
let same = 0;
const changed = [];
for (const [before, after] of pairs) {
  const key = keyOf(before);
  if (REBUILT.has(key)) continue;
  const a = readFileSync(join(audited, before));
  const b = readFileSync(join(published, after));
  if (a.equals(b)) {
    same += 1;
    continue;
  }
  if (COMPRESSED.test(before)) continue; // checked below, against the file it is a copy of
  if (putBack(a, key === BUNDLE).equals(b)) changed.push(`${before} -> ${after}`);
  else problems.push(`${before}: differs by more than ${key === BUNDLE ? 'the day of the release and ' : ''}the names of the files renamed`);
}
for (const [folder, paths] of [[audited, pathsBefore], [published, pathsAfter]]) {
  for (const path of paths.filter((name) => COMPRESSED.test(name))) {
    const original = path.replace(COMPRESSED, '');
    let inflated;
    try {
      inflated = inflate(path, readFileSync(join(folder, path)));
    } catch {
      problems.push(`${folder}: ${path} does not decompress`);
      continue;
    }
    if (!existsSync(join(folder, original)) || !inflated.equals(readFileSync(join(folder, original)))) {
      problems.push(`${folder}: ${path} is not the compressed copy of ${original}`);
    }
  }
}

// 4. The SBOM: the same components; its date the day of each package.
const pairOf = (key) => pairs.find(([before]) => before === key);
if (pairOf(SBOM)) {
  const sboms = [JSON.parse(readFileSync(join(audited, SBOM), 'utf8')), JSON.parse(readFileSync(join(published, SBOM), 'utf8'))];
  [dayAudited, dayPublished].forEach((day, index) => {
    if (sboms[index].metadata?.timestamp !== `${day}T00:00:00Z`) problems.push(`${SBOM}: dated ${sboms[index].metadata?.timestamp}, not ${day}T00:00:00Z`);
  });
  const strip = (sbom) => { const copy = structuredClone(sbom); delete copy.serialNumber; delete copy.metadata?.timestamp; return JSON.stringify(copy); };
  if (strip(sboms[0]) !== strip(sboms[1])) problems.push(`${SBOM}: its components differ`);
}

// 5. The endpoints of the static assets: each one true of the file it serves in its package (length, ETag, integrity,
// Last-Modified the day of the package); then the same endpoints on both sides, once the names put back and the values
// just checked set aside.
const CHECKED = new Set(['Content-Length', 'ETag', 'Last-Modified', 'integrity', 'original-resource', 'fingerprint']);
const httpDay = (day) => new Date(`${day}T00:00:00Z`).toUTCString();
const endpointsOf = (folder, day) => {
  const text = readFileSync(join(folder, ENDPOINTS), 'utf8');
  const endpoints = JSON.parse(text).Endpoints;
  for (const endpoint of endpoints) {
    const where = `${folder}: ${ENDPOINTS}, ${endpoint.Route}`;
    const file = join(folder, 'wwwroot', endpoint.AssetFile);
    if (!existsSync(file)) {
      problems.push(`${where}: serves ${endpoint.AssetFile}, absent`);
      continue;
    }
    const bytes = readFileSync(file);
    const header = (name) => endpoint.ResponseHeaders.find((entry) => entry.Name === name)?.Value;
    const property = (name) => endpoint.EndpointProperties.find((entry) => entry.Name === name)?.Value;
    if (header('Content-Length') !== String(bytes.length)) problems.push(`${where}: Content-Length is not the size of ${endpoint.AssetFile}`);
    if (header('ETag') !== `"${base64(bytes)}"`) problems.push(`${where}: ETag is not the hash of ${endpoint.AssetFile}`);
    if (header('Last-Modified') !== httpDay(day)) problems.push(`${where}: Last-Modified ${header('Last-Modified')}, not ${httpDay(day)}`);
    // A compressed copy served for the route of its file carries the hash of that file; served at its own route, its own.
    const forItsFile = COMPRESSED.test(endpoint.AssetFile) && !COMPRESSED.test(endpoint.Route);
    const original = forItsFile ? readFileSync(file.replace(COMPRESSED, '')) : bytes;
    const integrity = property('integrity');
    if (integrity !== undefined && integrity !== `sha256-${base64(original)}`) problems.push(`${where}: integrity is not the hash of the file served`);
    const resource = property('original-resource');
    if (resource !== undefined && resource !== `"${base64(original)}"`) problems.push(`${where}: original-resource is not the hash of the file served`);
  }
  return endpoints;
};
if (pairOf(ENDPOINTS)) {
  const shape = (endpoints, rename) => endpoints.map((endpoint) => {
    const named = (value) => (rename ? putBack(Buffer.from(value, 'latin1'), false).toString('latin1') : value);
    // A route names its file, and the cached one adds the fingerprint of the SDK before the extension (`index-BDfV0jEJ.aet7su5wwp.js`).
    const SDK = /\.[a-z0-9]{10}(\.[a-z0-9]+(?:\.br|\.gz)?)$/;
    const route = `${named(endpoint.Route.replace(SDK, '$1'))}${SDK.test(endpoint.Route) ? ' (fingerprinted)' : ''}`;
    const kept = (list) => list.filter((entry) => !CHECKED.has(entry.Name)).map((entry) => `${entry.Name}=${named(entry.Value)}`).join(';');
    const selectors = endpoint.Selectors.map((entry) => `${entry.Name}=${entry.Value}`).join(';');
    return `${route} ${named(endpoint.AssetFile)} [${selectors}] [${kept(endpoint.ResponseHeaders)}] [${kept(endpoint.EndpointProperties)}]`;
  }).sort().join('\n');
  const before = shape(endpointsOf(audited, dayAudited), true);
  const after = shape(endpointsOf(published, dayPublished), false);
  if (before !== after) {
    const [one, two] = [before.split('\n'), after.split('\n')];
    const first = one.findIndex((line, index) => line !== two[index]);
    problems.push(`${ENDPOINTS}: its endpoints differ, first at\n  ${one[first]}\n  ${two[first]}`);
  }
}

// 6. The block map of the MSIX: every file of the package, its size, the hash of each block of 64 KiB and of the whole file.
const BLOCK = 65536;
const OUTSIDE_THE_MAP = new Set([BLOCK_MAP, '[Content_Types].xml', 'AppxSignature.p7x']);
if (pairOf(BLOCK_MAP)) {
  for (const [folder, paths] of [[audited, pathsBefore], [published, pathsAfter]]) {
    const xml = readFileSync(join(folder, BLOCK_MAP), 'utf8');
    const listed = new Set();
    for (const [, name, size, body] of xml.matchAll(/<File Name="([^"]+)" Size="(\d+)"[^>]*>(.*?)<\/File>/g)) {
      const path = name.replaceAll('\\', '/').replaceAll('&amp;', '&');
      listed.add(path);
      const where = `${folder}: ${BLOCK_MAP}, ${path}`;
      if (!existsSync(join(folder, path))) {
        problems.push(`${where}: absent from the package`);
        continue;
      }
      const bytes = readFileSync(join(folder, path));
      if (Number(size) !== bytes.length) problems.push(`${where}: size ${size}, not ${bytes.length}`);
      const blocks = [...body.matchAll(/<Block Hash="([^"]+)"/g)].map((match) => match[1]);
      const expected = [];
      for (let offset = 0; offset < bytes.length; offset += BLOCK) expected.push(base64(bytes.subarray(offset, offset + BLOCK)));
      if (blocks.join() !== expected.join()) problems.push(`${where}: the hashes of its blocks are not those of the file`);
      const whole = /<b4:FileHash Hash="([^"]+)"/.exec(body)?.[1];
      if (whole !== undefined && whole !== base64(bytes)) problems.push(`${where}: the hash of the file is not that of the file`);
    }
    for (const path of paths) if (!OUTSIDE_THE_MAP.has(path) && !listed.has(path)) problems.push(`${folder}: ${path} is not in ${BLOCK_MAP}`);
  }
}

console.log(`${same} files identical; ${changed.length} the same once the day and the names put back; `
  + `${pairs.filter(([path]) => COMPRESSED.test(path)).length} compressed copies checked against their file; `
  + `${pairs.filter(([path]) => REBUILT.has(path)).length} rebuilt (SBOM, endpoints, block map) checked against the package`);
for (const line of changed.sort()) console.log(`  ${line}`);
if (problems.length > 0) fail(`Not the package audited:\n${problems.join('\n')}`);
console.log(`The package published is the package audited, the day of the release aside (${dayAudited} -> ${dayPublished})`);
