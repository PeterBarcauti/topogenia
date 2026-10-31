// palette.mjs
// Builds the "palette and tokens" section of the mockup from tokens.css itself, with contrast ratios computed on the values read there.

import { contrastRatio } from '../../src/shared/theme/laneTints.ts';

const TINT_NAMES = ['Rose poudré', 'Lavande', 'Bleu ciel', 'Menthe', 'Pêche', 'Vanille', 'Lilas', 'Aqua'];
const LANE_SAMPLES = ['Demandeur', 'Service Achats', 'Logiciel comptable', 'Contrôle de gestion', 'Magasin', 'Direction', 'Qualité', 'Fournisseur'];

const NEUTRAL_USAGE = {
  '--bg-app': 'Fond de l’application',
  '--surface': 'Panneaux, dialogues, étapes',
  '--lane-odd': 'Couloirs impairs',
  '--lane-even': 'Couloirs pairs',
  '--lane-separator': 'Trait entre couloirs',
  '--border': 'Bordures fines',
  '--text': 'Texte principal',
  '--text-muted': 'Texte secondaire',
  '--accent': 'Action principale, sélection',
  '--accent-strong': 'Survol, texte sur fond clair',
  '--edge': 'Flèches (AR-55)',
  '--danger': 'Suppression, erreurs',
  '--warning': 'Avertissements, brouillon',
  '--success': 'Fiche validée',
};

/** Accessibility tokens of AR-53 to AR-59: [token, foreground, background, minimum ratio, label]. */
const ACCESSIBILITY = [
  ['AR-55', '--edge', '--surface', 3, 'Flèche sur couloir impair'],
  ['AR-55', '--edge', '--lane-even', 3, 'Flèche sur couloir pair'],
  ['AR-54', '--focus-outline', '--surface', 3, 'Contour de focus (2 px)'],
  ['AR-56', '--success-text', '--surface', 4.5, 'Texte « validée »'],
  ['AR-56', '--success-text', '--success-soft', 4.5, 'Pastille « validée »'],
  ['AR-56', '--surface', '--success-text', 4.5, 'Numéro « Contrôler »'],
  ['AR-56', '--warning-text', '--warning-soft', 4.5, 'Pastille « brouillon »'],
  ['AR-56', '--warning-text', '--surface', 4.5, 'Texte orange'],
  ['AR-56', '--text-muted', '--hover', 4.5, 'Raccourci sur ligne survolée'],
  ['AR-53', '--text-muted', '--lane-even', 4.5, 'Étape coupée ou hors cadre'],
  ['AR-59', '--warning-graphic', '--surface', 3, 'Jauge « brouillon », pastille « ! »'],
  ['AR-59', '--warning-graphic', '--lane-even', 3, 'Pastille « ! » sur couloir pair'],
  ['AR-59', '--field-border', '--surface', 3, 'Bordure des champs de saisie'],
  ['AR-59', '--accent', '--surface', 3, 'Contour du bouton choisi du ruban'],
];

/** Reads the custom properties of the first :root block, in order, grouped under their comment (one or several lines). */
export function readTokens(css) {
  const start = css.indexOf(':root');
  const root = css.slice(css.indexOf('{', start) + 1, css.indexOf('}', start));
  const groups = [];
  let current = null;
  for (const match of root.matchAll(/\/\*([\s\S]*?)\*\/|(--[\w-]+)\s*:\s*([^;]+);/g)) {
    if (match[1] !== undefined) {
      current = { title: match[1].trim().split('\n')[0].trim(), tokens: [] };
      groups.push(current);
    } else if (current) {
      current.tokens.push({ name: match[2], value: match[3].trim() });
    }
  }
  return groups;
}

const fmt = (ratio) => ratio.toFixed(2).replace('.', ',') + ':1';
const isHex = (v) => /^#[0-9A-Fa-f]{6}$/.test(v);
const isColor = (v) => isHex(v) || v.startsWith('rgba');
/** Colour of a token: its value, or the hex colour inside a composite value such as an outline. */
const colorOf = (value) => (isHex(value) ? value : /#[0-9A-Fa-f]{6}/.exec(value)?.[0]);

function ratioBadge(ratio, min) {
  const ok = ratio >= min;
  return `<span class="ratio ${ok ? 'ratio--ok' : 'ratio--ko'}">${fmt(ratio)} ${ok ? (min >= 4.5 ? 'AA' : 'AA graphique') : 'insuffisant'}</span>`;
}

/** Replaces the var(--x) references of a token value by their values: tokens.css may alias a token to another. */
function resolver(all) {
  const resolve = (value, depth = 0) =>
    depth > 5 ? value : value.replace(/var\((--[\w-]+)\)/g, (ref, name) => (all[name] === undefined ? ref : resolve(all[name], depth + 1)));
  return resolve;
}

function swatch(token, usage, resolve) {
  if (!isColor(resolve(token.value))) return '';
  return (
    `<div class="swatch"><span class="swatch-chip" style="background:var(${token.name})"></span>` +
    `<span class="swatch-text"><code>${token.name}</code><span class="swatch-value">${token.value}</span>` +
    (usage ? `<span class="swatch-usage">${usage}</span>` : '') +
    '</span></div>'
  );
}

/** Sample of a pair: "Aa" for text, an arrow or an outlined box for graphic objects (no text, so no text contrast). */
function sample(fg, bg, min, icon) {
  if (min >= 4.5) return `<span class="contrast-sample" style="color:var(${fg});background:var(${bg})">Aa</span>`;
  if (fg === '--focus-outline') {
    return `<span class="contrast-sample" style="background:var(${bg})"><span class="focus-sample" style="outline:var(--focus-outline);box-shadow:var(--focus-ring)"></span></span>`;
  }
  return `<span class="contrast-sample" style="color:var(${fg});background:var(${bg})">${icon('arrow-right', 18, 1.75)}</span>`;
}

/** Renders the palette section body. */
export function renderPalette(css, icon) {
  const groups = readTokens(css);
  const all = Object.fromEntries(groups.flatMap((g) => g.tokens).map((t) => [t.name, t.value]));
  const group = (prefix) => groups.find((g) => g.title.startsWith(prefix))?.tokens ?? [];
  const neutrals = group('neutrals');
  const derived = group('tokens derived');
  const resolve = resolver(all);
  const ratio = (fg, bg) => contrastRatio(colorOf(resolve(all[fg])), colorOf(resolve(all[bg])));

  let html = '<div class="pal-block"><h3>Neutres et états <span class="pal-ref">§9.2</span></h3><div class="swatch-grid">';
  html += neutrals.map((t) => swatch(t, NEUTRAL_USAGE[t.name], resolve)).join('');
  html += '</div>';
  const textPairs = [
    ['--text', '--surface', 'Texte principal sur surface'],
    ['--text-muted', '--surface', 'Texte secondaire sur surface'],
    ['--text-muted', '--bg-app', 'Texte secondaire sur fond'],
    ['--text-muted', '--lane-even', 'Texte secondaire sur couloir pair'],
    ['--accent-strong', '--surface', 'Lien, texte d’accent'],
    ['--surface', '--accent', 'Texte du bouton principal'],
    ['--danger', '--surface', 'Texte « Supprimer »'],
    ['--danger', '--danger-soft', 'Erreur sur fond rouge doux'],
    ['--danger-muted', '--surface', 'Irritant (pictogramme), sur blanc seulement'],
    ['--text', '--warning-graphic', 'Pastille « ! » (AR-59)'],
  ];
  html += '<div class="contrast-list">';
  for (const [fg, bg, label] of textPairs) {
    html +=
      `<div class="contrast-row">${sample(fg, bg, 4.5, icon)}` +
      `<span>${label}<br><code>${fg}</code> sur <code>${bg}</code></span>${ratioBadge(ratio(fg, bg), 4.5)}</div>`;
  }
  html += '</div></div>';

  html += '<div class="pal-block"><h3>Jetons d’accessibilité <span class="pal-ref">NF08, AR-53 à AR-59</span></h3>';
  html +=
    '<p class="pal-note">Ajoutés à la charte pour tenir 4,5:1 sur tout texte et 3:1 sur les objets graphiques. ' +
    `Étape coupée ou hors cadre (AR-53) : la carte seule est atténuée à <code>--step-cut-opacity</code> ${all['--step-cut-opacity']}, ` +
    'le texte reste à pleine opacité en <code>--text-muted</code>.</p><div class="contrast-list" style="margin-top:0;padding-top:0;border:0">';
  for (const [ref, fg, bg, min, label] of ACCESSIBILITY) {
    html +=
      `<div class="contrast-row">${sample(fg, bg, min, icon)}` +
      `<span>${label} <span class="pal-ref">${ref}</span><br><code>${fg}</code> ${all[fg]} sur <code>${bg}</code></span>${ratioBadge(ratio(fg, bg), min)}</div>`;
  }
  html += '</div></div>';

  html += '<div class="pal-block"><h3>Teintes des couloirs, couples pastel et encre <span class="pal-ref">§9.2, AR-45, AR-47</span></h3>';
  html += '<p class="pal-note">Ratio du texte encre sur le pastel (exigence 4,5:1, NF08) et de l’encre sur blanc (liseré des étapes, contour des losanges, exigence graphique 3:1). Valeurs lues dans <code>tokens.css</code> au moment de la construction.</p><div class="tint-grid">';
  for (let n = 1; n <= 8; n++) {
    const pastel = all[`--lane-${n}-pastel`];
    const ink = all[`--lane-${n}-ink`];
    html +=
      `<div class="tint" style="--pastel:var(--lane-${n}-pastel);--ink:var(--lane-${n}-ink)">` +
      `<div class="tint-head"><span class="tint-num">${n}</span><span class="tint-name">${TINT_NAMES[n - 1]}</span>` +
      `<span class="tint-lane">${LANE_SAMPLES[n - 1]}</span><code>PRJ-C0${n}</code></div>` +
      `<div class="tint-body"><div><code>pastel ${pastel}</code><code>encre ${ink}</code></div>` +
      `<div class="tint-ratios"><span>sur pastel ${ratioBadge(contrastRatio(ink, pastel), 4.5)}</span>` +
      `<span>sur blanc ${ratioBadge(contrastRatio(ink, all['--surface']), 3)}</span></div></div></div>`;
  }
  html += '</div></div>';

  html += '<div class="pal-block"><h3>Jetons dérivés <span class="pal-ref">AR-26, AR-40, AR-41, AR-56</span></h3><div class="swatch-grid swatch-grid--dense">';
  html += derived.map((t) => swatch(t, '', resolve)).join('');
  html += '</div></div>';

  // radii, shadows and opacity: every token of these families, drawn with its own value
  const shapes = Object.keys(all).filter((name) => /^--(radius|shadow)-/.test(name));
  html += '<div class="pal-block"><h3>Rayons et ombres <span class="pal-ref">§9.2, §9.5</span></h3><div class="shape-grid">';
  for (const name of shapes) {
    const style = name.startsWith('--radius') ? `border-radius:var(${name})` : `border-radius:var(--radius-ui);box-shadow:var(${name})`;
    html += `<div class="shape-demo"><div class="shape-box" style="${style}"></div><span><code>${name}</code><br><span class="swatch-value">${all[name]}</span></span></div>`;
  }
  html += '</div></div>';
  return html;
}
