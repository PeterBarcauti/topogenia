// ProposalPreview.tsx
// A proposal of the assistant on the map open: its preview compared with the map (the changes listed, the steps touched marked on the map), « Appliquer » in one operation undone by Ctrl+Z, « Préciser », « Écarter »; a proposal that changes the level of steps only opens as a new project; a translation shows its texts on the map and sums them up, each to read again beside its original (spec 3.0 sections 3.1-5 to 7, 3.4, ADR 079, ADR 082, ADR 083).

import { useEffect, useRef, useState } from 'react';
import { Check, Eye, FolderPlus, Languages, Layers, MessageSquarePlus, Undo2, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { ownName } from '../../../shared/i18n/languageNames.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { ThreadContext } from './threadContext.ts';
import type { Translation } from './assistantStore.ts';

type Preview = Schema<'ProposalPreviewView'>;

/** What the card does with its proposal on the map: nothing yet, its preview shown, applied, or put aside. */
type State = { kind: 'idle' } | { kind: 'loading' } | { kind: 'preview'; preview: Preview } | { kind: 'applied' } | { kind: 'dismissed' } | { kind: 'failed'; message: string };

const ACTIONS = ['added', 'changed', 'removed'] as const;

export function ProposalOnMap({ pmap, context, translation }: { pmap: string; context: ThreadContext; translation?: Translation | undefined }) {
  const { t } = useTranslation();
  const [state, setState] = useState<State>({ kind: 'idle' });
  const proposals = context.proposals;
  // A card that leaves with its preview shown (the panel closed, the conversation started again) takes it off the map.
  const leave = useRef<(() => void) | null>(null);
  useEffect(() => {
    leave.current = state.kind === 'preview' ? (proposals?.unmark ?? null) : null;
  });
  useEffect(() => () => {
    leave.current?.();
  }, []);
  if (proposals === undefined || state.kind === 'dismissed') return state.kind === 'dismissed' ? <p className="proposal-note">{t('assistant.panel.dismissed')}</p> : null;
  const preview = async () => {
    setState({ kind: 'loading' });
    try {
      setState({ kind: 'preview', preview: await proposals.preview(pmap, translation) });
    } catch (failure) {
      setState({ kind: 'failed', message: messageOf(failure) });
    }
  };
  const apply = async (version: number) => {
    setState({ kind: 'loading' });
    const done = await proposals.apply(pmap, version, translation);
    setState(done ? { kind: 'applied' } : { kind: 'idle' });
  };
  const close = () => {
    proposals.unmark();
    setState({ kind: 'dismissed' });
  };
  return (
    <div className="proposal-on-map" data-testid="assistant-proposal-map">
      {state.kind === 'preview' && (translation !== undefined
        ? <TranslationReview preview={state.preview} language={translation.language} />
        : state.preview.mode === 'new_project'
          ? <NewProjectOnly preview={state.preview} pmap={pmap} context={context} />
          : <ChangeList preview={state.preview} />)}
      {state.kind === 'failed' && <p className="proposal-faults" role="alert">{state.message}</p>}
      {state.kind === 'applied' ? (
        <p className="proposal-applied" data-testid="assistant-applied"><Icon icon={Undo2} size={14} />{t('assistant.panel.applied')}</p>
      ) : (
        <div className="reply-actions">
          {state.kind !== 'preview' && (
            <button type="button" className="reply-action is-primary" data-testid="assistant-preview" disabled={state.kind === 'loading'} onClick={() => void preview()}>
              <Icon icon={Eye} size={14} />{t('assistant.panel.preview')}
            </button>
          )}
          {state.kind === 'preview' && !state.preview.empty && state.preview.mode === 'in_place' && (translation === undefined || (state.preview.translations ?? []).length > 0) && (
            <button type="button" className="reply-action is-primary" data-testid="assistant-apply" onClick={() => void apply(state.preview.structure_version)}>
              <Icon icon={Check} size={14} />{t('assistant.panel.apply')}
            </button>
          )}
          {/* A translation is asked again from the dialog of the translations, not refined in words. */}
          {translation === undefined && (
            <button type="button" className="reply-action" data-testid="assistant-refine" onClick={() => { context.store.getState().choose('modify', t('assistant.panel.refine_example')); }}>
              <Icon icon={MessageSquarePlus} size={14} />{t('assistant.panel.refine')}
            </button>
          )}
          <button type="button" className="reply-action" data-testid="assistant-dismiss" onClick={close}><Icon icon={X} size={14} />{t('assistant.panel.dismiss')}</button>
        </div>
      )}
    </div>
  );
}

/**
 * A proposal that brings steps up a level or moves them to another map (ADR 082): no operation of the map does it in
 * place, so it opens as a new project and the map of origin stays as it is (decision of the owner of the product). The
 * card says why, which steps of the map and its levels the new project would not hold, that the steps kept carry their
 * sheets and translations, and offers the opening; a facilitator who is no administrator downloads the file.
 */
function NewProjectOnly({ preview, pmap, context }: { preview: Preview; pmap: string; context: ThreadContext }) {
  const { t } = useTranslation();
  const codes = (kind: 'raised' | 'moved') => preview.reasons.filter((reason) => reason.startsWith(`${kind}:`)).map((reason) => reason.slice(kind.length + 1));
  const raised = codes('raised');
  const moved = codes('moved');
  return (
    <div className="proposal-new-project" data-testid="assistant-new-project">
      <p className="new-project-why"><Icon icon={Layers} size={14} />{t('assistant.panel.new_project.why')}</p>
      {raised.length > 0 && <p className="new-project-codes">{t('assistant.panel.new_project.raised', { codes: raised.join(', ') })}</p>}
      {moved.length > 0 && <p className="new-project-codes">{t('assistant.panel.new_project.moved', { codes: moved.join(', ') })}</p>}
      {preview.missing.length > 0 ? (
        <>
          <p className="new-project-codes">{t('assistant.panel.new_project.missing', { count: preview.missing.length })}</p>
          <ul className="proposal-changes" data-testid="assistant-new-project-missing">
            {preview.missing.map((loss) => (
              <li key={loss.code} className="change is-removed">
                <code>{loss.code}</code>
                <span className="change-text">{loss.label}</span>
                {loss.detailed && <span className="change-fields">{t(loss.has_sheet ? 'assistant.panel.new_project.detailed_sheet' : 'assistant.panel.new_project.detailed')}</span>}
              </li>
            ))}
          </ul>
        </>
      ) : <p className="new-project-codes">{t('assistant.panel.new_project.complete')}</p>}
      <p className="proposal-note">{t('assistant.panel.new_project.carried')}</p>
      {context.createProject !== undefined ? (
        <button type="button" className="reply-action is-primary" data-testid="assistant-new-project-open" onClick={() => context.createProject?.(pmap)}>
          <Icon icon={FolderPlus} size={14} />{t('assistant.panel.open_new_project')}
        </button>
      ) : <p className="proposal-note">{t('assistant.panel.new_project.ask_admin')}</p>}
    </div>
  );
}

/**
 * A translation previewed (ADR 083): the map shows its texts in place; the card says how many texts it writes and how
 * many translations typed by hand stay, and lists each text beside its original, to read them again.
 */
function TranslationReview({ preview, language }: { preview: Preview; language: string }) {
  const { t } = useTranslation();
  const texts = preview.translations ?? [];
  const name = ownName(language);
  if (texts.length === 0) return <p className="proposal-note" data-testid="assistant-translation-empty">{t('assistant.panel.translation.empty', { language: name })}</p>;
  const kept = texts.filter((text) => text.kept).length;
  return (
    <div className="proposal-translation" data-testid="assistant-translation">
      <p className="translation-summary"><Icon icon={Languages} size={14} />{t('assistant.panel.translation.summary', { count: texts.length, language: name })}</p>
      {kept > 0 && <p className="proposal-note" data-testid="assistant-translation-kept">{t('assistant.panel.translation.kept', { count: kept })}</p>}
      <details className="translation-review">
        <summary data-testid="assistant-translation-review">{t('assistant.panel.translation.review')}</summary>
        <table>
          <thead><tr><th scope="col">{t('assistant.panel.translation.original')}</th><th scope="col">{name}</th></tr></thead>
          <tbody>
            {texts.map((text) => (
              <tr key={`${text.kind}-${text.id}`} className={text.kept ? 'is-kept' : undefined}>
                <td><code>{text.code}</code> {text.original}</td>
                <td lang={language}>{text.text}{text.kept && <span className="translation-kept">{t('assistant.panel.translation.kept_mark')}</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </div>
  );
}

function ChangeList({ preview }: { preview: Preview }) {
  const { t } = useTranslation();
  if (preview.empty) return <p className="proposal-note" data-testid="assistant-preview-empty">{t('assistant.panel.preview_empty')}</p>;
  return (
    <ul className="proposal-changes" data-testid="assistant-changes">
      {ACTIONS.flatMap((action) => preview.changes.filter((change) => change.action === action).map((change, index) => (
        <li key={`${action}-${String(index)}`} className={`change is-${action}`}>
          <span className="change-action">{t(`assistant.panel.change_actions.${action}`)}</span>
          <span className="change-kind">{t(`assistant.panel.change_kinds.${change.kind}`)}</span>
          {change.code !== null && <code>{change.code}</code>}
          <span className="change-text">{change.text}</span>
          {change.fields.length > 0 && <span className="change-fields">{change.fields.map((field) => t(`assistant.panel.change_fields.${field}`)).join(', ')}</span>}
        </li>
      )))}
      {preview.warnings.map((warning) => <li key={warning} className="change is-warning">{t(`assistant.panel.warnings.${warning.split(':')[0] ?? ''}`, { code: warning.split(':')[1] ?? '' })}</li>)}
    </ul>
  );
}
