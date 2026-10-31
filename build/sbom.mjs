// sbom.mjs
// Writes the SBOM (CycloneDX 1.5, JSON) of a published mode from its deps.json and the components of build/components.mjs; arguments: <Desktop|Server> <deps.json> <runtimeconfig.json or -> <out.cdx.json> (spec 5.4, ADR 073).

import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { components, nugetLicence, root } from './components.mjs';

const [mode, depsFile, runtimeConfig, out] = process.argv.slice(2);
if (!['Desktop', 'Server'].includes(mode) || !depsFile || !runtimeConfig || !out) {
  console.error('arguments: <Desktop|Server> <deps.json> <runtimeconfig.json or -> <out.cdx.json>');
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
const { npm, carried } = components();

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

// The interface: the npm packages served, each with the packages carried inside its bundled files.
const inside = new Map();
for (const item of carried) inside.set(item.host, [...(inside.get(item.host) ?? []), item]);
const interfaceComponents = npm.filter((item) => item.usage === 'livré').map((item) => ({
  type: 'library', 'bom-ref': `pkg:npm/${item.id}@${item.version}`, name: item.id, version: item.version,
  purl: `pkg:npm/${item.id}@${item.version}`, licenses: licences(item.licence),
  ...(inside.has(item.id) && item.path === `node_modules/${item.id}` ? {
    components: inside.get(item.id).map((part) => ({
      type: 'library', 'bom-ref': `pkg:npm/${part.id}#in-${item.id}`, name: part.id, purl: `pkg:npm/${part.id}`, licenses: licences(part.licence),
      properties: [{ name: 'carried', value: part.version }],
    })),
  } : {}),
}));

const bom = {
  bomFormat: 'CycloneDX', specVersion: '1.5', serialNumber: `urn:uuid:${randomUUID()}`, version: 1,
  metadata: {
    timestamp: new Date().toISOString(),
    component: {
      type: 'application', 'bom-ref': `${product}@${version}`, name: product, version,
      description: mode === 'Desktop' ? 'Desktop mode, portable ZIP (self-contained, win-x64)' : 'Server mode, ZIP for IIS (Hosting Bundle .NET 10)',
      licenses: [{ license: { id: 'Apache-2.0' } }, { license: { name: 'Proprietary licence of the core (licenses/CORE-LICENSE.md)' } }],
    },
  },
  components: [...dotnet, ...provided, ...interfaceComponents],
};
writeFileSync(out, `${JSON.stringify(bom, null, 2)}\n`);
console.log(`${out}: ${dotnet.length} .NET, ${provided.length} provided, ${interfaceComponents.length} npm (${carried.length} carried inside)`);
