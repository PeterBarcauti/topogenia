// sbom.mjs
// Writes the SBOM (CycloneDX 1.5, JSON) of a published mode from its deps.json and the components of build/components.mjs; arguments: <Desktop|Server|Store> <deps.json> <runtimeconfig.json or -> <out.cdx.json> (spec 5.4, ADR 073).

import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { releaseDay } from './archive-dates.mjs';
import { components, nugetLicence, root } from './components.mjs';

const [mode, depsFile, runtimeConfig, out] = process.argv.slice(2);
if (!['Desktop', 'Server', 'Store'].includes(mode) || !depsFile || !runtimeConfig || !out) {
  console.error('arguments: <Desktop|Server|Store> <deps.json> <runtimeconfig.json or -> <out.cdx.json>');
  process.exit(2);
}

/** A licence of CycloneDX: an SPDX expression, an SPDX identifier, or a name (a file or an address of a package). */
function licences(text) {
  if (/ (AND|OR) |^\(/.test(text)) return [{ expression: text }];
  if (/^[A-Za-z0-9.+-]+$/.test(text) && text !== 'unknown') return [{ license: { id: text } }];
  return [{ license: { name: text } }];
}

const product = /Name = "([^"]+)"/.exec(readFileSync(join(root, 'src', 'Topogenia.Application', 'ProductIdentity.cs'), 'utf8'))[1];
const version = /<Version>([^<]+)<\/Version>/.exec(readFileSync(join(root, 'Directory.Build.props'), 'utf8'))[1];
const deps = JSON.parse(readFileSync(depsFile, 'utf8'));
const { nuget, npm, carried } = components();

// .NET: what the publication carries, by its deps.json (packages, and the runtime packs of the desktop mode).
const dotnet = Object.entries(deps.libraries)
  .filter(([, library]) => library.type === 'package' || library.type === 'runtimepack')
  .map(([key, library]) => {
    const [rawName, packageVersion] = key.split('/');
    const name = rawName.replace(/^runtimepack\./, '');
    return {
      type: library.type === 'runtimepack' ? 'framework' : 'library', 'bom-ref': `pkg:nuget/${name}@${packageVersion}`, name, version: packageVersion,
      purl: `pkg:nuget/${name}@${packageVersion}`, licenses: licences(nugetLicence(name, packageVersion)),
    };
  });
// The server mode runs on the frameworks of the Hosting Bundle, installed on the server: named, not carried.
const provided = runtimeConfig === '-' || !existsSync(runtimeConfig) ? [] : (JSON.parse(readFileSync(runtimeConfig, 'utf8')).runtimeOptions.frameworks ?? [])
  .map((framework) => ({
    type: 'framework', 'bom-ref': `framework:${framework.name}`, name: framework.name, version: `${framework.version} or later`, scope: 'excluded',
    properties: [{ name: 'provided-by', value: 'Hosting Bundle .NET 10, installed on the server' }],
  }));

// A package the notices say shipped that the deps.json does not name: its assembly is carried by a shared framework of
// .NET (System.Security.Cryptography.Pkcs in ASP.NET Core), the runtime pack of the desktop or the Hosting Bundle of the
// server, which supersedes the package; named, at its version, so that a scanner reads it (pre-audit 68, P2-06).
const inDeps = new Set(dotnet.map((component) => component.name));
const superseded = nuget.filter((item) => item.usage === 'livré' && !inDeps.has(item.id)).map((item) => ({
  type: 'library', 'bom-ref': `pkg:nuget/${item.id}@${item.version}`, name: item.id, version: item.version,
  purl: `pkg:nuget/${item.id}@${item.version}`, licenses: licences(item.licence),
  properties: [{ name: 'delivered-by', value: 'a shared framework of .NET (runtime pack of the desktop, Hosting Bundle of the server)' }],
}));

// The interface: the npm packages served, then the packages carried inside their bundled files, at the top as well, at
// the version their embedded package.json gives when the bundle carries one (elliptic), without a version otherwise.
const interfaceComponents = npm.filter((item) => item.usage === 'livré').map((item) => ({
  type: 'library', 'bom-ref': `pkg:npm/${item.id}@${item.version}`, name: item.id, version: item.version,
  purl: `pkg:npm/${item.id}@${item.version}`, licenses: licences(item.licence),
}));
// A package served and carried as well (core-js, pako) is named twice: the copy in the bundle may be of another version.
const carriedComponents = carried.map((item) => {
  const at = item.carriedVersion ? `@${item.carriedVersion}` : '';
  return {
    type: 'library', 'bom-ref': `pkg:npm/${item.id}${at}#in-${item.host}`, name: item.id, ...(item.carriedVersion ? { version: item.carriedVersion } : {}),
    purl: `pkg:npm/${item.id}${at}`, licenses: licences(item.licence), properties: [{ name: 'carried', value: item.version }],
  };
});

/** What the package of each mode is (ADR 088: the desktop mode also as an MSIX package for the Microsoft Store). */
const DESCRIPTIONS = {
  Desktop: 'Desktop mode, portable ZIP (self-contained, win-x64)',
  Server: 'Server mode, ZIP for IIS (Hosting Bundle .NET 10)',
  Store: 'Desktop mode, MSIX package for the Microsoft Store (self-contained, win-x64)',
};

const bom = {
  bomFormat: 'CycloneDX', specVersion: '1.5', serialNumber: `urn:uuid:${randomUUID()}`, version: 1,
  metadata: {
    // The release day at midnight UTC, the date of every entry of the package: never the hour of the build (pre-audit 68, P2-04).
    timestamp: releaseDay().toISOString().replace('.000Z', 'Z'),
    component: {
      type: 'application', 'bom-ref': `${product}@${version}`, name: product, version,
      description: DESCRIPTIONS[mode],
      licenses: [{ license: { id: 'Apache-2.0' } }, { license: { name: 'Proprietary licence of the core (licenses/CORE-LICENSE.md)' } }],
    },
  },
  components: [...dotnet, ...superseded, ...provided, ...interfaceComponents, ...carriedComponents],
};
// Every component THIRD-PARTY-NOTICES.md says shipped is a component of the SBOM, at the top, the one a scanner of
// vulnerabilities reads (pre-audit 68, P2-06): a package of the notices left out fails the SBOM.
const named = new Set(bom.components.map((component) => component.name));
const left = [...nuget, ...npm, ...carried].filter((item) => item.usage === 'livré' && !named.has(item.id)).map((item) => `${item.id} ${item.version}`);
if (left.length > 0) {
  console.error(`The SBOM leaves out ${left.length} components shipped: ${left.join(', ')}`);
  process.exit(1);
}
writeFileSync(out, `${JSON.stringify(bom, null, 2)}\n`);
console.log(`${out}: ${dotnet.length + superseded.length} .NET, ${provided.length} provided, ${interfaceComponents.length} npm and ${carriedComponents.length} carried inside them`);
