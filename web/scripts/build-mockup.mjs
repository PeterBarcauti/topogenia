// build-mockup.mjs
// Assembles the self-contained static mockup of the charter (P1-T07) into docs/recette/maquette/maquette.html.

import { readFileSync, writeFileSync, mkdirSync, readdirSync } from 'node:fs';
import { icon } from './mockup/icons.mjs';
import { renderMap } from './mockup/map.mjs';
import { renderPalette } from './mockup/palette.mjs';
import { outsideResources } from './mockup/self-contained.mjs';

const MOCKUP_DIR = new URL('./mockup/', import.meta.url);
const TOKENS = new URL('../src/styles/tokens.css', import.meta.url);
const FONT_DIR = new URL('../node_modules/@fontsource-variable/inter/files/', import.meta.url);
const OUT_DIR = new URL('../../docs/recette/maquette/', import.meta.url);
// the Application project folder carries the name too: it is found, not written
const SRC_DIR = new URL('../../src/', import.meta.url);
const APPLICATION_DIR = readdirSync(SRC_DIR).find((dir) => dir.endsWith('.Application'));
if (!APPLICATION_DIR) throw new Error('Application project not found under src/');
const PRODUCT_IDENTITY = new URL(`${APPLICATION_DIR}/ProductIdentity.cs`, SRC_DIR);

// D13: the product name is written only in ProductIdentity.cs; the mockup sources use the {{productName}} marker.
const PRODUCT_NAME = /public const string Name = "([^"]+)"/.exec(readFileSync(PRODUCT_IDENTITY, 'utf8'))?.[1];
if (!PRODUCT_NAME) throw new Error('Product name not found in ProductIdentity.cs');
for (const file of readdirSync(MOCKUP_DIR)) {
  if (readFileSync(new URL(file, MOCKUP_DIR), 'utf8').includes(PRODUCT_NAME)) {
    throw new Error(`D13: literal product name in scripts/mockup/${file}, use {{productName}}`);
  }
}

// unicode ranges of @fontsource-variable/inter (wght.css), latin and latin-ext subsets
const FONT_SUBSETS = [
  {
    file: 'inter-latin-ext-wght-normal.woff2',
    range: 'U+0100-02BA,U+02BD-02C5,U+02C7-02CC,U+02CE-02D7,U+02DD-02FF,U+0304,U+0308,U+0329,U+1D00-1DBF,U+1E00-1E9F,U+1EF2-1EFF,U+2020,U+20A0-20AB,U+20AD-20C0,U+2113,U+2C60-2C7F,U+A720-A7FF',
  },
  {
    file: 'inter-latin-wght-normal.woff2',
    range: 'U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD',
  },
];

/** Reads a mockup source file and drops its two-line header comment (the output has its own). */
function source(name) {
  const text = readFileSync(new URL(name, MOCKUP_DIR), 'utf8').replace(/\r\n/g, '\n');
  return text.replace(/^(?:<!--.*?-->\n|\/\*.*?\*\/\n){2}/, '');
}

function fontFaces() {
  return FONT_SUBSETS.map(({ file, range }) => {
    const data = readFileSync(new URL(file, FONT_DIR)).toString('base64');
    return (
      `@font-face{font-family:'Inter Variable';font-style:normal;font-display:block;font-weight:100 900;` +
      `src:url(data:font/woff2;base64,${data}) format('woff2-variations');unicode-range:${range};}`
    );
  }).join('\n');
}

const tokensCss = readFileSync(TOKENS, 'utf8').replace(/\r\n/g, '\n');

const MAPS = {
  editor: () => renderMap(icon, { width: 1366, height: 768 - 48 - 61, hoverLane: 1 }),
  // annex B 2.1: the panel narrows the canvas, the zoom controls and the minimap move with its right edge; the view is
  // panned 152 px to the left so that the edge of the panel (and its shadow) falls in the alley after column 3, not on
  // the text of a step or of an arrow label
  sheet: () => renderMap(icon, { width: 1366 - 440, height: 768 - 48 - 61, viewX: 40 }),
  menus: () => renderMap(icon, { width: 1366, height: 760, hoverLane: 1, controls: false }),
  notify: () => renderMap(icon, { width: 1366, height: 768 - 48 - 61 }),
};

/** Expands the placeholders: include, icon, map, palette, tokens, fonts, css, productName. */
function expand(text, depth = 0) {
  if (depth > 5) throw new Error('Include depth exceeded');
  return text.replace(/\{\{([\w-]+)(?::([^}]*))?\}\}/g, (all, kind, arg) => {
    switch (kind) {
      case 'include':
        return expand(source(arg), depth + 1);
      case 'css':
        return source(arg);
      case 'productName':
        return PRODUCT_NAME;
      case 'icon': {
        const [name, size, stroke] = arg.split(':');
        return icon(name, size ? Number(size) : 16, stroke ? Number(stroke) : 2);
      }
      case 'map':
        if (!MAPS[arg]) throw new Error(`Unknown map ${arg}`);
        return MAPS[arg]();
      case 'palette':
        return renderPalette(tokensCss, icon);
      case 'tokens':
        return tokensCss;
      case 'fonts':
        return fontFaces();
      default:
        throw new Error(`Unknown placeholder ${all}`);
    }
  });
}

const html = expand(source('template.html'));

// Guards of D14 (no network call, one file: no relative or absolute path either) and of the file format rules
// (no emoji, no leftover placeholder).
const outside = outsideResources(html);
if (outside.length > 0) throw new Error(`Resource outside the page: ${outside.join(', ')}`);
if (/\p{Extended_Pictographic}/u.test(html)) throw new Error('Pictographic character found in the mockup');
if (/\{\{[\w-]+/.test(html)) throw new Error('Unexpanded placeholder left in the mockup');

const header =
  '<!-- maquette.html -->\n' +
  `<!-- Static mockup of the ${PRODUCT_NAME} 2.0 charter, generated by web/scripts/build-mockup.mjs: do not edit. -->\n`;
mkdirSync(OUT_DIR, { recursive: true });
const out = new URL('maquette.html', OUT_DIR);
writeFileSync(out, header + html);
console.log(`maquette.html written (${Math.round((header.length + html.length) / 1024)} KiB)`);
