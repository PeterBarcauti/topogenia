// usePresenterStage.ts
// What the stage of a presentation runs (ADR 097): its clock at each frame while it plays, its keys on the modal layer (the keys of a presenter and of the remotes; Tab that walks its controls; none drives the story while a comment is written or the voice asks its question or shows its settings), the full screen asked by the gesture that opened it, kept with the presentation when a browser leaves it on the Échap that closed something (`escapeKey.ts`), the bar and the pointer hidden while nothing moves.

import { useEffect, useRef, useState, type KeyboardEvent as FieldKeyboardEvent, type RefObject } from 'react';
import { useStore } from 'zustand';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import type { MapEditor } from '../state/editor.ts';
import type { Move } from './model/player.ts';
import { lineEndAt } from './model/timing.ts';
import { forkOf } from './PresenterForks.tsx';
import type { PresenterState } from './presenterStore.ts';
import type { Presentation } from './usePresentation.ts';
import type { StageFocus } from './usePresenterFocus.ts';
import type { PresenterVoice } from './usePresenterVoice.ts';
import { switchVoice } from './voiceSwitch.ts';
import { escapeComment, openComment } from './comments.ts';
import { switchMessages } from './messages.ts';
import { detailedAt, diveInto } from './dive.ts';
import { escapeLeftFullScreen, escapeTaken, useEscapeWatch } from './escapeKey.ts';

/** A frame longer than this (a tab left, a pause of the machine) counts as this, so that the story never jumps. */
const LONGEST_FRAME_MS = 100;

/** The bar and the pointer hide after this time without a move of the mouse, while it plays (reveal.js: 5 s). */
const IDLE_MS = 3000;

/** A move of the mouse is counted at most this often. */
const MOVE_EVERY_MS = 400;

/** Runs the clock at each frame while it plays and the screen shows the map; held at the end of a sentence the voice still reads. */
export function usePresenterClock(editor: MapEditor, presentation: Presentation): void {
  const { durations, scripts } = presentation;
  const playing = useStore(editor.presenter, (state) => state.player.playing && state.screen === 'map');
  useEffect(() => {
    if (!playing) return undefined;
    let frame = 0;
    let last = performance.now();
    const loop = (now: number) => {
      const { player, speaking, tick } = editor.presenter.getState();
      // The sentence shown stays until the voice has said it: the clock stops a moment before its end.
      const hold = speaking ? lineEndAt(scripts[player.scene]?.lines ?? [], player.elapsed) - 1 : Number.POSITIVE_INFINITY;
      tick(Math.min(LONGEST_FRAME_MS, Math.max(0, now - last)), durations, hold);
      last = now;
      frame = requestAnimationFrame(loop);
    };
    frame = requestAnimationFrame(loop);
    return () => {
      cancelAnimationFrame(frame);
    };
  }, [editor, playing, durations, scripts]);
}

/** The moves of the keys of a presenter: PowerPoint, reveal.js and the remotes (Page Down and Page Up, the point for black); the arrows go a
 * whole step forward or back; Page Down and Enter reveal the next sentence before the next scene, as a remote reveals a slide (audit 3.2, UX-03). */
const MOVES: Readonly<Record<string, Move>> = {
  ArrowRight: 'nextStep', ArrowDown: 'nextStep', PageDown: 'next', Enter: 'next', N: 'next',
  ArrowLeft: 'previousStep', ArrowUp: 'previousStep', PageUp: 'previous', Backspace: 'previous', P: 'previous',
  Home: 'start', End: 'end',
};

/** The number of an exit typed, with Shift or not (the digits of a French keyboard), null for another key. */
const digitOf = (chord: string): number | null => {
  const digit = /^(?:Shift[+])?([1-9])$/.exec(chord)?.[1];
  return digit === undefined ? null : Number(digit);
};

/** What the keys of the stage act on. */
export interface StageKeys {
  readonly editor: MapEditor;
  readonly presentation: Presentation;
  readonly fullscreen: () => void;
  readonly voice: PresenterVoice;
  readonly focus: StageFocus;
}

/** A key of the story: the moves of a presenter, an exit of the decision shown, and the controls of the bar by their letter. */
function storyKey({ editor, presentation, fullscreen, voice }: StageKeys, chord: string): void {
  const presenter = editor.presenter.getState();
  const move = MOVES[chord];
  const digit = digitOf(chord);
  const fork = digit === null ? null : forkOf(presentation, presenter.player.scene);
  const exit = fork === null || digit === null ? undefined : fork.exits[digit - 1];
  const taken = fork === null || exit === undefined ? undefined : fork.stops.get(exit);
  if (taken !== undefined) presenter.choose(taken, presentation.scenes);
  else if (move !== undefined) presenter.move(move, presentation.scenes, presentation.lines);
  else if (chord === 'Space' || chord === 'K') presenter.toggle(presentation.durations);
  else if (chord === 'B' || chord === '.' || chord === 'Shift+.') presenter.blank('black');
  else if (chord === 'W' || chord === ',') presenter.blank('white');
  else if (chord === 'C' || chord === 'J') editor.preferences.getState().set('presenterSubtitles', !editor.preferences.getState().presenterSubtitles);
  else if (chord === 'V') switchVoice(editor, voice, presentation.language);
  else if (chord === 'E') switchMessages(editor, 'step', presentation.scenes);
  else if (chord === 'R') switchMessages(editor, 'passage', presentation.scenes);
  else if (chord === 'F') fullscreen();
  else if (chord === 'T') openComment(editor, presentation);
  else if (chord === 'D') {
    const detailed = detailedAt(presentation, presenter.player.scene);
    if (detailed !== null) diveInto(editor, presentation, detailed);
  }
  else if (chord === 'U') presenter.surface();
  else if (['+', 'Shift++', '=', 'Shift+='].includes(chord)) presenter.faster();
  else if (['-', 'Shift+-', '_', 'Shift+_'].includes(chord)) presenter.slower();
  else if (chord === 'F5') presenter.start(presenter.from);
  else if (chord === 'Escape') presenter.stop();
}

/** Whether the voice shows its question or its settings: the bar then stays shown, and no key drives the story (R6-01). */
export const voicePanelOpen = (presenter: PresenterState): boolean => presenter.asking || presenter.tuning;

/** Échap on the question or the settings of the voice: they close, and the key is marked as taken (`escapeKey.ts`). */
const escapeVoicePanel = (presenter: PresenterState): void => {
  if (!voicePanelOpen(presenter)) return;
  escapeTaken();
  presenter.ask(false);
  presenter.tune(false);
};

/** The chords left to the browser during a presentation: its reload of the page (ADR 097: « Ctrl+F5 et Ctrl+R rechargent toujours »). */
const BROWSER_CHORDS: ReadonlySet<string> = new Set(['Ctrl+R', 'Ctrl+F5']);

/**
 * Entrée or Espace on a button of the stage the keyboard walked to presses it, once for a key held, instead of « next
 * sentence » or « pause » (R6-05); says whether it did.
 */
function pressFocused(focus: StageFocus, event: KeyboardEvent, chord: string): boolean {
  const button = chord === 'Enter' || chord === 'Space' ? focus.pressed() : null;
  if (button !== null && !event.repeat) button.click();
  return button !== null;
}

/**
 * The keys of the stage, on the modal layer: every key is stopped there, so that none reaches the canvas of the editor
 * under it, presses a control left focused in it, nor reloads the page (F5); F1 and Ctrl+S stay heard, and the reload of
 * the browser stays its own. Tab walks the controls of the stage. While the voice asks its question or shows its
 * settings, Échap closes them, V does as the button of the voice, and no other key drives the story (R6-01). While a
 * comment is written, wherever the focus is, Échap closes its field, its text kept, not the presentation, and no key
 * drives the story (R2-05, R6-11). Échap and Entrée held act once: their repeats close nothing more, never end the
 * presentation after the field or the panel the press closed (essai J6), and tell the story no further after the field
 * Entrée closed.
 */
export function usePresenterKeys(keys: StageKeys): void {
  const { editor, presentation, voice, focus } = keys;
  useKeyLayer('modal', (event, chord) => {
    if (BROWSER_CHORDS.has(chord)) return false;
    if (event.type !== 'keydown' || ((chord === 'Escape' || chord === 'Enter') && event.repeat)) return true;
    const presenter = editor.presenter.getState();
    if (chord === 'Tab' || chord === 'Shift+Tab') focus.tab(chord !== 'Tab');
    else if (pressFocused(focus, event, chord)) return true;
    else if (voicePanelOpen(presenter)) {
      if (chord === 'Escape') escapeVoicePanel(presenter);
      else if (chord === 'V') switchVoice(editor, voice, presentation.language);
    } else if (presenter.writing !== null) {
      if (chord === 'Escape') escapeComment(editor, presentation);
    } else storyKey(keys, chord);
    return true;
  });
}

/**
 * A key typed in a field of the stage (the comment, the settings of the voice), which the keyboard manager leaves to its
 * field: Tab stays in the stage, F5 never reloads the page of a presentation, and Échap closes the question or the
 * settings of the voice.
 */
export function stageFieldKey(editor: MapEditor, focus: StageFocus, event: FieldKeyboardEvent): void {
  const chorded = event.ctrlKey || event.metaKey || event.altKey;
  if (event.key === 'Tab' && !chorded) {
    event.preventDefault();
    focus.tab(event.shiftKey);
  } else if (event.key === 'F5' && !chorded) event.preventDefault();
  else if (event.key === 'Escape' && !event.repeat) escapeVoicePanel(editor.presenter.getState());
}

/** Asks the full screen of an element, or leaves it; a browser that refuses, or has no full screen, leaves the page as it is. */
const ask = (element: HTMLElement): void => {
  Promise.resolve().then(() => element.requestFullscreen({ navigationUI: 'hide' })).catch(() => undefined);
};
const leave = (): void => {
  Promise.resolve().then(() => document.exitFullscreen()).catch(() => undefined);
};

/**
 * The full screen of the stage, asked at once by the gesture that opened it; refused, the stage covers the window all the
 * same. Leaving it by Échap or by the browser ends the presentation, as in PowerPoint; leaving it by F keeps it, and so
 * does Échap while a comment is written (ADR 099) or while the voice asks its question or shows its settings: Échap is
 * then for what is open (`useEscapeHeld`). A browser that leaves the full screen on the very Échap that closed the
 * field or the panel, pressed or released, keeps the presentation too, in its window: F gives the full screen back.
 */
export function useFullscreen(editor: MapEditor, root: RefObject<HTMLDivElement | null>): () => void {
  const toggled = useRef(false);
  useEscapeWatch();
  useEffect(() => {
    const element = root.current;
    if (element !== null) ask(element);
    const left = () => {
      if (document.fullscreenElement !== null) return;
      // Asked first, whatever is open: the mark of an Échap is said once.
      const escaped = escapeLeftFullScreen();
      if (toggled.current) toggled.current = false;
      else if (!escaped && editor.presenter.getState().writing === null && !voicePanelOpen(editor.presenter.getState())) editor.presenter.getState().stop();
    };
    document.addEventListener('fullscreenchange', left);
    return () => {
      document.removeEventListener('fullscreenchange', left);
      if (document.fullscreenElement !== null && document.fullscreenElement === element) leave();
    };
  }, [editor, root]);
  return () => {
    const element = root.current;
    if (element === null) return;
    if (document.fullscreenElement === element) {
      toggled.current = true;
      leave();
    } else ask(element);
  };
}

/** Whether the bar and the pointer are hidden: after a while without a move of the mouse, while it plays; a move shows them. */
export function useIdle(editor: MapEditor): { idle: boolean; moved: () => void } {
  const playing = useStore(editor.presenter, (state) => state.player.playing);
  const [lastMove, setLastMove] = useState(0);
  const [idle, setIdle] = useState(false);
  useEffect(() => {
    if (!playing) return undefined;
    const timer = setTimeout(() => {
      setIdle(true);
    }, IDLE_MS);
    return () => {
      clearTimeout(timer);
    };
  }, [playing, lastMove]);
  return {
    idle: idle && playing,
    // A move counts once in a while, not at each pixel: the stage is not drawn again for nothing.
    moved: () => {
      const now = Date.now();
      if (!idle && now - lastMove <= MOVE_EVERY_MS) return;
      setIdle(false);
      setLastMove(now);
    },
  };
}
