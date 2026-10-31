// styleSheets.test.ts
// The style sheets read as a whole (annex B 8.1, 8.2, spec 3.5, 9.3): the geometry from one module, no variable left undefined, nothing of the dead CSS of the 1.x, the diamonds in an ink that reads, the lane header raised on hover.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { geometryCssVariables } from '../features/map/model/geometry/constants.ts';
import { contrastRatio, resolvedTints } from '../shared/theme/laneTints.ts';

const src = join(import.meta.dirname, '..');
const strip = (css: string): string => css.replace(/\/\*[\s\S]*?\*\//g, '');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

const shipped = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/'));
const sheets = new Map(shipped.filter((file) => file.endsWith('.css')).map((file) => [file, strip(readFileSync(join(src, file), 'utf8'))]));
const scripts = new Map(shipped.filter((file) => /\.tsx?$/.test(file)).map((file) => [file, readFileSync(join(src, file), 'utf8')]));

/** The rules of a style sheet: their selectors and their declarations. */
const rulesOf = (css: string): { selector: string; body: string }[] =>
  [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({ selector: (match[1] ?? '').trim().replace(/\s+/g, ' '), body: match[2] ?? '' }));

/** The custom properties a text uses by var(), and those it sets (in a rule, or as the key of a style object). */
const usedIn = (text: string): string[] => [...text.matchAll(/var\(\s*(--[\w-]+)(?![\w-]*\$\{)/g)].map((match) => match[1] ?? '').filter((name) => !name.endsWith('-'));
const setIn = (text: string): string[] => [...text.matchAll(/(?:^|[\s;{'"])(--[\w-]+)['"]?\s*:/g)].map((match) => match[1] ?? '');

/** The variables used that nothing sets: neither a style sheet, nor a script, nor the geometry. */
function undefinedVariables(sources: ReadonlyMap<string, string>): string[] {
  const set = new Set([...sources.values()].flatMap(setIn).concat(Object.keys(geometryCssVariables())));
  return [...sources].flatMap(([file, text]) => usedIn(text).filter((name) => !set.has(name)).map((name) => `${file} ${name}`));
}

/** The declarations of the rules that size the steps, the lane headers and the controls of the canvas with a literal of the geometry. */
function geometryLiterals(css: string): string[] {
  const vars = geometryCssVariables();
  const pixels = ['--step-width', '--step-height', '--lane-header-width', '--column-width', '--row-height', '--controls-right', '--minimap-width'].map((name) => vars[name] ?? name);
  return rulesOf(css)
    .filter((rule) => /step|lane-head|lane-body|map-controls|map-minimap|react-flow__node/.test(rule.selector))
    .flatMap((rule) => pixels.filter((pixel) => new RegExp(`(?<![\\d.])${pixel}`).test(rule.body)).map((pixel) => `${rule.selector}: ${pixel}`));
}

describe('the geometry of the map in the style sheets (B-8.1-31, B-8.2-05, spec 3.5)', () => {
  it('no rule of a step, a lane header or the controls writes a size of the geometry: they read its variables', () => {
    expect([...sheets.values()].flatMap(geometryLiterals)).toEqual([]);
    expect(geometryLiterals('.lane-head { width: 184px; } .step { height: 64px; } .map-canvas .react-flow__panel.map-controls { right: 228px; } .raci-badge { max-width: 120px; }'))
      .toEqual(['.lane-head: 184px', '.step: 64px', '.map-canvas .react-flow__panel.map-controls: 228px']);
  });

  it('its variables are set by the one module of the geometry, given to the canvas, and declared by no style sheet', () => {
    const names = Object.keys(geometryCssVariables());
    expect(names.filter((name) => [...sheets.values()].some((css) => setIn(css).includes(name)))).toEqual([]);
    expect(names.filter((name) => [...sheets.values()].some((css) => usedIn(css).includes(name))).length).toBeGreaterThan(15);
    expect(scripts.get('features/map/canvas/Canvas.tsx')).toMatch(/style=\{geometryCssVariables\(\)\}/);
  });
});

describe('no variable undefined, nothing of the dead CSS of the 1.x (B-8.1-36, B-8.1-37)', () => {
  it('every variable used by a style sheet or a script is set: by tokens.css, a rule, a style object or the geometry', () => {
    expect(undefinedVariables(new Map([...sheets, ...scripts]))).toEqual([]);
    expect(undefinedVariables(new Map([['a.css', '.x { color: var(--erreur, red); background: var(--surface); } :root { --surface: white; }']]))).toEqual(['a.css --erreur']);
  });

  it('none of the dead names of the 1.x: --erreur, --proposition, .outils, the classes of state named in French', () => {
    const dead = /--erreur\b|--proposition\b|\.outils\b|\.etat-validee\b|\.etat-a_revoir\b/;
    expect([...sheets].filter(([, css]) => dead.test(css)).map(([file]) => file)).toEqual([]);
    expect([...scripts].filter(([, text]) => /--erreur|--proposition|'outils'|etat-validee|etat-a_revoir/.test(text)).map(([file]) => file)).toEqual([]);
    expect(dead.test('.etat-fiche.etat-validee { color: var(--erreur); }')).toBe(true);
  });
});

describe('the marker of the diamonds (B-8.1-38, spec 9.4)', () => {
  it('drawn in the ink of its lane on the white of the diamond, each ink reaching 4.5:1 on it, beyond the 3:1 of a graphic object', () => {
    const map = sheets.get('styles/map.css') ?? '';
    expect(rulesOf(map).filter((rule) => rule.selector === '.gateway-marker').map((rule) => /color:\s*var\(--ink\)/.test(rule.body))).toContain(true);
    expect(rulesOf(map).some((rule) => rule.selector === '.gateway-shape polygon' && /fill:\s*var\(--surface\)/.test(rule.body))).toBe(true);
    const surface = /--surface:\s*(#[0-9A-Fa-f]{6})/.exec(sheets.get('styles/tokens.css') ?? '')?.[1] ?? '';
    expect(resolvedTints().map((tint) => contrastRatio(tint.ink, surface) >= 4.5)).toEqual(resolvedTints().map(() => true));
    expect(contrastRatio('#DADADA', surface)).toBeLessThan(3);
  });
});

describe('the lane header on hover (S-9.3-01, spec 9.3)', () => {
  it('raised by the shadow of the charter, in the short duration, and its « ⋯ » button of the menu shown', () => {
    const rules = rulesOf(sheets.get('styles/map.css') ?? '');
    const body = (selector: string) => rules.filter((rule) => rule.selector.split(', ').includes(selector)).map((rule) => rule.body).join(';');
    expect(body('.lane-head:hover')).toMatch(/box-shadow:\s*var\(--shadow-raised\)/);
    expect(body('.lane-head')).toMatch(/transition:\s*box-shadow var\(--duration-fast\)/);
    expect(body('.lane-head:hover .lane-more')).toMatch(/opacity:\s*1/);
    expect(body('.lane-more')).toMatch(/opacity:\s*0/);
  });
});
