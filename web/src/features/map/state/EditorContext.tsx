// EditorContext.tsx
// The editor of the map shown, given to the ribbon, the canvas, the headers and the floating elements; the hooks that read its stores.

import { createContext, useContext, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { runCommand } from '../../../shared/commands/commands.ts';
import type { Point } from '../model/geometry/grid.ts';
import { clipboard } from './clipboard.ts';
import { contextOf } from './commands/context.ts';
import { MAP_COMMANDS } from './commands/mapCommands.ts';
import type { MapEditor } from './editor.ts';
import type { MenuTarget } from './floating.ts';
import type { MapState } from './mapStore.ts';

const EditorContext = createContext<MapEditor | null>(null);

export function EditorProvider({ editor, children }: { editor: MapEditor; children: ReactNode }) {
  return <EditorContext.Provider value={editor}>{children}</EditorContext.Provider>;
}

export function useEditor(): MapEditor {
  const editor = useContext(EditorContext);
  if (editor === null) throw new Error('useEditor outside EditorProvider.');
  return editor;
}

/** A part of the state of the editor, drawn again when it changes. */
export function useMapState<T>(select: (state: MapState) => T): T {
  return useStore(useEditor().store, select);
}

/** The language the names of the map are shown in (ADR 083): the language shown, the one of the interface until the screen tells it. */
export function useMapLanguage(): string {
  const { i18n } = useTranslation();
  return useMapState((state) => state.shown?.language ?? i18n.language);
}

/**
 * The context the commands read, drawn again when what they read changes: the state of the editor, the clipboard and
 * the preferences; null while the map is not read.
 */
export function useCommandContext() {
  const editor = useEditor();
  useStore(editor.store);
  useStore(clipboard);
  useStore(editor.preferences);
  return contextOf(editor);
}

/** Runs a command of the map by its identifier, with the context of the moment and the object of a menu. */
export function runMapCommand(editor: MapEditor, id: string, target: MenuTarget | null = null, pointer: Point | null = null): boolean {
  const context = contextOf(editor, target, pointer);
  const command = MAP_COMMANDS.find((candidate) => candidate.id === id);
  return context !== null && command !== undefined && runCommand(command, context);
}
