// editor.tsx
// The editor of a map shown in a test: a map answered at once, preferences held in memory, under the keyboard manager of the application.

import { act, fireEvent, render, within } from '@testing-library/react';
import type { Schema } from '../api/client.ts';
import { MapScreen, type MapScreenProps } from '../features/map/MapScreen.tsx';
import { createMapEditor } from '../features/map/state/editor.ts';
import type { MapSource } from '../features/map/state/mapStore.ts';
import type { PreferenceStorage } from '../features/map/state/preferences.ts';
import { KeyboardProvider } from '../shared/keyboard/KeyboardContext.tsx';
import { MAP_ID } from './maps.ts';

/** A storage of the preferences held in memory, with the values given, and what the editor wrote in it. */
export function memoryStorage(values: Record<string, string> = {}): PreferenceStorage & { written: Map<string, string> } {
  const written = new Map<string, string>();
  return {
    written,
    getItem: (key) => values[key] ?? null,
    setItem: (key, value) => {
      written.set(key, value);
    },
  };
}

/** The screen of a map under the keyboard manager, as the application shows it. */
export function renderMapScreen(props: MapScreenProps) {
  return render(<KeyboardProvider><MapScreen {...props} /></KeyboardProvider>);
}

/** A source that gives the maps given, one a reading, the last one repeated. */
export function sourceOf(...maps: Schema<'MapView'>[]): MapSource {
  let reads = 0;
  return () => Promise.resolve({ map: maps[Math.min(reads++, maps.length - 1)] as Schema<'MapView'>, history: { undo: [], redo: [] } });
}

/** Shows a map in the editor and waits for its canvas; the next readings give the maps given after it. */
export async function showMap(map: Schema<'MapView'>, storage: PreferenceStorage | null = memoryStorage(), ...next: Schema<'MapView'>[]): Promise<HTMLElement> {
  const { container } = renderMapScreen({ mapId: MAP_ID, source: sourceOf(map, ...next), storage });
  return within(container).findByTestId('map-canvas');
}

/** A map shown in an editor the test holds: its state read and changed by the test, the notifications and screens opened kept. */
export async function showEditor(map: Schema<'MapView'>, ...next: Schema<'MapView'>[]) {
  const notes: [string, string | undefined][] = [];
  const opened: string[] = [];
  const editor = createMapEditor({ source: sourceOf(map, ...next), storage: memoryStorage(), notify: (text, kind) => notes.push([text, kind]), navigate: (path) => opened.push(path) });
  const { container } = renderMapScreen({ mapId: MAP_ID, editor });
  const canvas = await within(container).findByTestId('map-canvas');
  return { editor, canvas, notes, opened };
}

/** Opens the sheet of a step as the user does (AR-151): a click chooses the step, then Enter on it opens its sheet. */
export function openSheetOf(canvas: HTMLElement, stepId: string): void {
  const step = canvas.querySelector<HTMLElement>(`.react-flow__node[data-id="${stepId}"]`);
  if (step === null) throw new Error(`No step ${stepId} on the canvas`);
  fireEvent.click(step);
  act(() => {
    step.focus();
  });
  fireEvent.keyDown(step, { key: 'Enter' });
}
