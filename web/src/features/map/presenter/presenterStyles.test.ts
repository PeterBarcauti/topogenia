// presenterStyles.test.ts
// The style sheet of the stage of a presentation read as a text (relecture 6: R6-06, R6-07): a ring of focus that is a valid declaration, shown while the keyboard walks the controls; the bar hidden at rest whatever button a click left focused; the pointer hidden at rest over the map too, and no hand on a flow that takes no click.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const sheet = (name: string): string => readFileSync(join(import.meta.dirname, '../../../styles', name), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

/** The rules of a style sheet: each of their selectors with their declarations. */
const rulesOf = (css: string): { selector: string; body: string }[] =>
  [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)].flatMap((match) => (match[1] ?? '').split(',').map((selector) => ({ selector: selector.trim().replace(/\s+/g, ' '), body: match[2] ?? '' })));

const presenter = rulesOf(sheet('presenter.css'));

/** The value a selector gives a property, null when no rule of it sets it. */
const valueOf = (selector: string, property: string): string | null => {
  const found = presenter.filter((rule) => rule.selector === selector).flatMap((rule) => [...rule.body.matchAll(/([\w-]+)\s*:\s*([^;]+)/g)]).filter((match) => match[1] === property);
  return found.at(-1)?.[2]?.trim() ?? null;
};

describe('the ring of the focus in the stage (relecture 6: R6-06)', () => {
  it('is the token of the charter, already a whole outline, never wrapped in another width and style: the declaration would be invalid and no ring drawn', () => {
    expect(sheet('tokens.css')).toMatch(/--focus-outline:\s*2px solid var\(--accent\);/);
    const outlines = presenter.flatMap((rule) => [...rule.body.matchAll(/outline\s*:\s*([^;]+)/g)].map((match) => (match[1] ?? '').trim())).filter((value) => value.includes('--focus-outline'));
    expect(outlines.length).toBeGreaterThan(0);
    expect(outlines.filter((value) => value !== 'var(--focus-outline)')).toEqual([]);
    for (const selector of ['.presenter-fork:focus-visible', '.presenter-button:focus-visible', '.presenter-chapter:focus-visible']) {
      expect([selector, valueOf(selector, 'outline')]).toEqual([selector, 'var(--focus-outline)']);
    }
  });

  it('shows while the keyboard walks the controls only: a button a click left focused, or given the focus back, shows none, Entrée and Espace being then keys of the story', () => {
    expect(valueOf('.presenter:not(.is-keyed) button:focus-visible', 'outline')).toBe('none');
    expect(valueOf('.presenter:focus-visible', 'outline')).toBe('none');
  });
});

describe('the bar and the pointer hidden at rest (relecture 6: R6-07)', () => {
  it('brings the bar back for the focus while the keyboard walks its controls, not for a button a click left focused', () => {
    expect(valueOf('.presenter.is-idle .presenter-bar', 'opacity')).toBe('0');
    expect(valueOf('.presenter.is-idle.is-keyed .presenter-bar:focus-within', 'opacity')).toBe('1');
    expect(presenter.filter((rule) => rule.selector === '.presenter.is-idle .presenter-bar:focus-within')).toEqual([]);
  });

  it('hides the pointer over the map too, whose steps set their own, and shows the hand on a flow and on its label, which take a click as a step does (lot J10)', () => {
    expect([valueOf('.presenter.is-idle', 'cursor'), valueOf('.presenter.is-idle .presenter-flow *', 'cursor')]).toEqual(['none', 'none']);
    expect(['.presenter-flow .react-flow__node', '.presenter-flow .react-flow__edge', '.presenter-flow .edge-label'].map((selector) => valueOf(selector, 'cursor')))
      .toEqual(['pointer', 'pointer', 'pointer']);
  });
});
