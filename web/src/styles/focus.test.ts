// focus.test.ts
// The keyboard focus seen on every control that the style sheets hide until a hover: no focus stop stays invisible (NF08, WCAG 2.4.7, S-NF-11).

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const strip = (text: string) => text.replace(/\/\*[\s\S]*?\*\//g, '');
const SHEETS = readdirSync(import.meta.dirname).filter((name) => name.endsWith('.css'));
// The steps of an animation are no rules of the page: they are left out.
const css = SHEETS.map((name) => strip(readFileSync(join(import.meta.dirname, name), 'utf8'))).join('\n').replace(/@keyframes[^{]*\{(?:[^{}]*\{[^{}]*\})*[^{}]*\}/g, '');

/** Every rule as its selectors and its declarations. */
const RULES = [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({
  selectors: (match[1] ?? '').split(',').map((part) => part.trim().replace(/\s+/g, ' ')),
  body: match[2] ?? '',
}));

const hides = (body: string) => /(?:^|;)\s*(?:opacity:\s*0\s*(?:;|$)|visibility:\s*hidden)/.test(body);
const shows = (body: string) => /(?:^|;)\s*(?:opacity:\s*1\s*(?:;|$)|visibility:\s*visible)/.test(body);

/** The class a selector ends on (`.lane-heads:hover .lane-add` ends on `lane-add`). */
const lastClass = (selector: string) => /\.([\w-]+)[^.\s]*$/.exec(selector.split(' ').at(-1) ?? '')?.[1];

/**
 * What the sheets hide until a hover and that takes no focus: the handles of React Flow (out of the order of the focus),
 * the pictograms of a step (text), the margin of the lanes drawn for the exports only (no control).
 */
const NOT_FOCUSABLE = new Set(['step-handle', 'step-pictos', 'lane-body-margin']);

describe('the focus of the keyboard on what is hidden until a hover (S-NF-11, WCAG 2.4.7)', () => {
  it('every element hidden by the sheets shows when it, or a control in it, holds the focus, but those that take no focus', () => {
    const hidden = new Set(RULES.filter((rule) => hides(rule.body)).flatMap((rule) => rule.selectors.map(lastClass)).filter((name) => name !== undefined));
    const shownOnFocus = (name: string) => RULES.some((rule) => shows(rule.body) && rule.selectors.some((selector) =>
      selector.endsWith(`.${name}:focus-visible`) || (selector.includes(':focus-within') && lastClass(selector) === name)));
    expect([...hidden].filter((name) => !NOT_FOCUSABLE.has(name) && !shownOnFocus(name))).toEqual([]);
    expect([...hidden].filter((name) => NOT_FOCUSABLE.has(name)).sort()).toEqual([...NOT_FOCUSABLE].sort());
  });

  it('a « + » of the quick add shows under the focus and takes the pointer, as on the hover of its step, which stays as it was (B-2.3-41)', () => {
    const rule = (selector: string) => RULES.find((candidate) => candidate.selectors.includes(selector))?.body ?? '';
    expect(rule('.quick-add:focus-visible')).toMatch(/opacity: 1;.*pointer-events: auto/);
    expect(rule('.react-flow__node:hover .quick-add')).toMatch(/opacity: 1;.*pointer-events: auto/);
    expect(rule('.quick-add')).toMatch(/opacity: 0;.*pointer-events: none/);
  });
});
