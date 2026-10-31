// tokens.test.ts
// Charter checks: values of the spec, single declaration of tokens, reduced motion, contrast, lane block, no colour outside tokens.css.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { LANE_TINTS, MIN_TEXT_CONTRAST, contrastRatio, ensureContrast, laneTokensBlock, resolvedTints } from './laneTints.ts';

const webDir = join(import.meta.dirname, '../../..');
const tokensCss = readFileSync(join(webDir, 'src/styles/tokens.css'), 'utf8');
const mockupPath = join(webDir, '../docs/recette/maquette/maquette.html');

const stripComments = (css: string): string => css.replace(/\/\*[\s\S]*?\*\//g, '');

/** Body of the first :root block (the token declarations; the reduced motion block comes after). */
function rootBlock(css: string): string {
  const match = /:root\s*\{([^}]*)\}/.exec(stripComments(css));
  if (!match?.[1]) throw new Error(':root block missing');
  return match[1];
}

/** Custom property declarations of a block, in order. */
function declarations(block: string): [string, string][] {
  return [...block.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)].map((m) => [m[1] ?? '', (m[2] ?? '').trim()]);
}

/** Tokens declared more than once in :root (the last value would silently win). */
function duplicateTokens(css: string): string[] {
  const names = declarations(rootBlock(css)).map(([name]) => name);
  return [...new Set(names.filter((name, i) => names.indexOf(name) !== i))];
}

/** Duration tokens of :root that the prefers-reduced-motion block does not set to 0. */
function motionNotCut(css: string): string[] {
  const durations = declarations(rootBlock(css)).filter(([name]) => name.startsWith('--duration-')).map(([name]) => name);
  const block = /@media\s*\(\s*prefers-reduced-motion\s*:\s*reduce\s*\)\s*\{\s*:root\s*\{([^}]*)\}/.exec(stripComments(css))?.[1] ?? '';
  const cut = new Map(declarations(block));
  return durations.filter((name) => !/^0(ms|s)?$/.test(cut.get(name) ?? ''));
}

/** Custom properties assigned to another one (--x: var(--y)): allowed in tokens.css only. */
function tokenAliases(css: string): string[] {
  return [...stripComments(css).matchAll(/(--[\w-]+)\s*:\s*var\(/g)].map((m) => m[1] ?? '');
}

/**
 * Tokens of tokens.css that a source declares again: --x: value, and in a TS or TSX style object '--x': value (a
 * script of the mockup may key a plain object by token names, such as the usage labels of the palette).
 */
function redefinedTokens(text: string, names: readonly string[], styleObjects = false): string[] {
  const code = stripComments(text);
  const quote = styleObjects ? `["']?` : '';
  return names.filter((name) => new RegExp(String.raw`(?<![\w-])${name}${quote}\s*:`).test(code));
}

/** Comparison key of a CSS value: spaces, case and the leading zero of decimals do not count. */
const normalise = (value: string): string =>
  value.replace(/\s+/g, ' ').replace(/\s*([,()])\s*/g, '$1').replace(/(^|[^\d])0\./g, '$1.').trim().toUpperCase();

/** Names whose value differs between actual and expected (missing names included). */
function mismatches(actual: (name: string) => string | undefined, expected: Record<string, string>): string[] {
  return Object.entries(expected)
    .filter(([name, value]) => normalise(actual(name) ?? '') !== normalise(value))
    .map(([name]) => name);
}

const rootTokens = new Map(declarations(rootBlock(tokensCss)));
/** Value of a token, its var(--x) references replaced by their values (a token may be an alias of another). */
function resolve(value: string, depth = 0): string {
  if (depth > 5) throw new Error(`Alias loop in ${value}`);
  return value.replace(/var\((--[\w-]+)\)/g, (ref, name: string) => {
    const target = rootTokens.get(name);
    return target === undefined ? ref : resolve(target, depth + 1);
  });
}
const token = (name: string): string => {
  const value = rootTokens.get(`--${name}`);
  if (value === undefined) throw new Error(`Token --${name} missing`);
  return resolve(value);
};

function filesUnder(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? filesUnder(path) : [path];
  });
}

/** Sources of the interface and of the mockup, relative to web/ (tokens.css, laneTints.ts and the tests excepted). */
const SOURCES = [...filesUnder(join(webDir, 'src')), ...filesUnder(join(webDir, 'scripts/mockup'))]
  .filter((f) => /\.(css|tsx?|html|mjs)$/.test(f))
  .filter((f) => !f.endsWith('tokens.css') && !f.endsWith('.test.ts') && !f.endsWith('.test.tsx') && !f.endsWith('laneTints.ts'))
  .map((f) => f.slice(webDir.length + 1));
const CSS_SOURCES = SOURCES.filter((f) => f.endsWith('.css'));
const readSource = (relative: string): string => readFileSync(join(webDir, relative), 'utf8');

/** Neutral tokens of the spec (9.2), except those adjusted by an accessibility arbitration (AR-55). */
const SPEC_NEUTRALS: Record<string, string> = {
  'bg-app': '#F7F7F8', surface: '#FFFFFF', 'lane-odd': '#FFFFFF', 'lane-even': '#F5F5F5',
  'lane-separator': '#E8E8EC', border: '#E3E4E8', text: '#1D1D1F', 'text-muted': '#6B6E76',
  accent: '#5B5BD6', 'accent-strong': '#4343B8', danger: '#C0362C', warning: '#E08A00', success: '#2F8F5B',
};

/** Shadows, focus ring and radii of the spec (9.2), as written there. */
const SPEC_SHAPES: Record<string, string> = {
  'shadow-step': '0 1px 2px rgba(16,24,40,.06), 0 1px 3px rgba(16,24,40,.10)',
  'shadow-raised': '0 4px 12px rgba(16,24,40,.12)',
  'focus-ring': '0 0 0 3px rgba(91, 91, 214, .35)',
  'radius-step': '10px',
  'radius-ui': '8px',
};

/** Dimensions of the charter (9.3 to 9.5) that are tokens. */
const SPEC_DIMENSIONS: Record<string, string> = {
  'header-height': '48px', 'ribbon-button': '32px', 'side-panel-width': '440px', 'menu-width': '240px',
  'font-size-base': '14px', 'font-size-step': '13px', 'font-size-code': '10.5px', 'font-size-lane-name': '13px',
  'font-size-lane-code': '11px', 'duration-fast': '150ms', 'duration-base': '200ms',
  // the command bar (annex B 2.13-01)
  'command-bar-width': '560px', 'command-bar-top': '18%',
};

/** Lane tints of the spec (9.2): number, pastel, ink. */
const SPEC_TINTS: [number, string, string][] = [
  [1, '#F9DCE6', '#8A3459'], [2, '#E5E0F8', '#4F3F99'], [3, '#D9E9FA', '#285B8C'], [4, '#D5F0E3', '#276A4F'],
  [5, '#FCE3D4', '#8A4424'], [6, '#F8EFC8', '#6E5A10'], [7, '#EEDDF4', '#6E3D86'], [8, '#D4EEF0', '#25646B'],
];

/** Tints of laneTints.ts that differ from the spec table. */
function tintMismatches(tints: readonly { pastel: string; ink: string }[]): number[] {
  if (tints.length !== SPEC_TINTS.length) return [-1];
  return SPEC_TINTS.filter(([n, pastel, ink]) => {
    const t = tints[n - 1];
    return !t || t.pastel.toUpperCase() !== pastel || t.ink.toUpperCase() !== ink;
  }).map(([n]) => n);
}

/** Text on background pairs used by the interface: each must reach 4.5:1 (NF08). */
const TEXT_PAIRS: [string, string][] = [
  ['text', 'surface'], ['text', 'bg-app'], ['text', 'lane-even'], ['text', 'hover'],
  ['text-muted', 'surface'], ['text-muted', 'bg-app'], ['text-muted', 'lane-even'], ['text-muted', 'hover'],
  ['text-muted', 'warning-soft'], ['accent-strong', 'surface'], ['accent-strong', 'frame-label-bg'],
  ['danger', 'surface'], ['danger', 'danger-soft'], ['text', 'danger-soft'], ['danger-muted', 'surface'],
  ['success-text', 'surface'], ['success-text', 'success-soft'], ['warning-text', 'warning-soft'], ['warning-text', 'surface'],
  ['initials-fg', 'initials-bg'], ['toast-fg', 'toast-bg'], ['text', 'table-header-bg'], ['text', 'other-input-bg'],
  ['other-input-border', 'other-input-bg'], ['text', 'warning-graphic'],
];

/**
 * --danger-muted reaches 4.43:1 only on --lane-even: while it stays under 4.5:1 there, it colours text on white cards
 * only, that is the irritant pictogram of a step card (the mockup check reads the real background of each use).
 */
const DANGER_MUTED_TEXT_RULES = ['.step-pictos .picto-irritant'];

/** Selectors of the rules that use a token as text colour. */
function textColourRules(css: string, name: string): string[] {
  const colour = new RegExp(String.raw`(?<![\w-])color\s*:\s*var\(${name}\)`);
  return [...stripComments(css).matchAll(/([^{}]+)\{([^}]*)\}/g)].filter((m) => colour.test(m[2] ?? '')).map((m) => (m[1] ?? '').trim());
}

/** Useful graphic objects (WCAG 1.4.11, AR-55, AR-59): each must reach 3:1. */
const GRAPHIC_PAIRS: [string, string][] = [
  ['edge', 'lane-odd'], ['edge', 'lane-even'], ['warning-graphic', 'surface'], ['warning-graphic', 'lane-even'],
  ['field-border', 'surface'], ['accent', 'surface'], ['accent', 'lane-even'],
];

/** The 148 named colours of CSS Color 4 (transparent and currentColor are keywords, not named colours). */
const NAMED_COLOURS = (
  'aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue blueviolet brown burlywood ' +
  'cadetblue chartreuse chocolate coral cornflowerblue cornsilk crimson cyan darkblue darkcyan darkgoldenrod darkgray ' +
  'darkgreen darkgrey darkkhaki darkmagenta darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen ' +
  'darkslateblue darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray dimgrey dodgerblue ' +
  'firebrick floralwhite forestgreen fuchsia gainsboro ghostwhite gold goldenrod gray green greenyellow grey honeydew ' +
  'hotpink indianred indigo ivory khaki lavender lavenderblush lawngreen lemonchiffon lightblue lightcoral lightcyan ' +
  'lightgoldenrodyellow lightgray lightgreen lightgrey lightpink lightsalmon lightseagreen lightskyblue lightslategray ' +
  'lightslategrey lightsteelblue lightyellow lime limegreen linen magenta maroon mediumaquamarine mediumblue ' +
  'mediumorchid mediumpurple mediumseagreen mediumslateblue mediumspringgreen mediumturquoise mediumvioletred ' +
  'midnightblue mintcream mistyrose moccasin navajowhite navy oldlace olive olivedrab orange orangered orchid ' +
  'palegoldenrod palegreen paleturquoise palevioletred papayawhip peachpuff peru pink plum powderblue purple ' +
  'rebeccapurple red rosybrown royalblue saddlebrown salmon sandybrown seagreen seashell sienna silver skyblue ' +
  'slateblue slategray slategrey snow springgreen steelblue tan teal thistle tomato turquoise violet wheat white ' +
  'whitesmoke yellow yellowgreen'
).split(' ');
const namedColour = new RegExp(String.raw`(?<![\w.#-])(?:${NAMED_COLOURS.join('|')})(?![\w-])`, 'i');

/** True when a CSS value names a colour (quoted strings and the names inside var() are not values). */
const namesColour = (value: string): boolean =>
  namedColour.test(value.replace(/(["'])(?:(?!\1).)*\1/g, '').replace(/var\(\s*--[\w-]+/g, 'var('));

/** Declarations of a style sheet or of a style attribute whose value names a colour. */
function namedColoursInCss(css: string): string[] {
  return [...stripComments(css).matchAll(/([\w-]+)\s*:\s*([^;{}]+)/g)]
    .filter((m) => namesColour(m[2] ?? ''))
    .map((m) => `${m[1] ?? ''}: ${(m[2] ?? '').trim()}`);
}

/** Named colours in HTML (or HTML built by a script): style attributes, SVG fill, stroke, stop-color and color. */
function namedColoursInAttributes(html: string): string[] {
  const styles = [...html.matchAll(/(?<![\w-])style\s*=\s*(["'])(.*?)\1/gs)].flatMap((m) => namedColoursInCss(m[2] ?? ''));
  const paints = [...html.matchAll(/(?<![\w-])(fill|stroke|stop-color|color)\s*=\s*(["'])(.*?)\2/gs)]
    .filter((m) => namesColour(m[3] ?? ''))
    .map((m) => `${m[1] ?? ''}="${m[3] ?? ''}"`);
  return [...styles, ...paints];
}

/** Named colours in the colour properties of style objects (TS, TSX, scripts): color: 'red', markerEnd: { color: 'black' }. */
function namedColoursInObjects(code: string): string[] {
  const key = String.raw`[A-Za-z]*(?:[Cc]olou?r|[Ff]ill|[Ss]troke|[Bb]ackground|[Bb]order|[Oo]utline|[Ss]hadow|[Dd]ecoration)[A-Za-z]*`;
  const property = new RegExp(String.raw`(?<![\w$.-])(${key})\s*:\s*(["'\`])((?:(?!\2).)*)\2`, 'g');
  return [...code.matchAll(property)].filter((m) => namesColour(m[3] ?? '')).map((m) => `${m[1] ?? ''}: '${m[3] ?? ''}'`);
}

/** Every named colour of a source, by its kind: style sheet, HTML attributes, style objects. */
function namedColours(path: string, text: string): string[] {
  return [
    ...(path.endsWith('.css') ? namedColoursInCss(text) : []),
    ...namedColoursInAttributes(text),
    ...(/\.(tsx?|mjs)$/.test(path) ? namedColoursInObjects(text) : []),
  ];
}

describe('tokens.css declares each token once (spec 9.2)', () => {
  it('no token is declared twice in :root', () => {
    expect(duplicateTokens(tokensCss)).toEqual([]);
  });

  it('detects a token declared twice', () => {
    expect(duplicateTokens(':root {\n  --edge: #80858E;\n  --text: #1D1D1F;\n  --edge: #8A8F98;\n}')).toEqual(['--edge']);
  });
});

describe('charter colours (spec 9.2, AR-54, AR-55, AR-59)', () => {
  it.each(Object.entries(SPEC_NEUTRALS))('--%s keeps the value of the spec', (name, value) => {
    expect(token(name).toUpperCase()).toBe(value.toUpperCase());
  });

  it('the eight tints of laneTints.ts are those of the spec table (pastel and ink)', () => {
    expect(tintMismatches(LANE_TINTS)).toEqual([]);
  });

  it('detects a tint that differs from the spec table', () => {
    const altered = LANE_TINTS.map((t, i) => (i === 2 ? { ...t, ink: '#3A6E9E' } : t));
    expect(tintMismatches(altered)).toEqual([3]);
    expect(tintMismatches(LANE_TINTS.slice(0, 6))).toEqual([-1]);
  });

  it('--edge is the darkened edge colour of AR-55', () => {
    expect(token('edge')).toBe('#80858E');
  });

  it('--field-border is --edge (AR-59)', () => {
    expect(token('field-border')).toBe(token('edge'));
  });

  it('the focus outline reaches 3:1 on the surface (AR-54)', () => {
    const colour = /#[0-9A-F]{6}/i.exec(token('focus-outline'))?.[0] ?? '';
    expect(contrastRatio(colour, token('surface'))).toBeGreaterThanOrEqual(3);
  });
});

describe('charter shapes and dimensions (spec 9.2 to 9.5)', () => {
  it('shadows, focus ring and radii keep the values of the spec', () => {
    expect(mismatches((name) => rootTokens.get(`--${name}`), SPEC_SHAPES)).toEqual([]);
  });

  it('dimensions, font sizes and durations keep the values of the charter', () => {
    expect(mismatches((name) => rootTokens.get(`--${name}`), SPEC_DIMENSIONS)).toEqual([]);
  });

  it('detects a shadow, a dimension or a token that differs from the spec', () => {
    const altered = new Map([['shadow-step', '0 1px 2px rgba(16, 24, 40, .06)'], ['menu-width', '260px']]);
    expect(mismatches((name) => altered.get(name), { 'shadow-step': SPEC_SHAPES['shadow-step'] ?? '', 'menu-width': '240px', 'radius-ui': '8px' }))
      .toEqual(['shadow-step', 'menu-width', 'radius-ui']);
    // spacing and the leading zero do not count
    expect(mismatches(() => '0 4px 12px rgba(16, 24, 40, 0.12)', { x: '0 4px 12px rgba(16,24,40,.12)' })).toEqual([]);
  });
});

describe('reduced motion (spec 9.5)', () => {
  it('prefers-reduced-motion sets every duration token to 0', () => {
    expect([...rootTokens.keys()].filter((name) => name.startsWith('--duration-')).length).toBeGreaterThan(0);
    expect(motionNotCut(tokensCss)).toEqual([]);
  });

  it('detects a duration that is not cut, or a missing block', () => {
    const root = ':root {\n  --duration-fast: 150ms;\n  --duration-base: 200ms;\n}\n';
    expect(motionNotCut(root + '@media (prefers-reduced-motion: reduce) {\n  :root { --duration-fast: 0ms; }\n}')).toEqual(['--duration-base']);
    expect(motionNotCut(root)).toEqual(['--duration-fast', '--duration-base']);
  });
});

describe('text contrast (NF08)', () => {
  it.each(TEXT_PAIRS)('--%s on --%s reaches 4.5:1', (fg, bg) => {
    expect(contrastRatio(token(fg), token(bg))).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST);
  });

  it('--danger-muted colours text on --lane-even only if it reaches 4.5:1 there, on white cards otherwise', () => {
    const onLaneEven = contrastRatio(token('danger-muted'), token('lane-even'));
    const rules = CSS_SOURCES.flatMap((f) => textColourRules(readSource(f), '--danger-muted'));
    expect(onLaneEven >= MIN_TEXT_CONTRAST || rules.every((r) => DANGER_MUTED_TEXT_RULES.includes(r))).toBe(true);
  });

  it('finds the rules that use a token as text colour', () => {
    const css = '.a { color: var(--danger-muted); }\n/* .b { color: var(--danger-muted); } */\n.c { border-color: var(--danger-muted); }';
    expect(textColourRules(css, '--danger-muted')).toEqual(['.a']);
  });
});

describe('graphic objects (WCAG 1.4.11)', () => {
  it.each(GRAPHIC_PAIRS)('--%s on --%s reaches 3:1', (fg, bg) => {
    expect(contrastRatio(token(fg), token(bg))).toBeGreaterThanOrEqual(3);
  });
});

describe('lane tints (spec 9.2, AR-36)', () => {
  it.each(resolvedTints().map((t, i) => [i + 1, t] as const))('tint %i reaches 4.5:1 between pastel and ink', (_n, tint) => {
    expect(contrastRatio(tint.ink, tint.pastel)).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST);
  });

  it('darkens an ink that misses the minimum until it passes', () => {
    const adjusted = ensureContrast('#E39AB8', '#F9DCE6');
    expect(adjusted).not.toBe('#E39AB8');
    expect(contrastRatio(adjusted, '#F9DCE6')).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST);
  });

  it('stops with an error instead of looping when no ink can pass', () => {
    expect(() => ensureContrast('#444444', '#333333')).toThrow();
  });

  it('computes the WCAG ratio of black on white as 21', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 5);
  });

  it('tokens.css carries the generated lane block (run npm run tokens)', () => {
    expect(tokensCss).toContain(laneTokensBlock());
  });
});

describe('no colour literal outside tokens.css (spec 9.2)', () => {
  const literal = /#[0-9A-Fa-f]{3,8}\b|\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color-mix|color)\(/;

  it.each(SOURCES.map((f) => [f]))('%s writes no colour literal', (relative) => {
    expect(readSource(relative)).not.toMatch(literal);
  });

  it('detects the literals it must refuse', () => {
    for (const bad of ['color: #fff;', 'background: rgb(1,2,3)', 'color: oklch(70% .1 200)', 'color: color-mix(in srgb, red, blue)'])
      expect(bad).toMatch(literal);
  });
});

describe('no named colour outside tokens.css (spec 9.2)', () => {
  it('the list holds the 148 named colours of CSS, each once', () => {
    expect(new Set(NAMED_COLOURS).size).toBe(148);
  });

  it.each(SOURCES.map((f) => [f]))('%s writes no named colour', (relative) => {
    expect(namedColours(relative, readSource(relative))).toEqual([]);
  });

  it('the generated maquette.html has no named colour in its style and SVG colour attributes', () => {
    expect(namedColoursInAttributes(readFileSync(mockupPath, 'utf8'))).toEqual([]);
  });
});

describe('named colour detection: one faulty case per form', () => {
  it('in a style sheet', () => {
    expect(namedColoursInCss('a { color: white; }\n.b { border: 1px solid RebeccaPurple; }')).toHaveLength(2);
    expect(namedColoursInCss('a { white-space: nowrap; color: transparent; fill: currentColor; content: "red"; }')).toEqual([]);
    expect(namedColoursInCss('.red:hover { color: var(--red-ink); } /* color: red */')).toEqual([]);
  });

  it('in a style attribute', () => {
    expect(namedColoursInAttributes('<span style="left:4px;color: Navy">a</span>')).toHaveLength(1);
    expect(namedColoursInAttributes("<b style='background:var(--surface);border:1px solid black'>b</b>")).toHaveLength(1);
    expect(namedColoursInAttributes('<span aria-label="Rose" style="display:contents">red</span>')).toEqual([]);
  });

  it('in the fill, stroke, stop-color and color attributes of SVG', () => {
    expect(namedColoursInAttributes('<path fill="tomato"/>')).toEqual(['fill="tomato"']);
    expect(namedColoursInAttributes('<path stroke="gray" stroke-width="2"/>')).toEqual(['stroke="gray"']);
    expect(namedColoursInAttributes('<stop offset="0" stop-color="gold"/>')).toEqual(['stop-color="gold"']);
    expect(namedColoursInAttributes('<svg color="teal"></svg>')).toEqual(['color="teal"']);
    expect(namedColoursInAttributes('<path fill="none" stroke="currentColor" color="var(--text)"/>')).toEqual([]);
  });

  it('in the colour properties of TS and TSX style objects', () => {
    expect(namedColoursInObjects("const s = { color: 'red' };")).toEqual(["color: 'red'"]);
    expect(namedColoursInObjects('<Edge style={{ stroke: "gray" }} />')).toEqual(["stroke: 'gray'"]);
    expect(namedColoursInObjects("const e = { markerEnd: { type: 'arrow', color: 'black' } };")).toEqual(["color: 'black'"]);
    expect(namedColoursInObjects("const s = { backgroundColor: `white` };")).toHaveLength(1);
    expect(namedColoursInObjects("const s = { color: 'var(--text)', label: 'red', fill: 'transparent' };")).toEqual([]);
  });
});

describe('tokens are declared in tokens.css only (spec 9.2)', () => {
  const tokenNames = [...rootTokens.keys()];

  it.each(CSS_SOURCES.map((f) => [f]))('%s assigns no custom property to another one', (relative) => {
    expect(tokenAliases(readSource(relative))).toEqual([]);
  });

  it.each(SOURCES.map((f) => [f]))('%s redefines no token of tokens.css', (relative) => {
    expect(redefinedTokens(readSource(relative), tokenNames, /\.tsx?$/.test(relative))).toEqual([]);
  });

  it('detects a token assigned to another one', () => {
    expect(tokenAliases('body { --text: var(--accent); color: var(--text); }')).toEqual(['--text']);
    expect(tokenAliases('/* --x: var(--y) */ body { color: var(--text); }')).toEqual([]);
  });

  it('detects a token redefined in a source, not its use nor a longer name', () => {
    const names = ['--surface', '--text'];
    expect(redefinedTokens('.panel { --surface: #FFF; color: var(--text); }', names)).toEqual(['--surface']);
    expect(redefinedTokens("const s = { '--text': 'var(--accent)' };", names, true)).toEqual(['--text']);
    expect(redefinedTokens("el.style.setProperty('--x', '1'); const c = 'var(--text)';", names)).toEqual([]);
    expect(redefinedTokens('/* --text: red */ .a { --surface-2: 1px; background: var(--surface); }', names)).toEqual([]);
  });
});
