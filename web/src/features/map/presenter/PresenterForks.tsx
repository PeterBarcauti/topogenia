// PresenterForks.tsx
// The exits of a decision shown (ADR 097, lot J10): a numbered button on each, near the decision, the one the story takes from this stop filled; a click or its number on the keyboard goes at once to the stop of the decision that takes that exit (« and if it is no? »): the tour tells every exit, the buttons choose which one now.

import { ViewportPortal } from '@xyflow/react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { MapEditor } from '../state/editor.ts';
import { pointOn } from './model/route.ts';
import { exitsOf } from './model/script.ts';
import type { Presentation } from './usePresentation.ts';

/** How far from the decision along its exit a number stands, in units of the map: beside it, whatever the length of the flow. */
const NUMBER_AT = 64;

/**
 * The exits of the decision of the scene shown, numbered from 1, with the one the tour takes from this stop and, for each
 * exit, the stop of the decision that takes it; none outside a decision with several.
 */
export function forkOf(presentation: Presentation, scene: number): { stepId: string; exits: string[]; followed: string | null; stops: ReadonlyMap<string, number> } | null {
  const current = presentation.scenes[scene];
  if (current?.kind !== 'step') return null;
  const stop = presentation.tour.stops[current.stop];
  const step = presentation.map.steps.find((candidate) => candidate.id === stop?.stepId);
  if (stop === undefined || step === undefined || (step.type !== 'decision' && step.type !== 'inclusive')) return null;
  const exits = exitsOf(presentation.map, step.id).map((flow) => flow.id);
  const stops = new Map(presentation.tour.stops.flatMap((visit, index): [string, number][] => (visit.stepId === step.id && visit.next?.flows[0] !== undefined ? [[visit.next.flows[0], index]] : [])));
  return exits.length < 2 ? null : { stepId: step.id, exits, followed: stop.next?.flows[0] ?? null, stops };
}

export function PresenterForks({ editor, presentation }: { editor: MapEditor; presentation: Presentation }) {
  const { t } = useTranslation();
  const scene = useStore(editor.presenter, (state) => state.player.scene);
  const fork = forkOf(presentation, scene);
  if (fork === null) return null;
  return (
    <ViewportPortal>
      {fork.exits.map((flowId, index) => {
        const trace = presentation.traces.get(flowId);
        if (trace === undefined) return null;
        const at = pointOn(trace, Math.min(0.5, NUMBER_AT / Math.max(1, trace.total)));
        const label = presentation.layout.flows.find((flow) => flow.route.id === flowId)?.text ?? '';
        const number = index + 1;
        const name = label === '' ? t('presenter.fork', { number }) : t('presenter.fork_named', { number, label });
        return (
          <button key={flowId} type="button" className={`presenter-fork${flowId === fork.followed ? ' is-followed' : ''}`} title={name} aria-label={name}
            style={{ transform: `translate(${String(at.x)}px, ${String(at.y)}px)` }} data-testid={`presenter-fork-${String(number)}`}
            onClick={() => {
              const taken = fork.stops.get(flowId);
              if (taken !== undefined) editor.presenter.getState().choose(taken, presentation.scenes);
            }}>
            {number}
          </button>
        );
      })}
    </ViewportPortal>
  );
}
