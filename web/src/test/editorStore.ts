// editorStore.ts
// An editor of a map without its screen, for the tests of its stores, operations and commands: the map read at once, the notifications and the screens opened kept.

import type { Schema } from '../api/client.ts';
import type { ToastKind } from '../shared/ui/toasts.ts';
import { createMapEditor, type MapEditor } from '../features/map/state/editor.ts';
import { memoryStorage } from './editor.tsx';
import { MAP_ID } from './maps.ts';

export interface TestEditor {
  readonly editor: MapEditor;
  /** The notifications shown, with their kind. */
  readonly notes: [string, ToastKind | undefined][];
  /** The paths opened. */
  readonly opened: string[];
}

/** An editor of the map given, read before it is handed over; the next readings give the maps given after it, the last one repeated. */
export async function editorOf(map: Schema<'MapView'>, ...next: Schema<'MapView'>[]): Promise<TestEditor> {
  const notes: [string, ToastKind | undefined][] = [];
  const opened: string[] = [];
  const maps = [map, ...next];
  let reads = 0;
  const editor = createMapEditor({
    source: () => Promise.resolve({ map: maps[Math.min(reads++, maps.length - 1)] ?? map, history: { undo: [], redo: [] } }),
    storage: memoryStorage(),
    notify: (text, kind) => notes.push([text, kind]),
    navigate: (path) => opened.push(path),
  });
  await editor.store.getState().load(MAP_ID);
  return { editor, notes, opened };
}
