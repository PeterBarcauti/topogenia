// third-party-notices.mjs
// Writes THIRD-PARTY-NOTICES.md from the components of build/components.mjs, the table then the texts of the licences of those that ship (build/licence-texts.mjs); with --check, writes nothing and fails when the file is not the one it would write.

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { components, root } from './components.mjs';
import { shippedTexts, textLines } from './licence-texts.mjs';

const all = components();
const { nuget, npm, carried } = all;
const row = (eco, p) => `| ${eco} | \`${p.id}\` | ${p.version} | ${p.licence} | ${p.usage} |`;
// A component delivered that the tests or the journeys of the interface import as well is declared among its dependencies
// of development (web/package.json), so that they do not hang on another package bringing it (relecture lot 4, m3).
const devOfWeb = JSON.parse(readFileSync(join(root, 'web', 'package-lock.json'), 'utf8')).packages['']?.devDependencies ?? {};
const testedToo = npm.filter((p) => p.usage === 'livré' && p.path === `node_modules/${p.id}` && Object.hasOwn(devOfWeb, p.id)).map((p) => `\`${p.id}\``);
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
  ...(testedToo.length === 0 ? [] : ['', 'Livrés, et importés aussi par les essais et les parcours de l\'interface (déclarés en dépendances de',
    `développement de \`web/package.json\`) : ${testedToo.join(', ')}.`]),
  '',
  '## Le runtime .NET',
  '',
  'Le runtime .NET 10 (`Microsoft.NETCore.App` et `Microsoft.AspNetCore.App`, licence MIT, © .NET Foundation and',
  'Contributors) est embarqué dans l\'exécutable du mode Poste (ZIP portable et paquet du Store) ; le mode Serveur',
  'utilise celui du serveur. Sa version exacte est dans le SBOM (`sbom.cdx.json`). Son texte de licence et la liste des',
  'composants qu\'il inclut (`LICENSE.TXT` et `THIRD-PARTY-NOTICES.TXT` de ses paquets d\'exécution), comme la liste',
  'que porte chaque paquet de Microsoft livré, sont copiés dans le dossier `licenses/microsoft` des paquets.',
  '',
  '## Textes des licences',
  '',
  'Pour chaque composant livré : le texte de licence que porte son paquet (avec son avis de copyright), sinon le texte',
  'de la licence qu\'il déclare, précédé de l\'avis tiré de ses métadonnées (copyright du `.nuspec`, auteur du',
  '`package.json`, lignes de copyright de ses sources dans la carte de l\'assemblage). Pour un paquet porté dans un',
  'fichier déjà assemblé, sa version n\'est pas connue : le texte est celui de la copie du même nom installée quand elle',
  'déclare la même licence. Un même texte n\'est écrit qu\'une fois, après les composants qu\'il couvre.',
  '',
  ...textLines(shippedTexts(all)),
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
