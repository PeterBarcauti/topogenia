// third-party-notices.mjs
// Writes THIRD-PARTY-NOTICES.md from the components of build/components.mjs; with --check, writes nothing and fails when the file is not the one it would write.

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { components, root } from './components.mjs';

const { nuget, npm, carried } = components();
const row = (eco, p) => `| ${eco} | \`${p.id}\` | ${p.version} | ${p.licence} | ${p.usage} |`;
const sorted = (list) => [...list].sort((a, b) => a.usage.localeCompare(b.usage) || a.id.localeCompare(b.id) || a.version.localeCompare(b.version));
const lines = [
  '<!-- THIRD-PARTY-NOTICES.md -->',
  '<!-- Third-party components, direct and transitive, with version, licence and whether they ship (generated) -->',
  '',
  '# Composants tiers',
  '',
  'Généré par `node build/third-party-notices.mjs` (`build/components.mjs`) depuis les graphes de restauration NuGet,',
  '`web/package-lock.json` et la liste des paquets que l\'interface construite sert (`web/served-packages.json`, écrite',
  'par `npm run build`), dépendances transitives comprises. Colonne « Usage » : `livré` (dans les paquets), `tests` ou',
  '`développement` (jamais livrés), `non servi` (dépendance de production dont le navigateur ne reçoit pas le code : la',
  'partie Node d\'exceljs, servi par son paquet navigateur, ADR 065). Les fichiers déjà assemblés que l\'interface sert',
  'tels quels embarquent d\'autres paquets : ceux de leur carte des sources (`exceljs.min.js`), et les dépendances d\'un',
  'fichier assemblé sans carte qu\'ils contiennent (`jszip.min.js`) ; leur version est « dans exceljs », celle de sa',
  'construction et non celle du verrouillage ; leur licence est celle du verrouillage pour un paquet du même nom, sinon',
  'celle de leur `package.json` embarqué, sinon « relevée » à la construction (à confirmer en ligne à la recette).',
  'Vite retire les bannières de licence des morceaux servis : ce fichier porte l\'attribution.',
  'Règle : licence permissive (spec 0.2) pour tout composant livré, vérifiée par `ThirdPartyNoticesTests`, qui vérifie',
  'aussi que ce fichier est celui que donne le générateur (`--check`) ; les exceptions sont justifiées dans',
  '`docs/decisions/` (ADR 020 axe-core, ADR 024 composants natifs de SQL Server).',
  '',
  '| Écosystème | Composant | Version | Licence | Usage |',
  '|---|---|---|---|---|',
  ...sorted(nuget).map((p) => row('NuGet', p)),
  ...sorted(npm).map((p) => row('npm', p)),
  ...sorted(carried).map((p) => row('npm', p)),
  '',
  'La police Inter (OFL-1.1) et les icônes Lucide (ISC) sont embarquées dans l\'interface ; leurs textes de',
  'licence sont livrés avec les paquets (dossier `licenses` des ZIP).',
  '',
];
const text = lines.join('\n');
const file = join(root, 'THIRD-PARTY-NOTICES.md');
if (process.argv.includes('--check')) {
  const current = readFileSync(file, 'utf8');
  if (current !== text) {
    const before = new Set(current.split('\n'));
    const after = new Set(lines);
    for (const line of lines.filter((line) => !before.has(line)).slice(0, 10)) console.error(`+ ${line}`);
    for (const line of current.split('\n').filter((line) => !after.has(line)).slice(0, 10)) console.error(`- ${line}`);
    console.error('THIRD-PARTY-NOTICES.md is not the one the components give: run node build/third-party-notices.mjs');
    process.exit(1);
  }
  console.log('THIRD-PARTY-NOTICES.md is up to date');
} else {
  writeFileSync(file, text);
  console.log(`${nuget.length} NuGet, ${npm.length} npm and ${carried.length} carried packages written`);
}
