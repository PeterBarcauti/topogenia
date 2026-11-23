// SearchPanel.tsx
// The panel « Rechercher » docked on the right (ADR 110 point 5): the field, « 12 résultats » then « 3 sur 12 », previous and next, then the results; a result leads to its step on its map (the view in BPMN 2.0 left); Enter and Shift+Enter walk them, Escape closes the panel and gives the focus back to the map.

import { useEffect, useMemo, useRef, type KeyboardEvent, type RefObject } from 'react';
import { ChevronDown, ChevronUp, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { paths } from '../../../app/router.ts';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { useDock } from '../../../shared/ui/dock.ts';
import { FoldButton } from '../../../shared/ui/FoldButton.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { bpmnView } from '../bpmn/bpmnState.ts';
import { stepNode } from '../canvas/stepFocus.ts';
import { useLeadTo } from '../panels/useLeadTo.ts';
import { focusOnArrival } from '../state/arrival.ts';
import type { MapEditor } from '../state/editor.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';
import { SearchGroups, SearchMessage } from './SearchHits.tsx';
import { flatHits, nextIndex, subjectOf, type FoundHit } from './searchResults.ts';
import { searchStore } from './searchStore.ts';
import { askOf, closeSearch, searchNow, typeSearch } from './useSearch.ts';

type MapView = Schema<'MapView'>;

/** The focus given back to the map: the step chosen, else the first step drawn (on the canvas or in the view in BPMN 2.0). */
export function focusTheMap(editor: MapEditor): void {
  const chosen = editor.store.getState().selection.steps[0];
  const node = (chosen === undefined ? null : stepNode(chosen)) ?? document.querySelector<HTMLElement>('.react-flow__node-step, [data-testid="bpmn-node"]');
  node?.focus();
}

/** Takes the focus into the field when it is asked (Ctrl+F, the button), and once shown on the map a result led to while the field held it. */
function useFieldFocus(field: RefObject<HTMLInputElement | null>): void {
  const focusField = useStore(searchStore, (state) => state.focusField);
  useEffect(() => {
    if (!focusField) return;
    field.current?.focus();
    field.current?.select();
    searchStore.setState({ focusField: false });
  }, [field, focusField]);
  useEffect(() => {
    if (!searchStore.getState().refocus) return;
    field.current?.focus();
    searchStore.setState({ refocus: false });
  }, [field]);
}

/**
 * Going to a result: on this map its step chosen and put in view (F-64); on another map that map opened, which chooses it
 * on arrival, the field focused again there when it held the focus; over the view in BPMN 2.0, which cannot put a step in
 * view, the view left and the canvas shown choosing it on arrival. Walking: the next or the previous result, round the
 * list, the search sent at once when its reply is not there yet.
 */
function useSearchWalk(map: MapView, field: RefObject<HTMLInputElement | null>) {
  const editor = useEditor();
  const leadTo = useLeadTo(map);
  const live = useRef(true);
  useEffect(() => {
    live.current = true;
    return () => {
      live.current = false;
    };
  }, []);
  const lead = (found: FoundHit) => {
    const subject = subjectOf(found);
    const away = subject.mapId !== map.id;
    if (away) searchStore.setState({ refocus: document.activeElement === field.current });
    if (!bpmnView.getState().open) {
      leadTo(subject);
      return;
    }
    focusOnArrival(subject);
    bpmnView.getState().setOpen(false);
    if (away) editor.navigate(paths.map(subject.mapId));
  };
  const go = (rank: number) => {
    const found = flatHits(searchStore.getState().reply)[rank];
    if (found === undefined) return;
    searchStore.setState({ index: rank });
    lead(found);
  };
  const walk = (direction: 1 | -1) => {
    const step = () => {
      const { reply, index } = searchStore.getState();
      go(nextIndex(index, flatHits(reply).length, direction));
    };
    const ask = askOf(editor);
    if (searchStore.getState().reply !== null || ask === null) {
      step();
      return;
    }
    void searchNow(ask).then(() => {
      if (live.current) step();
    });
  };
  return { go, walk };
}

interface SearchBarProps {
  readonly field: RefObject<HTMLInputElement | null>;
  readonly count: number;
  readonly walk: (direction: 1 | -1) => void;
  readonly onClose: () => void;
}

/** The field, the place of the result current among them, previous and next; Enter the next, Shift+Enter the previous, Escape closes. */
function SearchBar({ field, count, walk, onClose }: SearchBarProps) {
  const { t } = useTranslation();
  const editor = useEditor();
  const text = useStore(searchStore, (state) => state.text);
  const index = useStore(searchStore, (state) => state.index);
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      onClose();
      return;
    }
    if (event.key !== 'Enter' || event.nativeEvent.isComposing) return;
    event.preventDefault();
    walk(event.shiftKey ? -1 : 1);
  };
  return (
    <div className="search-bar">
      <input ref={field} type="search" className="field-control search-field" data-testid="search-field" value={text} maxLength={100} aria-label={t('search.field')}
        placeholder={t('search.placeholder')} autoComplete="off" spellCheck={false} onKeyDown={onKeyDown}
        onChange={(event) => {
          const ask = askOf(editor);
          if (ask !== null) typeSearch(event.target.value, ask);
        }} />
      <span className="search-count" role="status">{count === 0 ? '' : index < 0 ? t('search.results', { count }) : t('search.position', { index: index + 1, count })}</span>
      <button type="button" className="icon-button" aria-label={t('search.previous')} title={t('search.previous')} disabled={count === 0} data-testid="search-previous"
        onClick={() => { walk(-1); }}>
        <Icon icon={ChevronUp} size={16} />
      </button>
      <button type="button" className="icon-button" aria-label={t('search.next')} title={t('search.next')} disabled={count === 0} data-testid="search-next"
        onClick={() => { walk(1); }}>
        <Icon icon={ChevronDown} size={16} />
      </button>
    </div>
  );
}

export function SearchPanel({ map }: { map: MapView }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const shown = useMapLanguage();
  const reply = useStore(searchStore, (state) => state.reply);
  const count = useMemo(() => flatHits(reply).length, [reply]);
  const field = useRef<HTMLInputElement>(null);
  const folded = useDock()?.folded === true;
  useFieldFocus(field);
  const { go, walk } = useSearchWalk(map, field);
  const close = () => {
    focusTheMap(editor);
    closeSearch();
  };
  // Escape from a result or a button of the panel: the layer of the panels, the keyboard manager leaving the field its keys.
  useKeyLayer('panel', (event, chord) => {
    if (event.type !== 'keydown' || chord !== 'Escape' || document.activeElement?.closest('[data-testid="search-panel"]') == null) return false;
    close();
    return true;
  }, !folded);
  const title = t('search.title');
  return (
    <aside className="side-panel search-panel" aria-label={title} data-testid="search-panel">
      <header className="side-panel-head">
        <div className="stream-panel-title"><h2>{title}</h2></div>
        <FoldButton />
        <button type="button" className="icon-button" aria-label={t('app.close')} title={t('app.close')} data-testid="search-close" onClick={close}>
          <Icon icon={X} size={16} />
        </button>
      </header>
      <div className="side-panel-body">
        <SearchBar field={field} count={count} walk={walk} onClose={close} />
        <SearchMessage count={count} />
        <SearchGroups mapId={map.id} shown={shown} onGo={go} />
      </div>
    </aside>
  );
}
