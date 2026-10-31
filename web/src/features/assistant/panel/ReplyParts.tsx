// ReplyParts.tsx
// The parts of a reply of the assistant: its texts with the codes of the objects as buttons, its questions with their choices, its findings, its proposal with what it holds (spec 3.0 section 3.4).

import type { ReactNode } from 'react';
import {
  AlertTriangle, CircleHelp, ClipboardCopy, Download, Eye, FolderPlus, Lightbulb, ListChecks, RotateCcw, Sparkles, TriangleAlert,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { download, exportFileName } from '../../../shared/download.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { AssistantReply, Translation } from './assistantStore.ts';
import { ProposalOnMap } from './ProposalPreview.tsx';
import type { AssistantObject, ThreadContext } from './threadContext.ts';

const escaped = (code: string) => code.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** A text of the model, its line ends kept, each code of an object of the project a button that shows it on the map. */
export function TextWithCodes({ text, objects, reveal }: { text: string; objects: readonly AssistantObject[]; reveal: ThreadContext['reveal'] }) {
  if (objects.length === 0 || reveal === undefined) return <>{text}</>;
  const byCode = new Map(objects.map((object) => [object.code, object]));
  const pattern = new RegExp(`\\b(${[...byCode.keys()].map(escaped).join('|')})\\b`, 'g');
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(pattern)) {
    const object = byCode.get(match[0]);
    if (object === undefined) continue;
    parts.push(text.slice(last, match.index));
    parts.push(<CodeChip key={`${String(match.index)}-${object.code}`} object={object} reveal={reveal} />);
    last = match.index + match[0].length;
  }
  parts.push(text.slice(last));
  return <>{parts}</>;
}

function CodeChip({ object, reveal }: { object: AssistantObject; reveal: NonNullable<ThreadContext['reveal']> }) {
  const { t } = useTranslation();
  return (
    <button type="button" className={`code-chip is-${object.kind}`} title={t('assistant.panel.show_object', { label: object.label })} data-testid="assistant-code"
      onClick={() => { reveal([object]); }}>
      {object.code}
    </button>
  );
}

/** The questions of the model, its choices adding an answer to the field, and the way to have a proposal anyway. */
export function Questions({ reply, context }: { reply: AssistantReply; context: ThreadContext }) {
  const { t } = useTranslation();
  const answer = (question: string, choice: string) => {
    const draft = context.store.getState().draft;
    const line = t('assistant.panel.answer_line', { question, choice });
    context.store.getState().setDraft(`${draft}${draft === '' || draft.endsWith('\n') ? '' : '\n'}${line}\n`);
  };
  return (
    <>
      <ol className="reply-questions">
        {reply.questions.map((question, index) => (
          <li key={index}>
            <p>{question.question}</p>
            {question.choices.length > 0 && (
              <div className="choices">
                {question.choices.map((choice) => (
                  <button key={choice} type="button" className="choice-chip" data-testid="assistant-choice" onClick={() => { answer(question.question, choice); }}>{choice}</button>
                ))}
              </div>
            )}
          </li>
        ))}
      </ol>
      {(context.home || context.status?.can_propose === true) && (
        <div className="reply-actions">
          <button type="button" className="reply-action is-primary" data-testid="assistant-generate-anyway"
            onClick={() => void context.store.getState().send(t('assistant.panel.generate_anyway_request'), 'create')}>
            <Icon icon={Sparkles} size={14} />{t('assistant.panel.generate_anyway')}
          </button>
        </div>
      )}
    </>
  );
}

const FINDING_ICONS: Readonly<Record<string, typeof Sparkles>> = {
  wait: AlertTriangle, re_entry: ClipboardCopy, repeated_task: RotateCcw, no_owner: CircleHelp, no_sheet: ListChecks, loop: RotateCcw, break: TriangleAlert, other: Lightbulb,
};

/** The findings of an analysis, each with its kind, its detail, its suggestion, and the way to show its objects. */
export function Findings({ reply, context }: { reply: AssistantReply; context: ThreadContext }) {
  const { t } = useTranslation();
  return (
    <ul className="reply-findings">
      {reply.findings.map((finding, index) => {
        const objects = reply.objects.filter((object) => finding.objects.includes(object.code));
        return (
          <li key={index} className={`finding is-${finding.kind}`} data-testid="assistant-finding">
            <div className="finding-head">
              <span className="finding-icon" aria-hidden="true"><Icon icon={FINDING_ICONS[finding.kind] ?? Lightbulb} size={14} /></span>
              <span className="finding-kind">{t(`assistant.panel.kinds.${finding.kind}`, { defaultValue: finding.kind })}</span>
              <strong className="finding-title">{finding.title}</strong>
            </div>
            <p className="finding-detail"><TextWithCodes text={finding.detail} objects={reply.objects} reveal={context.reveal} /></p>
            {finding.suggestion !== null && finding.suggestion !== '' && (
              <p className="finding-suggestion">
                <Icon icon={Lightbulb} size={13} />
                <span><TextWithCodes text={finding.suggestion} objects={reply.objects} reveal={context.reveal} /></span>
              </p>
            )}
            <div className="reply-actions">
              {objects.length > 0 && context.reveal !== undefined && (
                <button type="button" className="reply-action" data-testid="assistant-show-finding" onClick={() => context.reveal?.(objects)}>
                  <Icon icon={Eye} size={14} />{t('assistant.panel.show', { count: objects.length })}
                </button>
              )}
              {context.status?.can_propose === true && (
                <button type="button" className="reply-action" data-testid="assistant-correct-finding"
                  onClick={() => void context.store.getState().send(t('assistant.panel.correct_request', { title: finding.title, codes: finding.objects.join(', ') }), 'modify')}>
                  <Icon icon={Sparkles} size={14} />{t('assistant.panel.correct')}
                </button>
              )}
            </div>
          </li>
        );
      })}
    </ul>
  );
}

/** A proposal: what it holds, the points to check, the faults of its file, and the gestures that keep it. */
export function Proposal({ reply, context, translation }: { reply: AssistantReply; context: ThreadContext; translation?: Translation | undefined }) {
  const { t } = useTranslation();
  const proposal = reply.proposal;
  if (proposal === null) return null;
  const valid = proposal.faults.length === 0;
  const onMap = !context.home && valid && context.proposals !== undefined;
  const save = () => {
    download(new Blob([proposal.pmap], { type: 'application/json' }), exportFileName(proposal.project_code ?? 'IA', proposal.project_name || 'proposition', '.pmap'));
  };
  return (
    <div className="reply-proposal" data-testid="assistant-proposal">
      {/* A translation is summed up by its texts, in its preview. */}
      {valid && translation === undefined && (
        <dl className="proposal-counts">
          {(['maps', 'lanes', 'steps', 'flows', 'sheets'] as const).map((key) => (
            <div key={key}><dt>{t(`assistant.panel.counts.${key}`, { count: proposal[key] })}</dt><dd>{proposal[key]}</dd></div>
          ))}
        </dl>
      )}
      {proposal.uncertainties.length > 0 && (
        <div className="proposal-uncertain">
          <p><Icon icon={CircleHelp} size={14} />{t('assistant.panel.uncertainties')}</p>
          <ul>{proposal.uncertainties.map((text, index) => <li key={index}>{text}</li>)}</ul>
        </div>
      )}
      {!valid && (
        <div className="proposal-faults" role="alert">
          <p><Icon icon={TriangleAlert} size={14} />{t('assistant.panel.faults', { count: proposal.faults.length })}</p>
          <ul>{proposal.faults.map((fault, index) => <li key={index}><code>{fault.path || '—'}</code> {fault.message}</li>)}</ul>
        </div>
      )}
      {/* On a map, the preview and the application first; the new project and the file stay at hand. */}
      {onMap && <ProposalOnMap pmap={proposal.pmap} context={context} translation={translation} />}
      <div className="reply-actions">
        {valid && translation === undefined && context.createProject !== undefined && (
          <button type="button" className={`reply-action${onMap ? '' : ' is-primary'}`} data-testid="assistant-create-project" onClick={() => context.createProject?.(proposal.pmap)}>
            <Icon icon={FolderPlus} size={14} />{t(context.home ? 'assistant.panel.create_project' : 'assistant.panel.open_new_project')}
          </button>
        )}
        <button type="button" className="reply-action" data-testid="assistant-download" onClick={save}><Icon icon={Download} size={14} />{t('assistant.panel.download')}</button>
      </div>
    </div>
  );
}
