// sheet.test.ts
// The sheet panel as its style sheet draws it: the width of the dock, the state in the colour of its code, the saving refused in red, the clarification, the procedure, the inputs from elsewhere (annex B 4, spec 9.2, 9.5).

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const read = (name: string) => readFileSync(join(import.meta.dirname, name), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
const css = readdirSync(import.meta.dirname).filter((name) => name.endsWith('.css') && name !== 'tokens.css').map(read).join('\n');

/** A property of the rules whose selector list holds the selector given, the last one winning. */
function rule(selector: string, property: string): string | undefined {
  let found: string | undefined;
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = (match[1] ?? '').split(',').map((part) => part.trim().replace(/\s+/g, ' '));
    if (!selectors.includes(selector)) continue;
    for (const declaration of (match[2] ?? '').split(';')) {
      const colon = declaration.indexOf(':');
      if (colon > 0 && declaration.slice(0, colon).trim() === property) found = declaration.slice(colon + 1).trim();
    }
  }
  return found;
}

const rules = (pairs: readonly [string, string][]) => pairs.map(([selector, property]) => rule(selector, property));

describe('the dock and the head (B-2.1-39, B-4-10, B-4-12, B-4-42, B-8.1-35)', () => {
  it('a side panel fills its frame, whose width is chosen by its handle and kept apart from the canvas (spec 3.0 section 15.2)', () => {
    expect(rules([['.side-panel', 'flex'], ['.side-dock', 'flex'], ['.side-dock', 'z-index'], ['.side-dock-panel[hidden]', 'display'], ['.side-dock-handle', 'cursor']]))
      .toEqual(['1', 'none', '8', 'none', 'col-resize']);
  });

  it('each state of a sheet has its class, named by its code, in the colours of the gauge of the map', () => {
    expect(rules([['.state--draft', 'color'], ['.state--validated', 'color'], ['.state--needs_review', 'color'], ['.state--empty', 'color']]))
      .toEqual(['var(--warning-text)', 'var(--success-text)', 'var(--danger)', 'var(--text-muted)']);
    expect(rules([['.sheet-completeness-bar span', 'background'], ['.sheet-completeness-bar--validated span', 'background'], ['.sheet-completeness-bar--needs_review span', 'background']]))
      .toEqual(['var(--warning-graphic)', 'var(--success)', 'var(--danger)']);
  });

  it('a notification stands left of a side panel open, in the bottom right corner of the canvas, never over its foot nor over the zoom and the mini map (spec 9.5)', () => {
    expect(rules([['body:has(.side-dock) .toasts', 'right'], ['body:has(.side-dock) .toasts', 'bottom']])).toEqual(['calc(var(--dock-room) + 16px)', '174px']);
    expect(rules([['.toasts', 'right'], ['.toasts', 'bottom']])).toEqual(['16px', '16px']);
  });

  it('« Non enregistré » in red; the clarification asked on the warning colours', () => {
    expect(rules([['.sheet-saving--failed', 'color'], ['.sheet-clarification', 'background'], ['.sheet-clarification', 'border-left']]))
      .toEqual(['var(--danger)', 'var(--warning-soft)', '3px solid var(--warning)']);
  });
});

describe('the form (B-4-17, B-4-33, B-4-38, B-4-65, AR-59, AR-89)', () => {
  it('the mode chosen outlined in --accent on the surface', () => {
    expect(rules([['.seg [aria-checked="true"]', 'border-color'], ['.seg [aria-checked="true"]', 'background']])).toEqual(['var(--accent)', 'var(--surface)']);
  });

  it('the buttons of a line hidden until the line is hovered or holds the focus; the line aimed by a drag ringed', () => {
    expect(rules([['.procedure-buttons', 'opacity'], ['.procedure-line:hover .procedure-buttons', 'opacity'], ['.procedure-line:focus-within .procedure-buttons', 'opacity'],
      ['.procedure-line.is-target', 'border-color'], ['.procedure-handle', 'cursor']])).toEqual(['0', '1', '1', 'var(--accent)', 'grab']);
  });

  it('a check in green: its border, its number white on --success-text', () => {
    expect(rules([['.procedure-line.is-check', 'border-left'], ['.procedure-line.is-check .procedure-number', 'background'], ['.procedure-line.is-check .procedure-number', 'color']]))
      .toEqual(['3px solid var(--success)', 'var(--success-text)', 'var(--surface)']);
  });

  it('an input from elsewhere on the tokens of AR-26, its producer in their ink', () => {
    expect(rules([['.entry--other', 'background'], ['.entry--other .from', 'color']])).toEqual(['var(--other-input-bg)', 'var(--other-input-border)']);
  });
});

describe('the durations (spec 9.5, S-9.5-01)', () => {
  it('in tabular figures, typed in a field of numbers or read in a sheet and in the remainder', () => {
    expect(rules([['input[type="number"]', 'font-variant-numeric'], ['.duration-value', 'font-variant-numeric']])).toEqual(['tabular-nums', 'tabular-nums']);
  });
});
