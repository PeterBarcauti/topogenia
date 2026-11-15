// cursors.test.ts
// The cursors of the link mode: a spool, then the thread unwound once the start is chosen, 24 px in the ink and the white of the charter, active at the end of the thread, on the whole map (AR-153).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const styles = import.meta.dirname;
const tokenOf = (name: string): string => {
  const value = new RegExp(`--${name}:\\s*(#[0-9A-Fa-f]{6});`).exec(readFileSync(join(styles, 'tokens.css'), 'utf8'))?.[1];
  if (value === undefined) throw new Error(`Token --${name} missing`);
  return value.toUpperCase();
};
const cursorOf = (name: string): string => readFileSync(join(styles, 'cursors', name), 'utf8');
const CURSORS = ['link-spool.svg', 'link-thread.svg'];

describe('the cursors of the link mode (AR-153)', () => {
  it.each(CURSORS.map((name) => [name]))('%s is drawn on 24 px, in the ink and the white of the charter only', (name) => {
    const svg = cursorOf(name);
    expect(svg).toMatch(/^<svg [^>]*width="24" height="24" viewBox="0 0 24 24"/);
    const colours = new Set([...svg.matchAll(/#[0-9A-Fa-f]{3,8}\b/g)].map((match) => match[0].toUpperCase()));
    expect([...colours].sort()).toEqual([tokenOf('text'), tokenOf('surface')].sort());
  });

  it.each(CURSORS.map((name) => [name]))('%s ends its thread in a dot at the point that is active, (2, 2) of the cursor', (name) => {
    expect(cursorOf(name)).toMatch(/<circle cx="2\.5" cy="2\.5" r="1\.9"/);
  });

  it('the background, the steps and what they hold, the handles and the flows take the spool, then the thread once the start is chosen; the cross when no image', () => {
    const css = readFileSync(join(styles, 'map.css'), 'utf8');
    const rule = (selectorStart: string, file: string) => {
      const found = new RegExp(String.raw`([^{}]*)\{ cursor: url\('\./cursors/${file.replace('.', String.raw`\.`)}'\) 2 2, crosshair; \}`).exec(css);
      const selectors = (found?.[1] ?? '').replace(/\/\*[\s\S]*?\*\//g, '').split(',').map((selector) => selector.trim());
      return ['.react-flow__pane', '.react-flow__node-step', '.react-flow__node-step *', '.react-flow__handle.step-handle', '.react-flow__edge']
        .map((target) => selectors.includes(`${selectorStart} ${target}`));
    };
    expect(rule('.map-canvas.is-linking', 'link-spool.svg')).toEqual([true, true, true, true, true]);
    expect(rule('.map-canvas.is-linking.has-link-source', 'link-thread.svg')).toEqual([true, true, true, true, true]);
  });
});
