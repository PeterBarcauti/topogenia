// translateCommands.ts
// « Translate… » in the menus of a step, a labelled flow and a lane: the manual translations of its name, for who may change the structure when more than one language is configured (B-5-28, spec 10).

import { Languages } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { openTranslate, translatedOf } from '../actions/translations.ts';
import type { TranslatedObject } from '../floating.ts';
import type { MapContext } from './context.ts';

/** The object of the menu whose name may be translated, null for another menu. */
export function translatedTarget(context: MapContext): TranslatedObject | null {
  const { target } = context;
  switch (target?.kind) {
    case 'step':
      return { kind: 'step', id: target.stepId };
    case 'flow':
      return { kind: 'flow', id: target.flowId };
    case 'lane':
      return { kind: 'lane', id: target.laneId };
    default:
      return null;
  }
}

/**
 * Shown to who may change the structure, or translate in another language than the one of origin (ADR 083), with another
 * language to translate into, on an object that has a name.
 */
const offered = (context: MapContext): boolean => {
  const object = translatedTarget(context);
  return (context.canEdit || context.state.translating) && context.editor.languages.length > 1 && object !== null && translatedOf(context.map, object) !== null;
};

const translate = (id: string): Command<MapContext> => ({
  id, label: 'menu.translate', icon: Languages, visible: offered, enabled: offered,
  run: (context) => {
    const object = translatedTarget(context);
    if (object !== null) openTranslate(context.editor, object);
  },
});

export const TRANSLATE_COMMANDS: readonly Command<MapContext>[] = [translate('step.translate'), translate('flow.translate'), translate('lane.translate')];
