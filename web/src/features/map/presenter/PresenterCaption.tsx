// PresenterCaption.tsx
// What the room reads (ADR 097), in the language shown: the subtitle of the scene, one sentence at a time, none when the scene has nothing to say that the map does not write (no banner: it repeated the box lit, essai J2), shown whatever the setting kept when the voice is on and no voice can say the language, told to the screen readers unless the voice reads it; the counters of the work and the lead time run so far, rounded as the sentences.

import { useEffect, useState } from 'react';
import { useStore } from 'zustand';
import { leadQuantity, workQuantity } from '../model/valueStream.ts';
import type { MapEditor } from '../state/editor.ts';
import { countersAt, leadInUnit, saidNumber, workInUnit } from './model/counters.ts';
import { lineAt } from './model/timing.ts';
import { useReducedMotion } from './usePresenterCamera.ts';
import type { Presentation } from './usePresentation.ts';

/**
 * The sentence of the moment: drawn again only when it changes. `silent`: no voice can say the language shown; the voice
 * on, its sentences are then read here whatever the subtitles kept, never a silent presentation without text (R6-04).
 * The caption, which stays while its sentences are made again, tells each one to the screen readers; not while the voice
 * reads it aloud, which would say it twice (R6-13).
 */
export function PresenterCaption({ editor, presentation, silent }: { editor: MapEditor; presentation: Presentation; silent: boolean }) {
  const subtitles = useStore(editor.preferences, (state) => state.presenterSubtitles || (state.presenterVoice && silent));
  const voiced = useStore(editor.preferences, (state) => state.presenterVoice && !silent);
  const playing = useStore(editor.presenter, (state) => state.player.playing && state.screen === 'map');
  // The comment written stands where the subtitles stand (ADR 099).
  const writing = useStore(editor.presenter, (state) => state.writing !== null);
  const moment = useStore(editor.presenter, (state) => {
    const lines = presentation.scripts[state.player.scene]?.lines ?? [];
    return `${String(state.player.scene)} ${String(lineAt(lines, state.player.elapsed))}`;
  });
  if (!subtitles || writing) return null;
  const [scene, line] = moment.split(' ').map(Number) as [number, number];
  const text = presentation.scripts[scene]?.lines[line] ?? '';
  return (
    <div className="presenter-caption" lang={presentation.language} aria-live={voiced && playing ? 'off' : 'polite'} data-testid="presenter-caption">
      {text !== '' && <p className="presenter-line" key={`line-${moment}`} data-testid="presenter-line">{text}</p>}
    </div>
  );
}

/** The time of the count of a counter up to the figures of the stop just reached. */
const COUNT_MS = 900;

/** The work, in minutes, from which a counter writes more than 0: half of the tenth it is rounded to. */
const WRITTEN_FROM = 0.05;

/**
 * The part of the count done since the scene was reached, from 0 to 1: by the clock of the screen, not by the one of the
 * story, so that a stop reached in pause (a presenter moving by a remote) counts up too; at once when less motion is asked.
 */
function useCountPart(scene: number, ms: number): number {
  const [part, setPart] = useState({ scene, value: 0 });
  useEffect(() => {
    if (ms === 0) return undefined;
    const start = performance.now();
    let frame = 0;
    const count = (now: number) => {
      const value = Math.min(1, Math.max(0, now - start) / ms);
      setPart({ scene, value });
      if (value < 1) frame = requestAnimationFrame(count);
    };
    frame = requestAnimationFrame(count);
    return () => {
      cancelAnimationFrame(frame);
    };
  }, [scene, ms]);
  if (ms === 0) return 1;
  // A scene just reached starts its count from nothing, before its first frame.
  return part.scene === scene ? part.value : 0;
}

/**
 * The work and the lead time run up to the stop shown, counted up as it is reached (A1 of the study): the lead time grows
 * by days while the work grows by minutes. Shown only when the tour knows some figure.
 */
export function PresenterCounters({ editor, presentation }: { editor: MapEditor; presentation: Presentation }) {
  const { map, tour, t, language } = presentation;
  const index = useStore(editor.presenter, (state) => state.player.scene);
  const part = useCountPart(index, useReducedMotion() ? 0 : COUNT_MS);
  const scene = presentation.scenes[index];
  const whole = countersAt(map, tour, tour.stops.length - 1);
  if (scene === undefined || scene.kind === 'opening' || (whole.workMinutes === 0 && whole.leadHours === 0)) return null;
  const stop = scene.kind === 'closing' ? tour.stops.length - 1 : scene.stop;
  const now = countersAt(map, tour, stop);
  const before = countersAt(map, tour, stop - 1);
  const done = scene.kind === 'step' ? part : 1;
  const work = before.workMinutes + (now.workMinutes - before.workMinutes) * done;
  const lead = before.leadHours + (now.leadHours - before.leadHours) * done;
  // Nothing to count before the first figure, a work written 0 under a twentieth of a minute: the counters come in with it.
  if (work < WRITTEN_FROM && lead === 0) return null;
  const writing = { language, workdayHours: map.value_stream.work_time.workday_hours, t };
  // The unit of the value stream, the number as the sentences say it: the counter and the subtitle show the same figure (R6-08).
  const quantities = [
    { key: 'work', label: t('presenter.counters.work'), value: { ...workQuantity(work, writing), value: saidNumber(workInUnit(work), language) } },
    { key: 'lead', label: t('presenter.counters.lead'), value: { ...leadQuantity(lead, writing), value: saidNumber(leadInUnit(lead, writing.workdayHours), language) } },
  ];
  return (
    <div className="presenter-counters" lang={language} data-testid="presenter-counters">
      {quantities.map((quantity) => (
        <div key={quantity.key} className="presenter-counter" data-testid={`presenter-counter-${quantity.key}`}>
          <span className="presenter-counter-label">{quantity.label}</span>
          <span className="presenter-counter-value">{quantity.value.value}<small>{quantity.value.unit}</small></span>
        </div>
      ))}
    </div>
  );
}
