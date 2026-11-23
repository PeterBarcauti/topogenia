// useSheetState.ts
// The state of the sheet panel of one step: the sheet read, the draft and the mode, the queue of its saving, the validation, the clarification, the person responsible, the writes of the documents one at a time (annex B 4).

import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { trackWrites } from '../../../shared/state/pendingWrites.ts';
import { boundOutOfOrder, draftOf, EMPTY_DRAFT, onlyFreeText, summaryOf, bodyOf, type SheetDraft } from '../model/draft.ts';
import type { SheetPanelProps, SheetValue } from '../SheetContext.tsx';
import { createAutoSave, type AutoSave } from './autoSave.ts';

type SheetView = Schema<'SheetView'>;

/** « Formulaire » or « Écrire librement » (B-4-17). */
export type SheetMode = 'form' | 'free';

/**
 * The queue of the saving of a step, made once for the panel: what is left leaves when the panel closes or the step
 * changes (B-4-45), and the browser asks before the tab closes while something typed is not on the host (B-4-46).
 */
function useAutoSave(stepId: string, onView: (view: SheetView) => void, onFailure: (failure: unknown) => void): AutoSave {
  const [save] = useState(() => createAutoSave({
    save: (body, version) => api.put('/api/steps/{id}/sheet', { path: { id: stepId }, body, version }),
    read: () => api.get('/api/steps/{id}/sheet', { path: { id: stepId } }),
  }, null));
  useEffect(() => {
    save.listen({ onView, onFailure });
  });
  useEffect(() => () => void save.flush(), [save]);
  // The command « Quit » of the desktop mode sends what waits before the host stops (spec 5.2).
  useEffect(() => trackWrites(save), [save]);
  useEffect(() => {
    const leave = (event: BeforeUnloadEvent) => {
      if (!save.unsaved()) return;
      void save.flush();
      event.preventDefault();
    };
    window.addEventListener('beforeunload', leave);
    return () => {
      window.removeEventListener('beforeunload', leave);
    };
  }, [save]);
  return save;
}

/** Reads the sheet once: the typing starts from it, the free text alone opens its mode (B-4-19). */
function useLoading(props: SheetPanelProps, apply: (view: SheetView) => void, start: (draft: SheetDraft, mode: SheetMode) => void): void {
  const { step, notify } = props;
  const latest = useRef({ apply, start, notify });
  useEffect(() => {
    latest.current = { apply, start, notify };
  });
  useEffect(() => {
    let alive = true;
    api.get('/api/steps/{id}/sheet', { path: { id: step.id } }).then((read) => {
      if (!alive) return;
      latest.current.apply(read);
      latest.current.start(draftOf(read.content), onlyFreeText(read.content) ? 'free' : 'form');
    }, (failure: unknown) => {
      if (alive) latest.current.notify(messageOf(failure), 'error');
    });
    return () => {
      alive = false;
    };
  }, [step.id]);
}

/**
 * The writes of the documents in their order, and whether one is asked and not answered: a queue `pendingWrites` follows,
 * told at each write asked and answered (ADR 115).
 */
function createDocumentQueue() {
  let pending = 0;
  let queue: Promise<unknown> = Promise.resolve();
  const listeners = new Set<() => void>();
  const changed = () => {
    for (const listener of [...listeners]) listener();
  };
  return {
    unsaved: () => pending > 0,
    flush: () => queue.then(() => undefined),
    subscribe(listener: () => void) {
      const own = () => {
        listener();
      };
      listeners.add(own);
      return () => {
        listeners.delete(own);
      };
    },
    add<T>(write: () => Promise<T>): Promise<T> {
      pending += 1;
      changed();
      const next = queue.then(write).finally(() => {
        pending -= 1;
        changed();
      });
      queue = next;
      return next;
    },
  };
}

/**
 * The writes of the documents, one at a time (B-4-63): each leaves once the one before is answered (audit 3.1 V3, U1),
 * its sheet applied, the map read again; a refusal said. Busy while one is asked and not answered; followed with the
 * saving of the sheet, so that « Quit » and the action plan read again wait for them (ADR 112 point 5).
 */
function useDocumentWrites(props: SheetPanelProps, apply: (view: SheetView) => void) {
  const [asked, setAsked] = useState(0);
  const [writes] = useState(createDocumentQueue);
  useEffect(() => trackWrites(writes), [writes]);
  const run = (call: () => Promise<SheetView>): Promise<boolean> => {
    setAsked((count) => count + 1);
    return writes.add(async () => {
      try {
        apply(await call());
        props.onMapChanged();
        return true;
      } catch (failure) {
        props.notify(messageOf(failure), 'error');
        return false;
      } finally {
        setAsked((count) => count - 1);
      }
    });
  };
  return { busy: asked > 0, run };
}

export interface SheetState {
  readonly value: SheetValue | null;
  readonly mode: SheetMode;
  readonly setMode: (mode: SheetMode) => void;
}

export function useSheetState(props: SheetPanelProps): SheetState {
  const { t } = useTranslation();
  const [view, setView] = useState<SheetView | null>(null);
  const [draft, setDraft] = useState<SheetDraft>(EMPTY_DRAFT);
  const [mode, setMode] = useState<SheetMode>('form');
  const latest = useRef(props);
  useEffect(() => {
    latest.current = props;
  });
  // The sheet received before it is drawn: the version of a write that waited for another one (audit 3.1 V3, U1).
  const received = useRef<SheetView | null>(null);
  const show = useCallback((next: SheetView) => {
    received.current = next;
    setView(next);
    latest.current.onSummary(summaryOf(next));
  }, []);
  const save = useAutoSave(props.step.id, show, (failure) => {
    latest.current.notify(messageOf(failure), 'error');
  });
  const apply = useCallback((next: SheetView) => {
    show(next);
    save.know(next);
  }, [show, save]);
  useLoading(props, apply, (read, opened) => {
    setDraft(read);
    // Opened on the way to a pain point (F-118), the sheet shows its form, where the field is, whatever it holds: the free
    // text alone would show neither the inputs nor the field (R4-01); it stays one click away.
    setMode((latest.current.painFocus ?? null) === null ? opened : 'form');
  });
  const documents = useDocumentWrites(props, apply);
  const nameOf = (userId: string) => props.project === null ? '…'
    : props.project.members.find((member) => member.user_id === userId)?.display_name ?? t('sheet.former_member');
  const edit = (next: SheetDraft) => {
    setDraft(next);
    // A bound of the duration out of order, which the host refuses, waits for the keystroke that puts it in order; the
    // closing sends it all the same (ADR 095, M5 point 1; audit 3.1 V3, U2).
    save.change(bodyOf(next), { hold: boundOutOfOrder(next) !== null });
  };
  // The sheet read only (the view in BPMN 2.0, ADR 106): what is drawn in it reads its rights from the sheet, so the sheet
  // given to them has none, and no document of it reports a pain point; the writes of the panel keep the sheet as read
  // (audit of the copy 64, P1-03).
  const readOnly = props.readOnly === true;
  const value: SheetValue | null = view === null ? null : {
    props: readOnly ? { ...props, onReport: undefined } : props, view: readOnly ? { ...view, permissions: NO_RIGHT } : view,
    draft, edit, apply, save, runDocument: documents.run, documentBusy: documents.busy, received: () => received.current ?? view, nameOf,
  };
  return { value, mode, setMode };
}

const NO_RIGHT: Schema<'SheetPermissions'> = { fill: false, validate: false, request_clarification: false, assign: false };
