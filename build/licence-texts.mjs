// licence-texts.mjs
// The texts of the licences of the components that ship (pre-audit 68, P2-05): the licence files their packages carry, else the text of the licence they declare with the notice their metadata give; and the notices of the packages of Microsoft a publication carries, copied beside them.

import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { root } from './components.mjs';

const nugetRoot = process.env.NUGET_PACKAGES ?? join(homedir(), '.nuget', 'packages');
/** The files of a package that hold its licence (LICENSE, LICENSE.md, LICENSE-MIT, licence.txt, COPYING) or its notice (NOTICE). */
const LICENCE_FILE = /^(licen[cs]e|copying|notice)([-._][\w.-]*)?$/i;
/** The texts of the licences declared without a file: build/licences, and the Apache text of the product itself. */
const TEMPLATES = { MIT: 'MIT.txt', ISC: 'ISC.txt', 'BSD-3-Clause': 'BSD-3-Clause.txt' };
/** A line that names the holders of a work in a source, not the clause that speaks of the notice. */
const HOLDER_LINE = /\bcopyright\b|\(c\)\s*\d{4}|©/i;
const CLAUSE_LINE = /notice|holders|copyright\s*$|copyright\s*\(c\)\s*<|^\W*copyright\W*$/i;
const NOT_KNOWN = 'les auteurs du paquet ; avis à relever en ligne à la recette (F-31)';

/** A text as written in the file: its line ends in LF, no space at the end of a line, nor empty lines around it. */
const clean = (text) => text.replace(/^﻿/, '').replace(/\r\n?/g, '\n').split('\n').map((line) => line.trimEnd()).join('\n').trim();

function template(id) {
  if (id === 'Apache-2.0') return clean(readFileSync(join(root, 'LICENSE'), 'utf8'));
  if (!TEMPLATES[id]) throw new Error(`No text for the licence ${id}: write build/licences/${id}.txt`);
  return clean(readFileSync(join(root, 'build', 'licences', TEMPLATES[id]), 'utf8'));
}

/** The licences an expression asks for: every part of an AND; the first choice of an OR (the permissive one, ThirdPartyNoticesTests). */
function partsOf(expression) {
  let text = expression.trim();
  while (text.startsWith('(') && text.endsWith(')')) text = text.slice(1, -1).trim();
  if (text.includes(' OR ')) return partsOf(text.split(' OR ')[0]);
  return text.split(' AND ').map((part) => part.trim());
}

/** The licence files of a folder, in the order of their names, empty ones left out. */
function filesIn(folder) {
  if (!existsSync(folder)) return [];
  return readdirSync(folder, { withFileTypes: true }).filter((entry) => entry.isFile() && LICENCE_FILE.test(entry.name))
    .map((entry) => entry.name).sort().map((name) => clean(readFileSync(join(folder, name), 'utf8'))).filter((text) => text !== '');
}

/** The holders a nuspec names: its copyright, else its authors. */
function nuspecNotice(folder, id) {
  const nuspec = join(folder, `${id.toLowerCase()}.nuspec`);
  if (!existsSync(nuspec)) return null;
  const text = readFileSync(nuspec, 'utf8');
  const copyright = /<copyright>([^<]+)<\/copyright>/.exec(text)?.[1]?.trim();
  if (copyright) return copyright;
  const authors = /<authors>([^<]+)<\/authors>/.exec(text)?.[1]?.trim();
  return authors ? `© ${authors} (auteurs déclarés du paquet)` : null;
}

/** What the source maps of the bundles served tell of the holders of each package they carry: authors and copyright lines. */
function holdersInBundles() {
  const served = JSON.parse(readFileSync(join(root, 'web', 'served-packages.json'), 'utf8'));
  const holders = new Map();
  for (const item of served) {
    for (const file of item.bundled) {
      const map = JSON.parse(readFileSync(join(root, 'web', item.path, `${file}.map`), 'utf8'));
      map.sources.forEach((source, index) => {
        const at = source.lastIndexOf('node_modules/');
        if (at < 0) return;
        const [scope, rest] = source.slice(at + 'node_modules/'.length).split('/');
        const id = scope.startsWith('@') ? `${scope}/${rest}` : scope;
        const lines = holders.get(id) ?? new Set();
        const content = map.sourcesContent?.[index] ?? '';
        if (source.endsWith(`node_modules/${id}/package.json`)) {
          const json = JSON.parse(content.replace(/^module\.exports\s*=\s*/, '').replace(/;?\s*$/, ''));
          const author = typeof json.author === 'string' ? json.author : json.author?.name;
          if (author) lines.add(`© ${author.replace(/\s*<[^>]*>/g, '').trim()} (auteur déclaré du paquet)`);
        }
        for (const line of content.split('\n')) {
          const text = line.replace(/^[\s/*#-]+/, '').trim();
          if (text.length < 160 && HOLDER_LINE.test(text) && !CLAUSE_LINE.test(text)) lines.add(text.replace(/^copyright:\s*'(.*)',?$/i, '$1'));
        }
        holders.set(id, lines);
      });
    }
  }
  return holders;
}

/** The entry of a component: the texts its package carries, else the texts of its licences and the notice found for it. */
function entry(component, carried, notice) {
  if (carried.length > 0) return { ...component, texts: carried, notice: null };
  return { ...component, texts: partsOf(component.licence).map(template), notice: notice ?? NOT_KNOWN };
}

/** Every component that ships with the texts of its licence, by ecosystem then name. */
export function shippedTexts({ nuget, npm, carried }) {
  const lock = JSON.parse(readFileSync(join(root, 'web', 'package-lock.json'), 'utf8')).packages;
  const holders = holdersInBundles();
  const entries = [];
  for (const item of nuget.filter((p) => p.usage === 'livré')) {
    const folder = join(nugetRoot, item.id.toLowerCase(), item.version);
    entries.push(entry(item, filesIn(folder), nuspecNotice(folder, item.id)));
  }
  for (const item of npm.filter((p) => p.usage === 'livré')) {
    const manifest = JSON.parse(readFileSync(join(root, 'web', item.path, 'package.json'), 'utf8'));
    const author = typeof manifest.author === 'string' ? manifest.author : manifest.author?.name;
    entries.push(entry(item, filesIn(join(root, 'web', item.path)), author ? `© ${author.replace(/\s*<[^>]*>/g, '').trim()} (auteur déclaré du paquet)` : null));
  }
  for (const item of carried) {
    // The texts of a copy of the same name, when it declares the same licence: the version carried in the bundle is unknown.
    const wanted = partsOf(item.licence);
    const path = Object.keys(lock).find((key) => key.endsWith(`node_modules/${item.id}`) && partsOf(lock[key].license ?? 'unknown')[0] === wanted[0]);
    const own = path ? filesIn(join(root, 'web', path)) : [];
    // A part under another licence than the package (PARTS of components.mjs, read in its code) adds the text of that licence.
    const declared = path ? partsOf(lock[path].license) : [];
    const parts = own.length > 0 ? wanted.filter((part) => !declared.includes(part)).map(template) : [];
    const found = [...(holders.get(item.id) ?? [])];
    entries.push(entry(item, own.length > 0 ? [...own, ...parts] : [], found.length > 0 ? found.join(' ; ') : null));
  }
  return entries;
}

/** The lines of the section of the texts: each distinct text once, after the components it covers, with their notices. */
export function textLines(entries) {
  const groups = new Map();
  for (const item of entries) {
    const key = item.texts.join('\n\n- - -\n\n');
    if (key.includes('~~~')) throw new Error(`The licence of ${item.id} holds a fence of Markdown`);
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }
  const lines = [];
  let rank = 0;
  for (const [text, items] of groups) {
    rank += 1;
    const licences = [...new Set(items.map((item) => item.licence))].join(', ');
    lines.push(`### ${rank}. ${licences}`, '');
    for (const item of items) lines.push(`- \`${item.id}\` ${item.version}${item.notice ? ` : ${item.notice} (texte de la licence déclarée)` : ''}`);
    lines.push('', '~~~text', text, '~~~', '');
  }
  return lines;
}

/**
 * The notices of the packages of Microsoft a publication carries, read in its deps.json (packages and runtime packs):
 * their THIRD-PARTY-NOTICES.TXT, and the licence of each runtime pack; each distinct text once, named after the first
 * package that carries it: the base runtime first, the other runtime packs, then the packages, each in the order of the
 * names.
 */
export function microsoftNotices(depsFile) {
  const libraries = JSON.parse(readFileSync(depsFile, 'utf8')).libraries;
  const seen = new Set();
  const files = [];
  const rank = ([key, library]) => (library.type !== 'runtimepack' ? 2 : key.startsWith('runtimepack.Microsoft.NETCore.App.') ? 0 : 1);
  const ordered = Object.entries(libraries).sort((a, b) => rank(a) - rank(b) || a[0].localeCompare(b[0]));
  for (const [key, library] of ordered) {
    if (library.type !== 'package' && library.type !== 'runtimepack') continue;
    const [raw, version] = key.split('/');
    const id = raw.replace(/^runtimepack\./, '');
    const folder = join(nugetRoot, id.toLowerCase(), version);
    if (!existsSync(folder)) continue;
    const names = readdirSync(folder).filter((name) => /^third-party-notices\.txt$/i.test(name) || (library.type === 'runtimepack' && /^licen[cs]e(\.txt)?$/i.test(name)));
    for (const name of names.sort()) {
      const source = join(folder, name);
      const hash = createHash('sha256').update(readFileSync(source)).digest('hex');
      if (seen.has(hash)) continue;
      seen.add(hash);
      files.push({ source, name: `${id}-${/^third/i.test(name) ? 'THIRD-PARTY-NOTICES' : 'LICENSE'}.txt` });
    }
  }
  return files;
}
