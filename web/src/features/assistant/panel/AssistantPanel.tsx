// AssistantPanel.tsx
// The panel of the assistant docked on the right of the map: its scope, the pill of the selection, the line that says where the data go, the thread and the field; the codes of a reply shown on the map (spec 3.0 sections 3.1, 3.4, 6).

import { useEffect, useMemo, useState } from 'react';
import { RotateCcw, ShieldCheck, ShieldOff, Sparkles, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { api } from '../../../api/client.ts';
import { paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { FoldButton } from '../../../shared/ui/FoldButton.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { notify } from '../../../shared/ui/toasts.ts';
import { useShortcutLabel } from '../../../shared/ui/useShortcutLabel.ts';
import { OpenPmapDialog } from '../../project/files/OpenPmapDialog.tsx';
import { useEditor, useMapState } from '../../map/state/EditorContext.tsx';
import type { StepMark } from '../../map/state/mapStore.ts';
import { AssistantThread, type ThreadContext } from './AssistantThread.tsx';
import type { AssistantDestination, AssistantReply, AssistantStatus } from './assistantStore.ts';
import { Composer } from './Composer.tsx';
import { chooseProcessLanguage } from '../../../shared/state/processLanguage.ts';

type AssistantObject = AssistantReply['objects'][number];

/** The line that says where the data of an exchange go (spec 3.0 section 6), or why they cannot. */
export function DestinationLine({ status, destination }: { status: AssistantStatus | null; destination?: AssistantDestination | null }) {
  const { t } = useTranslation();
  const shown = destination ?? status?.destination ?? null;
  if (status === null || shown === null) return null;
  const classification = t(`classification.${shown.classification ?? 'internal'}`);
  if (!status.allowed)
    return <p className="assistant-destination is-refused" data-testid="assistant-destination"><Icon icon={ShieldOff} size={14} />{t('assistant.panel.destination_refused', { classification })}</p>;
  if (status.mode !== 'model')
    return <p className="assistant-destination" data-testid="assistant-destination"><Icon icon={ShieldCheck} size={14} />{t('assistant.panel.destination_copy_paste')}</p>;
  const where = shown.connector === 'bedrock'
    ? t('assistant.panel.destination_bedrock', { region: shown.region ?? '' })
    : t('assistant.panel.destination_anthropic');
  return (
    <p className="assistant-destination" data-testid="assistant-destination">
      <Icon icon={ShieldCheck} size={14} />
      <span>{t('assistant.panel.destination', { where, model: shown.model ?? '', classification })}</span>
    </p>
  );
}

/** Shows the objects of a reply: the steps of this map selected and the first put in view; an object of another map is offered to open. */
function useReveal(): (objects: readonly AssistantObject[]) => void {
  const { t } = useTranslation();
  const editor = useEditor();
  return (objects) => {
    const map = editor.store.getState().map;
    if (map === null || objects.length === 0) return;
    const here = objects.filter((object) => object.map_id === map.id);
    const steps = new Set(here.filter((object) => object.kind === 'step').map((object) => object.id));
    for (const lane of here.filter((object) => object.kind === 'lane'))
      for (const step of map.steps) if (step.lane_id === lane.id) steps.add(step.id);
    if (steps.size > 0) {
      editor.store.setState({ selection: { steps: [...steps], flows: [] } });
      editor.canvas.reveal([...steps]);
    }
    const elsewhere = objects.find((object) => object.map_id !== map.id);
    if (here.length === 0 && elsewhere !== undefined)
      notify(t('assistant.panel.other_map', { code: elsewhere.code, label: elsewhere.label }), 'info', {
        label: t('assistant.panel.open_map'),
        run: () => { editor.navigate(paths.map(elsewhere.map_id)); },
      });
  };
}

/**
 * The preview of a proposal marks the steps it changes or removes; the one of a translation shows its texts in place, in
 * its language (ADR 083). « Appliquer » is a write of the map, undone by Ctrl+Z.
 */
function proposalsOf(editor: ReturnType<typeof useEditor>): NonNullable<ThreadContext['proposals']> {
  const unmark = () => {
    editor.store.setState({ marks: {} });
    editor.store.getState().preview(null);
  };
  return {
    async preview(pmap, translation) {
      const mapId = translation?.mapId ?? editor.store.getState().map?.id ?? '';
      const body = translation === undefined ? { pmap } : { pmap, language: translation.language };
      const preview = await api.post('/api/maps/{id}/assistant/preview', { path: { id: mapId }, body });
      if (translation !== undefined) {
        editor.store.setState({ marks: {} });
        editor.store.getState().preview({ language: translation.language, texts: Object.fromEntries((preview.translations ?? []).map((text) => [text.id, text.text])) });
        return preview;
      }
      const marks: Record<string, StepMark> = {};
      for (const change of preview.changes)
        if (change.kind === 'step' && change.id !== null && (change.action === 'changed' || change.action === 'removed')) marks[change.id] = change.action;
      editor.store.setState({ marks });
      return preview;
    },
    async apply(pmap, version, translation) {
      const body = translation === undefined ? { pmap } : { pmap, language: translation.language };
      const reply = await editor.write((map) => api.post('/api/maps/{id}/assistant/apply', { path: { id: translation?.mapId ?? map.id }, body, version }));
      // A translation applied shows the process in its language at once (ADR 083), its version of origin untouched: the
      // map stays in it when its preview ends.
      const map = editor.store.getState().map;
      if (reply !== null && translation !== undefined && map !== null) {
        chooseProcessLanguage(map.project_id, translation.language);
        editor.store.getState().show({ language: translation.language, origin: map.languages.origin, locked: true });
      }
      unmark();
      return reply !== null;
    },
    unmark,
  };
}

export function AssistantPanel() {
  const { t } = useTranslation();
  const editor = useEditor();
  const shortcut = useShortcutLabel();
  const store = editor.assistant;
  const status = useStore(store, (state) => state.status);
  const statusError = useStore(store, (state) => state.statusError);
  const turns = useStore(store, (state) => state.turns);
  const dropped = useStore(store, (state) => state.selectionDropped);
  const map = useMapState((state) => state.map);
  const selected = useMapState((state) => state.selection.steps.length);
  const reveal = useReveal();
  const [creating, setCreating] = useState<{ pmap: string; origin: string } | null>(null);
  const progressDestination = useMemo(() => {
    for (const turn of [...turns].reverse()) if (turn.kind === 'progress') return turn.destination;
    return null;
  }, [turns]);
  const empty = map === null || map.steps.length === 0;
  // The state is read again when the map becomes empty or filled: the tiles then offer to create, or to analyse.
  useEffect(() => {
    void store.getState().loadStatus();
  }, [store, empty]);
  const close = () => {
    store.getState().setOpen(false);
  };
  if (map === null) return null;
  const root = map.breadcrumb.length <= 1;
  const canPropose = status?.can_propose === true;
  const context: ThreadContext = {
    store, status, home: false, reveal,
    ...(editor.admin && canPropose ? { createProject: (pmap: string) => { setCreating({ pmap, origin: map.id }); } } : {}),
    ...(canPropose ? { proposals: proposalsOf(editor) } : {}),
  };
  const blocked = status !== null && !status.allowed;
  return (
    <aside className="side-panel assistant-panel" aria-label={t('assistant.panel.name')} data-testid="assistant-panel">
      <header className="side-panel-head assistant-head">
        <span className="assistant-orb" aria-hidden="true"><Icon icon={Sparkles} size={16} /></span>
        <div className="assistant-title">
          <h2>{t('assistant.panel.name')}</h2>
          <p className="assistant-scope" data-testid="assistant-scope">{root ? t('assistant.panel.scope_project') : t('assistant.panel.scope_map', { name: map.name })}</p>
        </div>
        {turns.length > 0 && (
          <button type="button" className="icon-button" data-testid="assistant-restart" title={t('assistant.panel.restart')} aria-label={t('assistant.panel.restart')}
            onClick={() => { store.getState().restart(); }}><Icon icon={RotateCcw} size={16} /></button>
        )}
        <FoldButton />
        <button type="button" className="icon-button" data-testid="assistant-close" title={`${t('app.close')} (${shortcut('Ctrl+I')})`} aria-label={t('app.close')} onClick={close}>
          <Icon icon={X} size={16} />
        </button>
      </header>
      <div className="assistant-context">
        {selected > 0 && (
          <span className={`selection-pill${dropped ? ' is-dropped' : ''}`} data-testid="assistant-selection">
            {dropped ? t('assistant.panel.selection_dropped') : t('assistant.panel.selection', { count: selected })}
            <button type="button" data-testid="assistant-selection-toggle" aria-label={t(dropped ? 'assistant.panel.selection_restore' : 'assistant.panel.selection_remove')}
              title={t(dropped ? 'assistant.panel.selection_restore' : 'assistant.panel.selection_remove')}
              onClick={() => { store.getState().dropSelection(!dropped); }}>
              {dropped ? <Icon icon={RotateCcw} size={12} /> : <Icon icon={X} size={12} />}
            </button>
          </span>
        )}
        <DestinationLine status={status} destination={progressDestination} />
        {statusError !== null && <p className="message-error" role="alert">{messageOf(statusError)}</p>}
      </div>
      <AssistantThread context={context} />
      <footer className="assistant-foot">
        <Composer store={store} disabled={blocked} onClose={close}
          placeholder={t(status?.mode === 'copy_paste' ? 'assistant.panel.placeholder_copy' : 'assistant.panel.placeholder')} />
        <p className="assistant-note">{t('assistant.panel.note')}</p>
      </footer>
      {creating !== null && <OpenPmapDialog content={creating.pmap} originMapId={creating.origin} onClose={() => { setCreating(null); }} />}
    </aside>
  );
}
