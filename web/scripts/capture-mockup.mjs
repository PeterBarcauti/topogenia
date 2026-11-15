// capture-mockup.mjs
// Opens the generated mockup in Chromium (Playwright), writes one PNG per section, checks the embedded font, contrast, step and arrow label layout, and that the page is one file.

import { mkdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { outsideResources } from './mockup/self-contained.mjs';

const MOCKUP = new URL('../../docs/recette/maquette/maquette.html', import.meta.url);
const OUT_DIR = new URL('../../docs/recette/maquette/captures/', import.meta.url);
const MINIMUMS = { text: 4.5, graphic: 3 };
const SCREEN = { width: 1366, height: 768 };
mkdirSync(OUT_DIR, { recursive: true });
const out = (name) => fileURLToPath(new URL(`${name}.png`, OUT_DIR));

/** Runs in the page: colour helpers (WCAG relative luminance and ratio, compositing, ancestors' backgrounds). */
function colourTools() {
  const parse = (value) => {
    const m = /rgba?\(([^)]+)\)/.exec(value ?? '');
    if (!m) return null;
    const [r, g, b, a = 1] = m[1].split(/[\s,/]+/).filter(Boolean).map(Number);
    return { r, g, b, a };
  };
  const over = (top, under) => ({
    r: top.r * top.a + under.r * (1 - top.a),
    g: top.g * top.a + under.g * (1 - top.a),
    b: top.b * top.a + under.b * (1 - top.a),
    a: 1,
  });
  const channel = (c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  const lum = (c) => 0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
  const ratio = (x, y) => {
    const [l1, l2] = [lum(x), lum(y)].sort((p, q) => q - p);
    return (l1 + 0.05) / (l2 + 0.05);
  };
  const hex = (c) => '#' + [c.r, c.g, c.b].map((v) => Math.round(v).toString(16).padStart(2, '0')).join('').toUpperCase();

  /** Possible opaque colours behind the content of an element, from its ancestors (used for graphic objects). */
  const backdrops = (el) => {
    if (!el || el.nodeType !== 1) return [{ r: 255, g: 255, b: 255, a: 1 }];
    const cs = getComputedStyle(el);
    const base = parse(cs.backgroundColor) ?? { r: 0, g: 0, b: 0, a: 0 };
    const under = base.a >= 1 ? [base] : backdrops(el.parentElement).map((u) => over(base, u));
    const stops = cs.backgroundImage === 'none' ? [] : [...cs.backgroundImage.matchAll(/rgba?\([^)]+\)/g)].map((m) => parse(m[0]));
    return [...under, ...stops.flatMap((s) => (s.a >= 1 ? [s] : under.map((u) => over(s, u))))];
  };

  /** Worst ratio of a colour shown at the given opacity against a list of opaque backdrops. */
  const worstOf = (colour, opacity, under) => {
    let worst = null;
    for (const bg of under) {
      const shown = over({ ...colour, a: colour.a * opacity }, bg);
      const r = ratio(shown, bg);
      if (!worst || r < worst.ratio) worst = { ratio: r, fg: hex(shown), bg: hex(bg) };
    }
    return worst;
  };
  return { parse, over, hex, backdrops, worstOf };
}

/** Runs in the page: element helpers (name, visibility, clipping, covering, text rectangles). */
function pageTools() {
  const describe = (el) => {
    const raw = typeof el.className === 'string' ? el.className : (el.className?.baseVal ?? '');
    const cls = raw.trim() ? '.' + raw.trim().split(/\s+/).join('.') : '';
    const section = el.closest('section[id]')?.id ?? '';
    return `${section ? section + ' > ' : ''}${el.tagName.toLowerCase()}${cls}`;
  };
  /** Product of the opacities of the element and its ancestors, 0 when it is not rendered. */
  const visible = (el) => {
    const rect = el.getBoundingClientRect();
    // a straight SVG line has one null dimension: only an element empty both ways is not rendered
    const empty = el instanceof SVGGeometryElement ? rect.width === 0 && rect.height === 0 : rect.width === 0 || rect.height === 0;
    if (empty || getComputedStyle(el).visibility === 'hidden' || getComputedStyle(el).display === 'none') return 0;
    let opacity = 1;
    for (let a = el; a; a = a.parentElement) opacity *= Number(getComputedStyle(a).opacity);
    return opacity;
  };
  const disabled = (el) => Boolean(el.closest(':disabled, [aria-disabled="true"], .is-disabled'));
  /** Part of a rectangle left visible by the element and the ancestors that clip their content, null when none. */
  const clipped = (el, r) => {
    let [left, top, right, bottom] = [r.left, r.top, r.right, r.bottom];
    for (let a = el; a && a !== document.documentElement; a = a.parentElement) {
      const cs = getComputedStyle(a);
      if (cs.overflowX === 'visible' && cs.overflowY === 'visible' && cs.clipPath === 'none') continue;
      const b = a.getBoundingClientRect();
      [left, top, right, bottom] = [Math.max(left, b.left), Math.max(top, b.top), Math.min(right, b.right), Math.min(bottom, b.bottom)];
    }
    return right > left && bottom > top ? { x: left, y: top, w: right - left, h: bottom - top } : null;
  };
  // transient layers (menu, notification, tooltip) hide what lies under them and under their shadow (16 px around)
  const layers = [...document.querySelectorAll('.menu, .toast, .tooltip')].map((layer) => [layer, layer.getBoundingClientRect()]);
  const underLayer = (el, x, y) =>
    layers.some(([layer, b]) => !layer.contains(el) && x > b.left - 16 && x < b.right + 16 && y > b.top - 16 && y < b.bottom + 16);
  /** Parts of a rectangle that nothing else paints over (tested every 2 px on three rows), as rectangles. */
  const visibleParts = (el, r) => {
    const shows = (x, y) => {
      if (underLayer(el, x, y)) return false;
      const top = document.elementFromPoint(x, y);
      return !top || el.contains(top) || top.contains(el);
    };
    const rows = [r.y + 1.5, r.y + r.h / 2, r.y + r.h - 1.5];
    const parts = [];
    let start = null;
    for (let x = r.x + 1; x <= r.x + r.w - 1; x += 2) {
      const free = rows.every((y) => shows(x, y));
      if (free && start === null) start = x;
      if (!free && start !== null) parts.push([start, x]);
      if (!free) start = null;
    }
    if (start !== null) parts.push([start, r.x + r.w + 1]);
    // from 1 px before the first free column to 1 px after the last one
    return parts.map(([a, b]) => ({ x: a - 1, y: r.y, w: b - a, h: r.h }));
  };
  const textRects = (node) => {
    const range = document.createRange();
    range.selectNodeContents(node);
    return [...range.getClientRects()];
  };
  /** Content box of an element (inside border and padding), where its ::before / ::after text is drawn. */
  const contentBox = (el) => {
    const r = el.getBoundingClientRect();
    const cs = getComputedStyle(el);
    const px = (name) => parseFloat(cs[name]) || 0;
    const left = r.left + px('borderLeftWidth') + px('paddingLeft');
    const top = r.top + px('borderTopWidth') + px('paddingTop');
    const right = r.right - px('borderRightWidth') - px('paddingRight');
    const bottom = r.bottom - px('borderBottomWidth') - px('paddingBottom');
    return new DOMRect(left, top, right - left, bottom - top);
  };
  return { describe, visible, disabled, clipped, visibleParts, textRects, contentBox };
}

/** Runs in the page: contrast of the useful graphic objects (WCAG 1.4.11) against the colours next to them. */
function checkGraphics({ parse, hex, backdrops, describe, visible, disabled, worstOf }) {
  /** Distinct body colours of the lanes: an arrow, a step border, runs over both (odd and even). */
  const laneBackgrounds = (el) => {
    const scope = el.closest('.viewport') ?? document;
    const colours = [...scope.querySelectorAll('.lane-body')].map((b) => parse(getComputedStyle(b).backgroundColor));
    return colours.filter((c, i) => c && colours.findIndex((d) => d && hex(d) === hex(c)) === i);
  };
  const parentBackdrops = (el) => backdrops(el.parentElement);
  const outerAndInner = (el) => [...backdrops(el.parentElement), ...backdrops(el)];
  const cardAndLanes = (el) => [...backdrops(el), ...laneBackgrounds(el)];
  const border = (cs) => cs.borderTopColor;
  /** [selector, colour of the object, colours it must stand out from] */
  const GRAPHICS = [
    ['.step-gauge > span', (cs) => cs.backgroundColor, (el) => backdrops(el.closest('.step'))],
    ['.completeness-bar > span', (cs) => cs.backgroundColor, parentBackdrops],
    ['.step-warning', (cs) => cs.backgroundColor, parentBackdrops],
    ['.step--wait', border, cardAndLanes],
    ['.step--end', border, cardAndLanes],
    ['.input', border, outerAndInner],
    ['.pill-select', border, outerAndInner],
    ['.rbtn[aria-pressed="true"], .rbtn[aria-checked="true"]', border, outerAndInner],
    ['.seg [aria-checked="true"], .lang-switch [aria-pressed="true"]', border, outerAndInner],
    ['.menu-item.is-focus', (cs) => cs.outlineColor, outerAndInner],
    ['.check', border, parentBackdrops],
    ['.radio-dot', border, parentBackdrops],
    ['.edge', (cs) => cs.stroke, laneBackgrounds],
    ['.arrow-head', (cs) => cs.fill, laneBackgrounds],
    ['.gateway-shape polygon', (cs) => cs.stroke, (el) => [...backdrops(el.closest('.step')), ...laneBackgrounds(el)]],
  ];
  const list = [];
  const kinds = new Map();
  for (const [selector, colourOf, under] of GRAPHICS) {
    kinds.set(selector, 0);
    for (const el of document.querySelectorAll(selector)) {
      if (disabled(el)) continue;
      // the arrow head lives in <defs> and is not laid out itself: it is drawn at the end of each arrow
      const opacity = el.closest('defs') ? 1 : visible(el);
      if (opacity === 0) continue;
      const colour = parse(colourOf(getComputedStyle(el)));
      const bgs = under(el).filter(Boolean);
      if (!colour || bgs.length === 0) continue;
      list.push({ element: describe(el), text: selector, ...worstOf(colour, opacity, bgs) });
      kinds.set(selector, kinds.get(selector) + 1);
    }
  }
  return { list, unmatched: [...kinds].filter(([, n]) => n === 0).map(([selector]) => selector) };
}

/**
 * Runs in the page: every visible text (own text nodes, ::before / ::after whose content is a text, SVG text), with
 * its colour, its opacity and the rectangles it covers, clipped by the ancestors, without the parts painted over.
 */
function collectTexts({ parse, describe, visible, disabled, clipped, visibleParts, textRects, contentBox }) {
  const items = [];
  const push = (el, which, text, fg, opacity, raw) => {
    const inside = raw.map((r) => clipped(el, r)).filter((r) => r && r.w >= 3 && r.h >= 3);
    const rects = inside.flatMap((r) => visibleParts(el, r)).filter((r) => r.w >= 3);
    if (fg && text && rects.length > 0) items.push({ element: describe(el) + which, text: text.slice(0, 40), fg, opacity, rects });
  };
  for (const el of document.body.querySelectorAll('*')) {
    if (disabled(el)) continue;
    const isSvgText = el instanceof SVGTextElement;
    const nodes = isSvgText ? [] : [...el.childNodes].filter((n) => n.nodeType === 3 && n.textContent.trim());
    const pseudo = ['::before', '::after']
      .map((which) => ({ which, ps: getComputedStyle(el, which) }))
      .filter(({ ps }) => /^["'].*\S.*["']$/.test(ps.content));
    if (!isSvgText && nodes.length === 0 && pseudo.length === 0) continue;
    const opacity = visible(el);
    if (opacity === 0) continue;
    const cs = getComputedStyle(el);
    if (isSvgText) push(el, '', el.textContent.trim(), parse(cs.fill), opacity, [el.getBoundingClientRect()]);
    if (nodes.length > 0) push(el, '', nodes.map((n) => n.textContent).join('').trim(), parse(cs.color), opacity, nodes.flatMap(textRects));
    for (const { which, ps } of pseudo) push(el, which, ps.content.slice(1, -1), parse(ps.color), opacity * Number(ps.opacity), [contentBox(el)]);
  }
  return items;
}

/**
 * Runs in the page: real background of each text, read in a capture of the page where every text is transparent
 * (what is painted under the text: backgrounds, siblings, pseudo-elements, gradients, shadows), worst pixel kept.
 */
async function textContrasts({ worstOf }, url, items) {
  const image = new Image();
  image.src = url;
  await image.decode();
  const canvas = document.createElement('canvas');
  [canvas.width, canvas.height] = [image.naturalWidth, image.naturalHeight];
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  ctx.drawImage(image, 0, 0);
  const results = [];
  for (const { fg, opacity, rects, ...item } of items) {
    const seen = new Set();
    for (const r of rects) {
      // one pixel in from each side: the antialiased edge belongs to the neighbouring box
      const [x0, y0] = [Math.ceil(r.x) + 1, Math.ceil(r.y) + 1];
      const [x1, y1] = [Math.min(canvas.width, Math.floor(r.x + r.w) - 1), Math.min(canvas.height, Math.floor(r.y + r.h) - 1)];
      if (x1 <= x0 || y1 <= y0) continue;
      const data = ctx.getImageData(x0, y0, x1 - x0, y1 - y0).data;
      for (let i = 0; i < data.length; i += 4) seen.add((data[i] << 16) | (data[i + 1] << 8) | data[i + 2]);
    }
    const under = [...seen].map((k) => ({ r: (k >> 16) & 255, g: (k >> 8) & 255, b: k & 255, a: 1 }));
    if (under.length > 0) results.push({ ...item, ...worstOf(fg, opacity, under) });
  }
  return results;
}

/** Runs in the page: true when two rectangles share some area. */
function meets(a, b) {
  return a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
}

/**
 * Runs in the page: step labels hold 3 lines at most, inside the card (1 px at least from the border), clear of the
 * pictograms (annex B 2.10) and 2 px at least above the top of the sheet gauge (spec 9.4).
 */
function checkStepLabels({ describe, visible, textRects }) {
  const failures = [];
  for (const label of document.querySelectorAll('.step > .step-label')) {
    const step = label.parentElement;
    if (step.querySelector('.gateway-shape') || visible(step) === 0) continue;
    const cs = getComputedStyle(step);
    const card = step.getBoundingClientRect();
    const [top, bottom] = [card.top + Math.max(parseFloat(cs.borderTopWidth), 1), card.bottom - Math.max(parseFloat(cs.borderBottomWidth), 1)];
    const box = label.getBoundingClientRect();
    const lines = Math.round(box.height / parseFloat(getComputedStyle(label).lineHeight));
    const fail = (problem) => failures.push({ element: describe(step), text: label.textContent.slice(0, 40), problem });
    if (lines > 3) fail(`label of ${lines} lines`);
    if (box.top < top + 1 || box.bottom > bottom - 1) fail('label touching or crossing the border');
    const pictos = step.querySelector('.step-pictos')?.getBoundingClientRect();
    const shown = textRects(label).filter((t) => t.top < box.bottom);
    if (pictos && shown.some((t) => meets(t, pictos))) fail('label running under the pictograms');
    const gauge = step.querySelector('.step-gauge');
    if (gauge && visible(gauge) > 0 && gauge.getBoundingClientRect().top - box.bottom < 2) fail('label less than 2 px above the gauge');
  }
  return failures;
}

/**
 * Runs in the page: arrow labels cover no step, no lane separator (spec 9.7, annex B 3.4 rule 6), no other arrow
 * label and no gateway label (AR-58).
 */
function checkArrowLabels({ describe }) {
  const failures = [];
  const labels = [...document.querySelectorAll('.viewport .edge-label')];
  labels.forEach((label, i) => {
    const scope = label.closest('.viewport');
    const box = label.getBoundingClientRect();
    const bodies = [...scope.querySelectorAll('.lane-body')].map((b) => b.getBoundingClientRect());
    const separators = bodies.flatMap((b, n) => [
      ...(n === 0 ? [new DOMRect(b.left, b.top, b.width, 1)] : []),
      new DOMRect(b.left, b.bottom - 1, b.width, 1),
    ]);
    const fail = (problem) => failures.push({ element: describe(label), text: label.textContent.slice(0, 40), problem });
    if (separators.some((s) => meets(box, s))) fail('arrow label across a lane separator');
    if ([...scope.querySelectorAll('.step')].some((s) => meets(box, s.getBoundingClientRect()))) fail('arrow label over a step');
    const others = labels.slice(i + 1).filter((o) => o.closest('.viewport') === scope);
    if (others.some((o) => meets(box, o.getBoundingClientRect()))) fail('arrow label over another arrow label');
    const gatewayLabels = [...scope.querySelectorAll('.step > .step-label')].filter((l) => l.parentElement.querySelector('.gateway-shape'));
    if (gatewayLabels.some((l) => meets(box, l.getBoundingClientRect()))) fail('arrow label over a gateway label');
  });
  return failures;
}

/** Runs in the page: every layout check. */
function checkLayout(tools) {
  return [...checkStepLabels(tools), ...checkArrowLabels(tools)];
}

const PAGE_SCRIPT =
  [colourTools, pageTools, checkGraphics, collectTexts, textContrasts, meets, checkStepLabels, checkArrowLabels, checkLayout].join('\n') +
  '\nwindow.mockupChecks = { colourTools, pageTools, checkGraphics, collectTexts, textContrasts, checkLayout };';
/** Every text made transparent (icons drawn in currentColor too): the capture then shows what lies under the texts. */
const HIDE_TEXT =
  '*, *::before, *::after { color: transparent !important; -webkit-text-fill-color: transparent !important; ' +
  'text-shadow: none !important; text-decoration-color: transparent !important; } text, tspan { fill: transparent !important; }';

const summary = (list, minimum) => ({
  checked: list.length,
  lowest: list.reduce((low, r) => (!low || r.ratio < low.ratio ? r : low), null),
  failures: list.filter((r) => r.ratio < minimum),
});

/** Runs every check on a page whose viewport shows all of it. */
async function runChecks(page) {
  await page.addScriptTag({ content: PAGE_SCRIPT });
  const inPage = (name, ...args) =>
    page.evaluate(
      ([fn, rest]) => {
        const checks = window.mockupChecks;
        return checks[fn]({ ...checks.colourTools(), ...checks.pageTools() }, ...rest);
      },
      [name, args],
    );
  const graphics = await inPage('checkGraphics');
  const layout = await inPage('checkLayout');
  const items = await inPage('collectTexts');
  const hide = await page.addStyleTag({ content: HIDE_TEXT });
  const shot = await page.screenshot({ animations: 'disabled' });
  await hide.evaluate((node) => node.remove());
  const texts = await inPage('textContrasts', `data:image/png;base64,${shot.toString('base64')}`, items);
  return {
    text: summary(texts, MINIMUMS.text),
    graphics: summary(graphics.list, MINIMUMS.graphic),
    unmatched: graphics.unmatched,
    layout,
  };
}

/**
 * Faulty page of the self-test: each item must be reported. Pale text in ::before, ::after, SVG text and ordinary
 * text; dark text on a dark sibling painted under it (its ancestors are white); an input with a pale border; an arrow
 * too pale on a lane; an arrow label across a lane separator, one over another arrow label, one over a gateway label;
 * a step label overflowing its card; a step label touching its gauge. Colours are written here only, never in the
 * mockup sources.
 */
const SELF_TEST_PAGE = `<!doctype html><html><body style="margin:0;background:#FFFFFF">
<p class="a">x</p><p class="b">y</p><p class="c" style="color:#B5B5B5">texte ordinaire trop pâle</p>
<div style="position:relative;height:30px"><div style="position:absolute;inset:0;background:#333333"></div>
<span class="d" style="position:relative;color:#222222">sombre sur un voisin sombre</span></div>
<svg width="80" height="20"><text x="0" y="15" fill="#DDDDDD">pale</text></svg>
<div class="input" style="width:80px;height:30px;border:1px solid #EEEEEE"></div>
<div class="viewport" style="position:relative;height:130px">
<div class="lane-body" style="position:absolute;left:0;top:0;width:300px;height:40px;background:#F5F5F5;border-bottom:1px solid #E8E8EC"></div>
<svg width="80" height="20" style="position:absolute;top:10px"><path class="edge" d="M0 10 L80 10" style="stroke:#DDDDDD;stroke-width:2"/></svg>
<span class="edge-label" style="position:absolute;left:120px;top:30px;background:#FFFFFF">1</span>
<div class="step" style="position:absolute;left:0;top:70px;width:120px;height:40px;border:1px solid #707070">
<span class="step-label" style="display:block;line-height:15px">un libellé bien trop long pour tenir dans la carte</span></div>
<div class="step gauged" style="position:absolute;left:400px;top:70px;width:170px;height:40px;border:1px solid #707070">
<span class="step-label" style="position:absolute;left:4px;top:14px;line-height:15px">touche la jauge</span>
<span class="step-gauge" style="position:absolute;left:0;right:0;bottom:0;height:13px"></span></div>
<span class="edge-label first" style="position:absolute;left:620px;top:10px;background:#FFFFFF">a</span>
<span class="edge-label over-label" style="position:absolute;left:624px;top:14px;background:#FFFFFF">b</span>
<div class="step gateway" style="position:absolute;left:700px;top:0;width:60px;height:20px"><svg class="gateway-shape" width="60" height="20"></svg>
<span class="step-label" style="position:absolute;left:0;top:24px;line-height:15px">losange</span></div>
<span class="edge-label over-gateway" style="position:absolute;left:710px;top:28px;background:#FFFFFF">c</span></div>
<style>.a::before{content:'avant';color:#EEEEEE}.b::after{content:'après';color:#EEEEEE}</style></body></html>`;
const SELF_TEST_EXPECTED = {
  text: ['p.a::before', 'p.b::after', 'p.c', 'span.d', 'text'],
  graphics: ['div.input', 'path.edge'],
  // [element, problem]
  layout: [
    ['span.edge-label', 'arrow label across a lane separator'],
    ['div.step', 'label touching or crossing the border'],
    ['div.step.gauged', 'label less than 2 px above the gauge'],
    ['span.edge-label.first', 'arrow label over another arrow label'],
    ['span.edge-label.over-gateway', 'arrow label over a gateway label'],
  ],
};
/** Witness page of the font check: it uses the family of --font-sans but declares no @font-face. */
const NO_FONT_FACE_PAGE = `<!doctype html><html><head><style>:root{--font-sans:"Inter Variable",system-ui,sans-serif}
body{font-family:var(--font-sans)}</style></head><body><div class="step"><span class="step-label">Rédiger la demande</span></div></body></html>`;
const SELF_TEST_RESOURCES = '<img src="logo.png"><a href="#haut">a</a><link href="https://x.org/a.css">' +
  '<i style="background:url(img/a.png)"></i><i style="background:url(data:image/png;base64,AA)"></i><style>@import "b.css";</style>';

/** The checks must fail on the faulty page, otherwise a green run proves nothing. */
async function selfTest(browser) {
  const probe = await browser.newPage({ viewport: SCREEN, deviceScaleFactor: 1 });
  await probe.setContent(SELF_TEST_PAGE);
  const result = await runChecks(probe);
  await probe.close();
  const missed = [
    ...SELF_TEST_EXPECTED.text.filter((e) => !result.text.failures.some((f) => f.element === e)),
    ...SELF_TEST_EXPECTED.graphics.filter((e) => !result.graphics.failures.some((f) => f.element === e)),
    ...SELF_TEST_EXPECTED.layout
      .filter(([e, problem]) => !result.layout.some((f) => f.element === e && f.problem === problem))
      .map(([e, problem]) => `${e} (${problem})`),
  ];
  const resources = outsideResources(SELF_TEST_RESOURCES);
  if (resources.length !== 4) missed.push(`resources outside the page (${resources.length} found, 4 expected)`);
  const witness = await browser.newPage({ viewport: SCREEN, deviceScaleFactor: 1 });
  await witness.setContent(NO_FONT_FACE_PAGE);
  if ((await fontProblems(witness)).length === 0) missed.push('font of --font-sans without @font-face');
  await witness.close();
  if (missed.length > 0) throw new Error(`Self-test: faulty items not reported: ${missed.join(', ')}`);
  const count = Object.values(SELF_TEST_EXPECTED).flat().length + 2;
  console.log(`Self-test: the ${count} faulty items of the test page are reported`);
}

/** Opens the mockup; any request but the page itself (network, relative or absolute path) is blocked and listed. */
async function openMockup(browser) {
  const page = await browser.newPage({ viewport: SCREEN, deviceScaleFactor: 1 });
  const requests = [];
  const self = decodeURI(MOCKUP.href);
  await page.route('**/*', (route) => {
    const url = route.request().url();
    if (decodeURI(url.split('#')[0]) === self || url.startsWith('data:')) return route.continue();
    requests.push(url);
    return route.abort();
  });
  await page.goto(MOCKUP.href, { waitUntil: 'load' });
  const problems = await fontProblems(page);
  if (problems.length > 0) throw new Error(`Inter is not loaded: ${problems.join('; ')}`);
  console.log('Font: the family of --font-sans has a loaded FontFace, the step labels are drawn with an embedded font');
  return { page, requests };
}

/**
 * Proof that the font of the charter is the one drawn, empty when it holds. document.fonts.check() is true for a family
 * that has no @font-face at all, so it proves nothing: (1) the family declared first in --font-sans has at least one
 * FontFace loaded after document.fonts.ready; (2) the fonts Chromium really uses for a step label (CDP
 * CSS.getPlatformFontsForNode) are all web fonts (isCustomFont), not a system font found under the same name.
 */
async function fontProblems(page) {
  const problems = [];
  const { family, loaded } = await page.evaluate(async () => {
    await document.fonts.ready;
    const first = getComputedStyle(document.documentElement).getPropertyValue('--font-sans').split(',')[0].trim();
    const name = first.replace(/^["']|["']$/g, '');
    const faces = [...document.fonts].filter((f) => f.family.replace(/^["']|["']$/g, '') === name && f.status === 'loaded');
    return { family: name, loaded: faces.length };
  });
  if (!family) problems.push('--font-sans is not declared');
  if (loaded === 0) problems.push(`no FontFace "${family}" loaded in document.fonts`);
  const cdp = await page.context().newCDPSession(page);
  try {
    await cdp.send('DOM.enable');
    await cdp.send('CSS.enable');
    const { root } = await cdp.send('DOM.getDocument', { depth: 0 });
    const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector: '.step > .step-label' });
    if (!nodeId) return [...problems, 'no step label in the page'];
    const { fonts } = await cdp.send('CSS.getPlatformFontsForNode', { nodeId });
    if (fonts.length === 0) problems.push('no font used for the step label');
    for (const f of fonts.filter((x) => !x.isCustomFont)) problems.push(`step label drawn with the system font "${f.familyName}"`);
  } finally {
    await cdp.detach();
  }
  return problems;
}

/** One PNG per section, and the 1366 x 768 screens alone (their content, frame border excluded). */
async function capture(page) {
  const shots = [];
  for (const element of await page.locator('[data-capture]').all()) {
    const name = await element.getAttribute('data-capture');
    await element.screenshot({ path: out(name), animations: 'disabled' });
    shots.push(name);
  }
  for (const frame of await page.locator('[data-capture-frame]').all()) {
    const name = await frame.getAttribute('data-capture-frame');
    const screen = frame.locator(':scope > .app');
    const box = await screen.boundingBox();
    if (box?.width !== SCREEN.width || box.height !== SCREEN.height) {
      throw new Error(`${name}: the screen measures ${box?.width} x ${box?.height}, not ${SCREEN.width} x ${SCREEN.height}`);
    }
    // the screen may sit at a fractional position: the clip is rounded so the image is exactly the screen size
    const at = await screen.evaluate((el) => ({ x: el.getBoundingClientRect().left + scrollX, y: el.getBoundingClientRect().top + scrollY }));
    const clip = { x: Math.round(at.x), y: Math.round(at.y), ...SCREEN };
    await page.screenshot({ path: out(name), fullPage: true, clip, animations: 'disabled' });
    const png = readFileSync(out(name));
    if (png.readUInt32BE(16) !== SCREEN.width || png.readUInt32BE(20) !== SCREEN.height) throw new Error(`${name}.png is not ${SCREEN.width} x ${SCREEN.height}`);
    shots.push(name);
  }
  await page.screenshot({ path: out('00-page-entiere'), fullPage: true, animations: 'disabled' });
  shots.push('00-page-entiere');
  console.log(`${shots.length} captures written: ${shots.sort().join(', ')}`);
}

/** Prints the results; returns false when one check fails. */
function report(result) {
  const fmt = (r) => r.toFixed(2).replace('.', ',');
  let ok = true;
  const contrast = (title, unit, minimum, { checked, lowest, failures }) => {
    console.log(`${title}: ${checked} ${unit} checked, minimum ${minimum}:1`);
    if (lowest) console.log(`  lowest ratio ${fmt(lowest.ratio)}:1 on ${lowest.element} "${lowest.text}" (${lowest.fg} on ${lowest.bg})`);
    if (failures.length === 0) return console.log('  none under the minimum');
    ok = false;
    console.error(`${failures.length} ${unit} under ${minimum}:1:`);
    for (const f of failures) console.error(`  ${fmt(f.ratio)}:1  ${f.element}  "${f.text}"  ${f.fg} on ${f.bg}`);
  };
  contrast('Text contrast (NF08), background read under the text', 'texts', MINIMUMS.text, result.text);
  contrast('Graphic objects (WCAG 1.4.11)', 'graphic objects', MINIMUMS.graphic, result.graphics);
  // a selector that matches nothing would let a check pass silently
  if (result.unmatched.length > 0) {
    ok = false;
    console.error(`Graphic selectors matching nothing: ${result.unmatched.join(', ')}`);
  }
  console.log(`Layout of step and arrow labels: ${result.layout.length === 0 ? 'no fault' : `${result.layout.length} faults`}`);
  for (const f of result.layout) console.error(`  ${f.element}  "${f.text}"  ${f.problem}`);
  return ok && result.layout.length === 0;
}

const outside = outsideResources(readFileSync(MOCKUP, 'utf8'));
if (outside.length > 0) throw new Error(`Resources outside maquette.html: ${outside.join(', ')}`);
const browser = await chromium.launch();
try {
  await selfTest(browser);
  const { page, requests } = await openMockup(browser);
  await capture(page);
  // the checks read the whole page at once: the viewport is stretched to the full height
  await page.setViewportSize({ width: SCREEN.width, height: await page.evaluate(() => document.documentElement.scrollHeight) });
  if (!report(await runChecks(page))) process.exitCode = 1;
  if (requests.length > 0) throw new Error(`Requests outside the page: ${requests.join(', ')}`);
  console.log('Self-contained: no resource outside maquette.html, no request');
} finally {
  await browser.close();
}
