// icons.mjs
// Reads Lucide icon data (ISC licence) from node_modules/lucide-react and renders it as inline SVG for the mockup.

import { readFileSync } from 'node:fs';

const ICON_DIR = new URL('../../node_modules/lucide-react/dist/esm/icons/', import.meta.url);
const cache = new Map();

/** Returns the [tag, attributes] list of an icon, following the alias files (export { default } from ...). */
function iconNode(name, depth = 0) {
  if (depth > 3) throw new Error(`Icon alias loop on ${name}`);
  const source = readFileSync(new URL(`${name}.mjs`, ICON_DIR), 'utf8');
  const alias = /export \{ default \} from '\.\/([\w-]+)\.mjs'/.exec(source);
  if (alias) return iconNode(alias[1], depth + 1);
  const start = source.indexOf('[', source.indexOf('node:'));
  if (source.indexOf('node:') < 0 || start < 0) throw new Error(`No icon data found for ${name}`);
  // balanced bracket scan (path data contains no brackets)
  let level = 0;
  let end = start;
  for (; end < source.length; end++) {
    if (source[end] === '[') level++;
    else if (source[end] === ']' && --level === 0) break;
  }
  // The data is a JS literal with unquoted keys: quote them and parse it as JSON (no eval).
  const json = source.slice(start, end + 1).replace(/([{,]\s*)([A-Za-z][\w-]*):/g, '$1"$2":').replace(/,(\s*[\]}])/g, '$1');
  return JSON.parse(json);
}

const escapeAttr = (value) => String(value).replace(/&/g, '&amp;').replace(/"/g, '&quot;');

/**
 * Renders a Lucide icon as an inline, decorative SVG.
 * @param {string} name Lucide file name (kebab case)
 * @param {number} size pixel size
 * @param {number} stroke stroke width in the 24 px design grid
 */
export function icon(name, size = 16, stroke = 2) {
  const key = `${name}:${size}:${stroke}`;
  if (cache.has(key)) return cache.get(key);
  const children = iconNode(name)
    .map(([tag, attrs]) => {
      const list = Object.entries(attrs)
        .filter(([attr]) => attr !== 'key')
        .map(([attr, value]) => `${attr}="${escapeAttr(value)}"`)
        .join(' ');
      return `<${tag} ${list}/>`;
    })
    .join('');
  const svg =
    `<svg class="i" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" ` +
    `stroke-width="${stroke}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">${children}</svg>`;
  cache.set(key, svg);
  return svg;
}
