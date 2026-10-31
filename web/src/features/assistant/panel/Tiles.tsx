// Tiles.tsx
// The empty panel of the assistant: a greeting and the four tiles, one per practice, with examples fitted to the map (spec 3.0 section 3.4).

import { FilePlus2, MessageCircleQuestion, PenLine, ScanSearch, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { Intent } from './assistantStore.ts';
import type { ThreadContext } from './threadContext.ts';

/** The practices of the tiles, in their order; the first two change the map, for the facilitator only. */
const TILES: readonly { readonly intent: Exclude<Intent, 'free'>; readonly icon: typeof Sparkles; readonly proposes: boolean }[] = [
  { intent: 'create', icon: FilePlus2, proposes: true },
  { intent: 'modify', icon: PenLine, proposes: true },
  { intent: 'analyze', icon: ScanSearch, proposes: false },
  { intent: 'ask', icon: MessageCircleQuestion, proposes: false },
];

/** An empty map offers to create first; the home page offers only to create. A tile fills the field with its example and sets the practice. */
export function Tiles({ context }: { context: ThreadContext }) {
  const { t } = useTranslation();
  const empty = context.home || context.status?.empty_map === true;
  const canPropose = context.home || context.status?.can_propose === true;
  const tiles = context.home
    ? TILES.filter((tile) => tile.intent === 'create')
    : empty ? [...TILES].sort((a, b) => Number(b.intent === 'create') - Number(a.intent === 'create')) : TILES;
  const greeting = context.home ? 'assistant.panel.hello_home' : empty ? 'assistant.panel.hello_empty' : 'assistant.panel.hello';
  return (
    <div className="assistant-welcome" data-testid="assistant-tiles">
      <div className="assistant-hello">
        <span className="assistant-orb" aria-hidden="true"><Icon icon={Sparkles} size={20} /></span>
        <p>{t(greeting)}</p>
      </div>
      <div className={`assistant-tiles${tiles.length === 1 ? ' is-single' : ''}`}>
        {tiles.map((tile) => {
          const allowed = !tile.proposes || canPropose;
          const example = t(`assistant.panel.tiles.${tile.intent}.${empty ? 'example_empty' : 'example'}`);
          return (
            <button key={tile.intent} type="button" className={`assistant-tile is-${tile.intent}`} data-testid={`assistant-tile-${tile.intent}`} disabled={!allowed}
              title={allowed ? example : t('assistant.panel.facilitator_only')}
              onClick={() => { context.store.getState().choose(tile.intent, example); }}>
              <span className="tile-icon" aria-hidden="true"><Icon icon={tile.icon} size={18} /></span>
              <span className="tile-title">{t(`assistant.panel.tiles.${tile.intent}.title`)}</span>
              <span className="tile-text">{allowed ? t(`assistant.panel.tiles.${tile.intent}.text`) : t('assistant.panel.facilitator_only')}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
