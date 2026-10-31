// AssistantThread.tsx
// The thread of the panel of the assistant: the four tiles of an empty panel, then the requests and the cards of the replies, the last kept in view as they come (spec 3.0 section 3.4).

import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Turn } from './assistantStore.ts';
import type { ThreadContext } from './threadContext.ts';
import { Tiles } from './Tiles.tsx';
import { CopiedCard, FailureCard, ProgressCard, ReplyCard, RequestCard } from './TurnCards.tsx';

export type { ThreadContext } from './threadContext.ts';

function TurnView({ turn, context }: { turn: Turn; context: ThreadContext }) {
  switch (turn.kind) {
    case 'request':
      return <RequestCard turn={turn} />;
    case 'progress':
      return <ProgressCard turn={turn} />;
    case 'reply':
      return <ReplyCard turn={turn} context={context} />;
    case 'failure':
      return <FailureCard turn={turn} context={context} />;
    case 'copied':
      return <CopiedCard turn={turn} context={context} />;
  }
}

export function AssistantThread({ context }: { context: ThreadContext }) {
  const { t } = useTranslation();
  const turns = useStore(context.store, (state) => state.turns);
  const thread = useRef<HTMLDivElement>(null);
  const last = turns.at(-1);
  // The last turn kept in view as the replies come: a new turn, or a progress that becomes a reply.
  useEffect(() => {
    const element = thread.current;
    if (element !== null) element.scrollTop = element.scrollHeight;
  }, [turns.length, last?.kind]);
  return (
    <div ref={thread} className="assistant-thread" role="log" aria-label={t('assistant.panel.thread')} data-testid="assistant-thread">
      {turns.length === 0 ? <Tiles context={context} /> : turns.map((turn) => <TurnView key={turn.id} turn={turn} context={context} />)}
    </div>
  );
}
