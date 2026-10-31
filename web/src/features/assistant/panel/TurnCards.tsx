// TurnCards.tsx
// The cards of the thread of the assistant: the request of the person, the model at work, the reply in its form with its cost, the failure, the prompt copied for the copy and paste (spec 3.0 section 3.4).

import { useState } from 'react';
import { Check, ClipboardCopy, Copy, RotateCcw, Sparkles, TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { notify } from '../../../shared/ui/toasts.ts';
import type { CopiedTurn, FailureTurn, ProgressTurn, ReplyTurn, RequestTurn } from './assistantStore.ts';
import { FilePills } from './Attachments.tsx';
import { Findings, Proposal, Questions, TextWithCodes } from './ReplyParts.tsx';
import { copyText, type ThreadContext } from './threadContext.ts';

export function RequestCard({ turn }: { turn: RequestTurn }) {
  const { t } = useTranslation();
  const [whole, setWhole] = useState(false);
  const long = turn.text.length > 600 || turn.text.split('\n').length > 10;
  return (
    <div className="turn-request" data-testid="assistant-request">
      {turn.intent !== 'free' && <span className="intent-chip is-static">{t(`assistant.panel.tiles.${turn.intent}.title`)}</span>}
      <p className={`request-text${long && !whole ? ' is-folded' : ''}`}>{turn.text}</p>
      {long && <button type="button" className="link-button" data-testid="assistant-request-unfold" onClick={() => { setWhole(!whole); }}>{t(whole ? 'assistant.panel.fold' : 'assistant.panel.unfold')}</button>}
      <FilePills files={turn.files} />
      {turn.selection.length > 0 && <span className="request-selection">{t('assistant.panel.selection_sent', { count: turn.selection.length })}</span>}
    </div>
  );
}

/** The model at work: a moving mark, what it does, how long it has been, the last line of its thinking. */
export function ProgressCard({ turn }: { turn: ProgressTurn }) {
  const { t } = useTranslation();
  const phase = turn.phase === 'writing' && turn.characters > 0
    ? t('assistant.panel.phases.writing_count', { count: turn.characters })
    : t(`assistant.panel.phases.${turn.phase}`, { defaultValue: t('assistant.panel.phases.thinking') });
  return (
    <div className="turn-progress" data-testid="assistant-progress" role="status" aria-live="polite">
      <div className="progress-head">
        <span className="assistant-orb is-working" aria-hidden="true"><Icon icon={Sparkles} size={16} /></span>
        <span className="progress-phase">{phase}</span>
        <span className="progress-time">{t('assistant.panel.seconds', { count: Math.floor(turn.elapsed / 1000) })}</span>
      </div>
      {turn.thought !== null && <p key={turn.thought} className="progress-thought">{turn.thought}</p>}
      <div className="progress-bar" aria-hidden="true"><span /></div>
    </div>
  );
}

/** The tokens of the exchange and its cost, for the administrator only. */
function CostLine({ turn }: { turn: ReplyTurn }) {
  const { t, i18n } = useTranslation();
  const cost = turn.cost;
  if (cost === null) return null;
  const dollars = cost.estimated_usd === null ? null
    : new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 3 }).format(cost.estimated_usd);
  return (
    <p className="reply-cost" data-testid="assistant-cost">
      {t('assistant.panel.cost', {
        input: cost.input_tokens + cost.cache_read_tokens + cost.cache_write_tokens, cached: cost.cache_read_tokens, output: cost.output_tokens, model: cost.model,
      })}
      {dollars !== null && <strong> · ≈ {dollars}</strong>}
    </p>
  );
}

export function ReplyCard({ turn, context }: { turn: ReplyTurn; context: ThreadContext }) {
  const { t } = useTranslation();
  const { reply } = turn;
  const copy = async () => {
    const done = await copyText(reply.message);
    notify(t(done ? 'assistant.panel.copied' : 'assistant.panel.copy_refused'), done ? 'success' : 'error');
  };
  return (
    <article className={`turn-reply is-${reply.form}`} data-testid="assistant-reply" data-form={reply.form}>
      <header className="reply-head">
        <span className="reply-form">{t(`assistant.panel.forms.${reply.form}`)}</span>
        {turn.pasted && <span className="reply-pasted">{t('assistant.panel.pasted')}</span>}
        {turn.elapsed > 0 && <span className="reply-time">{t('assistant.panel.seconds', { count: Math.round(turn.elapsed / 1000) })}</span>}
        <button type="button" className="reply-copy" data-testid="assistant-reply-copy" title={t('assistant.panel.copy_reply')} aria-label={t('assistant.panel.copy_reply')}
          onClick={() => void copy()}><Icon icon={Copy} size={14} /></button>
      </header>
      {reply.message !== '' && <p className="reply-message"><TextWithCodes text={reply.message} objects={reply.objects} reveal={context.reveal} /></p>}
      {reply.form === 'questions' && <Questions reply={reply} context={context} />}
      {reply.form === 'findings' && <Findings reply={reply} context={context} />}
      {reply.form === 'proposal' && <Proposal reply={reply} context={context} translation={turn.translation} />}
      <CostLine turn={turn} />
    </article>
  );
}

export function FailureCard({ turn, context }: { turn: FailureTurn; context: ThreadContext }) {
  const { t } = useTranslation();
  const running = useStore(context.store, (state) => state.running);
  const text = turn.message ?? t(`assistant.panel.failures.${turn.failure}`, { defaultValue: t('assistant.panel.failures.other') });
  const retry = turn.retry;
  return (
    <div className={`turn-failure${turn.failure === 'cancelled' ? ' is-quiet' : ''}`} data-testid="assistant-failure" data-failure={turn.failure} role="alert">
      <p><Icon icon={TriangleAlert} size={15} /><span>{text}</span></p>
      {turn.detail !== null && turn.detail !== '' && <p className="failure-detail">{turn.detail}</p>}
      {retry !== null && (
        <button type="button" className="reply-action" data-testid="assistant-retry" disabled={running}
          onClick={() => void context.store.getState().send(retry.text, retry.intent, retry.files)}>
          <Icon icon={RotateCcw} size={14} />{t('assistant.panel.retry')}
        </button>
      )}
    </div>
  );
}

/** The prompt copied for the AI of the person: the way to copy it again and to paste its reply here. */
export function CopiedCard({ turn, context }: { turn: CopiedTurn; context: ThreadContext }) {
  const { t } = useTranslation();
  const [pasted, setPasted] = useState('');
  const running = useStore(context.store, (state) => state.running);
  const again = async () => {
    const done = await copyText(turn.prompt);
    notify(t(done ? 'assistant.panel.prompt_copied' : 'assistant.panel.copy_refused'), done ? 'success' : 'error');
  };
  return (
    <div className="turn-copied" data-testid="assistant-copied">
      <p>
        <Icon icon={turn.copied ? Check : ClipboardCopy} size={15} />
        <span>{t(turn.copied ? 'assistant.panel.copy_paste_done' : 'assistant.panel.copy_paste_manual', { count: turn.prompt.length })}</span>
      </p>
      <div className="reply-actions">
        <button type="button" className="reply-action" data-testid="assistant-copy-prompt" onClick={() => void again()}><Icon icon={Copy} size={14} />{t('assistant.panel.copy_prompt')}</button>
      </div>
      <textarea className="copied-reply" data-testid="assistant-pasted" rows={5} value={pasted} placeholder={t('assistant.panel.paste_reply_here')}
        aria-label={t('assistant.panel.paste_reply_here')} onChange={(event) => { setPasted(event.target.value); }} />
      <div className="reply-actions">
        <button type="button" className="reply-action is-primary" data-testid="assistant-read-pasted" disabled={running || pasted.trim() === ''}
          onClick={() => void context.store.getState().paste(turn.id, pasted)}>
          <Icon icon={Check} size={14} />{t('assistant.panel.read_reply')}
        </button>
      </div>
    </div>
  );
}
