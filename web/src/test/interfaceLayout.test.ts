// interfaceLayout.test.ts
// The cutting of the interface read on its sources (annex B 8.1 and 8.2, spec 3.5): the folders, the components of 250 lines at most, the canvas apart from its hooks, the ribbon, the floating elements, the keyboard, the undo on the host.

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const src = join(import.meta.dirname, '..');
const read = (file: string): string => readFileSync(join(src, file), 'utf8');
const code = (file: string): string => read(file).replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

/** The modules of the interface, the tests, their helpers and the generated client aside. */
const shipped = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/'));

/** The lines of a source as ESLint counts them for max-lines. */
const linesOf = (text: string): number => text.split('\n').length - (text.endsWith('\n') ? 1 : 0);

/** The components of more than `limit` lines (spec 3.5: 250). */
const longComponents = (files: readonly { name: string; text: string }[], limit = 250): string[] =>
  files.filter((file) => file.name.endsWith('.tsx') && linesOf(file.text) > limit).map((file) => `${file.name} (${String(linesOf(file.text))})`);

describe('the folders of web/src (B-8.2-01, spec 3.5)', () => {
  it('app, api, shared, features and styles, with the entry point; the features home, project, map, sheet, translations and assistant', () => {
    const top = readdirSync(src).filter((name) => statSync(join(src, name)).isDirectory()).sort();
    expect(top).toEqual(['api', 'app', 'features', 'shared', 'styles', 'test']);
    expect(readdirSync(join(src, 'features')).sort()).toEqual(['assistant', 'home', 'map', 'project', 'sheet', 'translations']);
    const map = readdirSync(join(src, 'features/map')).filter((name) => statSync(join(src, 'features/map', name)).isDirectory()).sort();
    expect(map).toEqual(['canvas', 'chrome', 'edges', 'export', 'model', 'nodes', 'panels', 'state']);
    expect(['main.tsx', 'api/generated/schema.ts', 'shared/ui/Dialog.tsx', 'styles/tokens.css'].every((file) => existsSync(join(src, file)))).toBe(true);
  });
});

describe('components of 250 lines at most (B-8.1-01; 1.x: a canvas of about 820 lines)', () => {
  it('no component of the interface has more than 250 lines', () => {
    const components = shipped.filter((file) => file.endsWith('.tsx'));
    expect(components.length).toBeGreaterThan(70);
    expect(longComponents(components.map((name) => ({ name, text: read(name) })))).toEqual([]);
  });

  it('detects a component of 251 lines, and counts as ESLint does', () => {
    expect(longComponents([{ name: 'Big.tsx', text: 'x\n'.repeat(251) }, { name: 'Fine.tsx', text: 'x\n'.repeat(250) }, { name: 'big.ts', text: 'x\n'.repeat(400) }])).toEqual(['Big.tsx (251)']);
  });
});

describe('the canvas and its hooks (B-8.1-02 to B-8.1-04, B-8.1-09 to B-8.1-11)', () => {
  const canvas = code('features/map/canvas/Canvas.tsx');

  it('the view and its bounds live in useBoundedViewport, the canvas keeping no state of its own', () => {
    expect(code('features/map/canvas/useBoundedViewport.ts')).toMatch(/export function useBoundedViewport\(/);
    expect(canvas).toMatch(/useBoundedViewport\(/);
    expect(canvas).not.toMatch(/\buse(State|Reducer|NodesState|EdgesState)\b/);
  });

  it('the nodes of React Flow are made from the map by flowElements, never kept as a copy to synchronise', () => {
    expect(canvas).toMatch(/useMemo\(\(\) => flowElements\(layout, states\)/);
    expect(code('features/map/canvas/flowElements.ts')).not.toMatch(/from 'react'/);
  });

  it('the gestures of the mouse are in the hooks of useCanvasEvents: the canvas only hands them to React Flow', () => {
    expect([...code('features/map/canvas/useCanvasEvents.ts').matchAll(/export function (use\w+)\(/g)].map((match) => match[1])).toEqual(
      expect.arrayContaining(['useClicks', 'useMenus', 'useSelectionAndDrag', 'useElementStates']));
    expect(canvas).not.toMatch(/\b(MouseEvent|PointerEvent|DragEvent)\b|\(event\b|\bevent\./);
  });

  it('the ribbon is a component of chrome/, beside the canvas; the floating elements are those of panels/, in a store of their own', () => {
    const screen = code('features/map/MapScreen.tsx');
    expect([/<Ribbon \/>/.test(screen), /<MapFloating \/>/.test(screen), /<Canvas /.test(screen)]).toEqual([true, true, true]);
    expect(canvas).not.toMatch(/Ribbon|panels\/|floating/i);
    expect(code('features/map/state/floating.ts')).toMatch(/export function createFloatingStore\(/);
    const floating = code('features/map/panels/MapFloating.tsx');
    expect(floating.match(/case '[\w-]+':/g)?.length).toBeGreaterThanOrEqual(14);
  });

  it('no reference of the editor of the map holds a copy of its state: only elements of the page and flags', () => {
    const refs = shipped.filter((file) => file.startsWith('features/map/'))
      .flatMap((file) => [...code(file).matchAll(/useRef(<[^>]*>)?\(([^)]*)\)/g)].map((match) => `${file} ${match[1] ?? ''}(${match[2] ?? ''})`));
    expect(refs.length).toBeGreaterThan(5);
    expect(refs.filter((ref) => !/<HTML\w+Element>\(null\)$|\((true|false)\)$/.test(ref))).toEqual([]);
  });
});

describe('menus, File menu and keyboard from the commands (B-8.1-05, B-8.1-07, B-8.1-08)', () => {
  it('the menus of the objects and the File menu are lists of commands, without React', () => {
    for (const file of ['features/map/state/commands/mapCommands.ts', 'features/map/state/commands/menuCommands.ts', 'shared/commands/commands.ts']) {
      expect([file, /from 'react'/.test(code(file))]).toEqual([file, false]);
    }
    expect(code('features/map/state/commands/mapCommands.ts')).toMatch(/export const FILE_MENU: readonly MenuLine<MapContext>\[\]/);
    expect(code('features/map/panels/MapFloating.tsx')).toMatch(/file: \{ lines: FILE_MENU/);
    expect(code('features/map/panels/MapFloating.tsx')).toMatch(/menuItemsOf\(MAP_COMMANDS, menu\.lines, context\)/);
  });

  it('only the manager of the keyboard listens to the keys of the window; the canvas gives it its layer', () => {
    const listening = shipped.filter((file) => /addEventListener\(\s*'key(down|up|press)'/.test(code(file)));
    expect(listening).toEqual(['shared/keyboard/keyboard.ts']);
    expect(code('features/map/canvas/useCanvasKeys.ts')).toMatch(/useKeyLayer\('canvas'/);
  });
});
