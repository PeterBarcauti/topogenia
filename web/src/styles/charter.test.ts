// charter.test.ts
// The charter as the components wear it: colours, type, radii, shadows, buttons, fields, focus, dialogs, menus, notifications, motion and small screens, read in the style sheets (annex B 6, spec 9.2 to 9.5).

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const read = (name: string) => readFileSync(join(import.meta.dirname, name), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
const SHEETS = readdirSync(import.meta.dirname).filter((name) => name.endsWith('.css') && name !== 'tokens.css');
const css = SHEETS.map(read).join('\n');
const tokens = read('tokens.css');

/** The declarations of every rule whose selector list holds the selector given, in every sheet but tokens.css, the last one winning. */
function declarations(selector: string): Map<string, string> {
  const found = new Map<string, string>();
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = (match[1] ?? '').split(',').map((part) => part.trim().replace(/\s+/g, ' '));
    if (!selectors.includes(selector)) continue;
    for (const declaration of (match[2] ?? '').split(';')) {
      const colon = declaration.indexOf(':');
      if (colon > 0) found.set(declaration.slice(0, colon).trim(), declaration.slice(colon + 1).trim());
    }
  }
  return found;
}

const rule = (selector: string, property: string): string | undefined => declarations(selector).get(property);
const rules = (pairs: readonly [string, string][]) => pairs.map(([selector, property]) => rule(selector, property));
const token = (name: string) => new RegExp(`--${name}:\\s*([^;]+);`).exec(tokens)?.[1]?.trim();

describe('colours of the components (B-6-01 to B-6-12, B-6-22 to B-6-24, B-6-28 to B-6-30)', () => {
  it('the page on --bg-app, the panels on --surface, the text in --text and --text-muted (B-6-01 to B-6-04)', () => {
    expect(rules([['body', 'background'], ['.home', 'background']])).toEqual(['var(--bg-app)', 'var(--bg-app)']);
    expect(rules([['.dialog', 'background'], ['.menu', 'background'], ['.page-error', 'background'], ['.project', 'background'], ['.app-header', 'background']]))
      .toEqual(Array(5).fill('var(--surface)'));
    expect(rules([['body', 'color'], ['.muted', 'color'], ['.field-hint', 'color'], ['.menu-shortcut', 'color']]))
      .toEqual(['var(--text)', 'var(--text-muted)', 'var(--text-muted)', 'var(--text-muted)']);
  });

  it('the main button in --accent, --accent-strong on hover; the hover of the other buttons and the menus in --hover (B-6-05 to B-6-07, B-6-22)', () => {
    expect(rules([['.btn--primary', 'background'], ['.btn--primary:hover', 'background'], ['.btn:hover', 'background'], ['.menu-item:hover', 'background']]))
      .toEqual(['var(--accent)', 'var(--accent-strong)', 'var(--hover)', 'var(--hover)']);
  });

  it('warning, danger, error background, success: their tokens (B-6-08, B-6-10, B-6-11, B-6-23, B-6-28)', () => {
    expect(rules([['.map-banner--warning', 'background'], ['.map-banner--warning', 'color'], ['.step-warning', 'background']]))
      .toEqual(['var(--warning-soft)', 'var(--warning-text)', 'var(--warning-graphic)']);
    expect(rules([['.btn--danger', 'color'], ['.message-error', 'color'], ['.field-error', 'color'], ['.btn--danger:hover', 'background']]))
      .toEqual(['var(--danger)', 'var(--danger)', 'var(--danger)', 'var(--danger-soft)']);
    expect(rules([['.toast--success .toast-icon', 'background'], ['.message-success', 'color']])).toEqual(['var(--success)', 'var(--success-text)']);
  });

  it('borders in --border, the fields in --field-border (B-6-12, B-6-42)', () => {
    expect(rules([['.menu', 'border'], ['.btn--secondary', 'border'], ['.project', 'border'], ['.field-control', 'border']]))
      .toEqual(['1px solid var(--border)', '1px solid var(--border)', '1px solid var(--border)', '1px solid var(--field-border)']);
  });

  it('a notification on --toast-bg with an icon of its state (B-6-24)', () => {
    expect(rules([['.toast', 'background'], ['.toast', 'color'], ['.toast--info .toast-icon', 'background'], ['.toast--error .toast-icon', 'background']]))
      .toEqual(['var(--toast-bg)', 'var(--toast-fg)', 'var(--accent)', 'var(--danger)']);
  });

  it('initials on their mint pastel, the codes on the background of the table headers (B-6-29, B-6-30)', () => {
    expect(rules([['.initials', 'background'], ['.initials', 'color'], ['.avatar', 'background'], ['.avatar', 'color'], ['.chip-code', 'background']]))
      .toEqual(['var(--initials-bg)', 'var(--initials-fg)', 'var(--initials-bg)', 'var(--initials-fg)', 'var(--table-header-bg)']);
  });
});

describe('type (B-6-16 to B-6-21)', () => {
  it('Inter embedded, then Segoe UI Variable and system-ui, at the base size (B-6-16, B-6-17)', () => {
    expect(rules([['body', 'font-family'], ['body', 'font-size']])).toEqual(['var(--font-sans)', 'var(--font-size-base)']);
    expect(token('font-sans')).toBe("'Inter Variable', 'Segoe UI Variable', system-ui, sans-serif");
    expect(readFileSync(join(import.meta.dirname, '../main.tsx'), 'utf8')).toMatch(/import '@fontsource-variable\/inter';/);
  });

  it('h1 and h2 in the sizes of the charter, semi-bold (B-6-18, B-6-19)', () => {
    expect(rules([['h1', 'font-size'], ['h1', 'font-weight'], ['h2', 'font-size'], ['h2', 'font-weight']])).toEqual(['var(--font-size-h1)', '600', 'var(--font-size-h2)', '600']);
  });

  it('the codes of the pages at fixed pitch with tabular figures (B-6-20)', () => {
    expect(rules([['code', 'font-family'], ['code', 'font-variant-numeric'], ['kbd', 'font-family'], ['.chip-code', 'font-family']])).toEqual(['var(--font-mono)', 'tabular-nums', 'var(--font-mono)', 'var(--font-mono)']);
  });

  it('the names of the zones of the ribbon in small muted type (B-6-21)', () => {
    expect(rules([['.toolbar-caption', 'font-size'], ['.toolbar-caption', 'color']])).toEqual(['var(--font-size-micro)', 'var(--text-muted)']);
  });
});

describe('shapes of the components (B-6-13, B-6-14, B-6-39 to B-6-47)', () => {
  it('shadows: --shadow-step at rest, --shadow-raised for panels and dialogs, --shadow-menu for the menus (B-6-13)', () => {
    expect(rules([['.project', 'box-shadow'], ['.dialog', 'box-shadow'], ['.toast', 'box-shadow'], ['.menu', 'box-shadow']]))
      .toEqual(['var(--shadow-step)', 'var(--shadow-raised)', 'var(--shadow-raised)', 'var(--shadow-menu)']);
  });

  it('the side panels: 440 px at first, white, the raised shadow, a sticky header and foot (spec 9.5, B-2.9-24, spec 3.0 section 15.2)', () => {
    expect(rules([['.side-panel', 'background'], ['.side-panel', 'box-shadow'], ['.side-panel-head', 'position'], ['.side-panel-foot', 'position']]))
      .toEqual(['var(--surface)', 'var(--shadow-raised)', 'sticky', 'sticky']);
    expect(token('side-panel-width')).toBe('440px');
    expect(token('dock-room')).toBe('var(--side-panel-width)');
  });

  it('radii: 8 for the controls, 10 for the large button, pills at 999 px (B-6-14, B-6-40, B-6-41)', () => {
    expect(rules([['.btn', 'border-radius'], ['.field-control', 'border-radius'], ['.menu', 'border-radius'], ['.btn--lg', 'border-radius']]))
      .toEqual(['var(--radius-ui)', 'var(--radius-ui)', 'var(--radius-ui)', 'var(--radius-step)']);
    expect(token('radius-pill')).toBe('999px');
    expect(rules([['.tag', 'border-radius'], ['.avatar', 'border-radius'], ['.initials', 'border-radius'], ['.pl-button', 'border-radius']])).toEqual(Array(4).fill('var(--radius-pill)'));
  });

  it('the kinds of buttons: plain, secondary, main, danger, tool with its pressed and checked states, icon, link (B-6-39)', () => {
    for (const selector of ['.btn', '.btn--secondary', '.btn--primary', '.btn--danger', '.toolbar-button', '.icon-button', '.toast-action', '.crumbs a'])
      expect(declarations(selector).size, selector).toBeGreaterThan(0);
    expect(rule('.toolbar-button[aria-pressed="true"]', 'background')).toBe('var(--accent-soft)');
    expect(rule('.toolbar-button[aria-checked="true"]', 'background')).toBe('var(--accent-soft)');
  });

  it('the focus: an outline in --accent and the ring of 3 px of the charter (B-6-43, B-6-44)', () => {
    expect(rules([[':focus-visible', 'outline'], [':focus-visible', 'box-shadow']])).toEqual(['var(--focus-outline)', 'var(--focus-ring)']);
    expect([token('focus-outline'), token('focus-ring')]).toEqual(['2px solid var(--accent)', '0 0 0 3px rgba(91, 91, 214, .35)']);
  });

  it('dialogs over the veil of tokens.css, 520 or 1100 px wide; menus of 240 px (B-6-45 to B-6-47)', () => {
    expect([rule('.overlay', 'background'), token('overlay')]).toEqual(['var(--overlay)', 'rgba(16, 24, 40, .25)']);
    expect([rule('.dialog', 'width'), rule('.dialog--wide', 'width'), token('dialog-width'), token('dialog-wide-width')]).toEqual(['var(--dialog-width)', 'var(--dialog-wide-width)', '520px', '1100px']);
    expect(rule('.menu', 'width')).toBe('var(--menu-width)');
  });
});

describe('theme, motion and small screens (B-6-48, B-6-50, B-6-51, B-6-53 to B-6-56)', () => {
  it('a light theme only: no style sheet asks for the dark scheme (B-6-48)', () => {
    expect([...SHEETS, 'tokens.css'].filter((name) => read(name).includes('prefers-color-scheme'))).toEqual([]);
  });

  it('a notification fades in with a slight rise; the « + » fade like the handles and the pictograms (B-6-50, B-6-51)', () => {
    expect(rule('.toast', 'animation')).toBe('rise var(--duration-base) ease-out');
    expect(/@keyframes rise \{ from \{ opacity: 0; transform: translateY\(4px\); \} \}/.test(css)).toBe(true);
    expect([rule('.quick-add', 'transition'), rule('.lane-add', 'transition')]).toEqual(['opacity var(--duration-fast)', 'opacity var(--duration-fast)']);
  });

  it('every animation and transition lasts a duration token, which prefers-reduced-motion sets to 0 (B-6-53)', () => {
    const timed = [...css.matchAll(/(?:^|[;{\s])(animation|transition)\s*:\s*([^;}]+)/g)].map((match) => (match[2] ?? '').trim());
    expect(timed.length).toBeGreaterThan(5);
    expect(timed.filter((value) => /\d(?:\.\d+)?m?s\b/.test(value.replace(/var\([^)]*\)/g, '')) || !value.includes('var(--duration-'))).toEqual([]);
  });

  it('the home page on one column below 900 px, a file form below 600 px; nothing for the editor on a small screen (B-6-54 to B-6-56)', () => {
    expect(css).toMatch(/@media \(max-width: 899px\) \{[^@]*\.home-cols \{ grid-template-columns: 1fr; \}/);
    expect(css).toMatch(/@media \(max-width: 599px\) \{\s*\.form-row \{ grid-template-columns: 1fr; \}/);
    expect(read('map.css')).not.toContain('@media');
  });
});
