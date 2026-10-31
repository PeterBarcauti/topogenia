// security-list.mjs
// Writes the checked list of docs/securite.md (the requirements of section 5 of the specification) from the rows S-5.x of docs/couverture.md, between its two markers; --check fails when the list is not the one of the coverage.

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const BEGIN = '<!-- list:begin -->';
const END = '<!-- list:end -->';

/** The cells of a row of the coverage: cut on the pipes outside the code spans, an escaped pipe kept. */
function cells(line) {
  const out = [];
  let current = '';
  let code = false;
  for (let index = 0; index < line.length; index++) {
    const char = line[index];
    if (char === '\\' && line[index + 1] === '|') {
      current += '\\|';
      index++;
    } else if (char === '`') {
      code = !code;
      current += char;
    } else if (char === '|' && !code) {
      out.push(current.trim());
      current = '';
    } else {
      current += char;
    }
  }
  return out.slice(1);
}

const sections = { '5.1': 'Principes', '5.2': 'Mode Poste', '5.3': 'Mode Serveur', '5.4': 'Communs aux deux modes', '5.5': 'Journal d’audit', '5.6': 'Faiblesses 1.x corrigées' };
const rows = readFileSync(join(root, 'docs', 'couverture.md'), 'utf8').split('\n')
  .filter((line) => /^\| S-5\.\d-\d\d \|/.test(line))
  .map((line) => cells(line))
  .map(([id, , behaviour, , , , proof, state]) => ({ id, behaviour, proof, state }));
const done = (state) => state.startsWith('couvert');
const list = [BEGIN, ''];
for (const [section, title] of Object.entries(sections)) {
  const own = rows.filter((row) => row.id.startsWith(`S-${section}-`));
  if (own.length === 0) continue;
  list.push(`### ${section} ${title} (${own.filter((row) => done(row.state)).length} sur ${own.length})`, '');
  for (const row of own)
    list.push(`- [${done(row.state) ? 'x' : ' '}] **${row.id}** ${row.behaviour} : *${row.state}*. Preuves : ${row.proof || 'aucune'}.`);
  list.push('');
}
list.push(END);

const file = join(root, 'docs', 'securite.md');
const text = readFileSync(file, 'utf8');
const start = text.indexOf(BEGIN);
const end = text.indexOf(END);
if (start < 0 || end < 0) throw new Error('docs/securite.md has no markers of its list');
const written = text.slice(0, start) + list.join('\n') + text.slice(end + END.length);
if (process.argv.includes('--check')) {
  if (written !== text) {
    console.error('The list of docs/securite.md is not the one of the coverage: run node build/security-list.mjs');
    process.exit(1);
  }
  console.log('docs/securite.md is up to date');
} else {
  writeFileSync(file, written);
  console.log(`${rows.length} requirements, ${rows.filter((row) => done(row.state)).length} covered`);
}
