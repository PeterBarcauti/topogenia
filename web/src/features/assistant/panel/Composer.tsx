// Composer.tsx
// The field of the request of the panel of the assistant: four lines shown at once, growing with the text up to twelve then scrolling, its icons to join files, copy, paste, read a text file and clear, the chip of the practice chosen, the pills of the files joined, the files dropped or pasted, Enter to send (spec 3.0 section 3.4, wishes of the owner of the product of 30/10/2026, lot 4).

import { useLayoutEffect, useRef, type ChangeEvent, type KeyboardEvent, type RefObject } from 'react';
import { ArrowUp, ClipboardPaste, Copy, Eraser, FileText, Square, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore, type StoreApi } from 'zustand';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { notify } from '../../../shared/ui/toasts.ts';
import { AttachButton, CopyPasteHint, FilePills, pastedFiles, useFileDrop, useFileJoin } from './Attachments.tsx';
import { REQUEST_MAX, type AssistantState } from './assistantStore.ts';
import { copyText } from './threadContext.ts';

/** The lines shown at once and the most shown before the field scrolls. */
export const COMPOSER_LINES = { min: 4, max: 12 } as const;

/** The largest text file read into the field (a prompt prepared in a file), in characters as the host bounds a request. */
export const FILE_MAX = REQUEST_MAX;

/** The field, grown with its text between its least and its most lines; beyond, it scrolls. */
function useGrowingField(text: string): RefObject<HTMLTextAreaElement | null> {
  const field = useRef<HTMLTextAreaElement>(null);
  useLayoutEffect(() => {
    const element = field.current;
    if (element === null) return;
    const style = getComputedStyle(element);
    const line = Number.parseFloat(style.lineHeight) || 20;
    const padding = (Number.parseFloat(style.paddingTop) || 0) + (Number.parseFloat(style.paddingBottom) || 0);
    element.style.height = 'auto';
    const wanted = Math.min(Math.max(element.scrollHeight, line * COMPOSER_LINES.min + padding), line * COMPOSER_LINES.max + padding);
    element.style.height = `${String(Math.ceil(wanted))}px`;
    element.style.overflowY = element.scrollHeight > wanted + 1 ? 'auto' : 'hidden';
  }, [text]);
  return field;
}

/** What the icons of the field do: copy its text, paste where the caret is, read a text file there (chosen or dropped). */
function useFieldTools(store: StoreApi<AssistantState>, field: RefObject<HTMLTextAreaElement | null>) {
  const { t } = useTranslation();
  /** Puts a text where the caret is, in the place of the text chosen, and the caret after it. */
  const put = (text: string) => {
    const element = field.current;
    const draft = store.getState().draft;
    const start = element?.selectionStart ?? draft.length;
    const end = element?.selectionEnd ?? draft.length;
    store.getState().setDraft((draft.slice(0, start) + text + draft.slice(end)).slice(0, REQUEST_MAX));
    const caret = start + text.length;
    requestAnimationFrame(() => {
      field.current?.focus();
      field.current?.setSelectionRange(caret, caret);
    });
  };
  /** Reads a text file (a prompt prepared, chosen or dropped) where the caret is. */
  const readFile = async (file: File) => {
    try {
      const text = await file.text();
      if (text.length > FILE_MAX) notify(t('assistant.panel.file_too_large', { max: FILE_MAX }), 'error');
      else {
        // Read as UTF-8 by the browser, which leaves out the mark of the order of bytes a file of Windows may open with.
        put(text);
        notify(t('assistant.panel.file_read', { name: file.name }), 'success');
      }
    } catch {
      notify(t('assistant.panel.file_unreadable'), 'error');
    }
  };
  return {
    readFile,
    async copy() {
      const done = await copyText(store.getState().draft);
      notify(t(done ? 'assistant.panel.copied' : 'assistant.panel.copy_refused'), done ? 'success' : 'error');
    },
    async paste() {
      try {
        put(await navigator.clipboard.readText());
      } catch {
        // The browser may refuse to read the clipboard to the page: Ctrl+V in the field still pastes.
        notify(t('assistant.panel.paste_refused'), 'info');
        field.current?.focus();
      }
    },
    async read(event: ChangeEvent<HTMLInputElement>) {
      const file = event.target.files?.[0];
      event.target.value = '';
      if (file !== undefined) await readFile(file);
    },
    clear() {
      store.getState().setDraft('');
      field.current?.focus();
    },
  };
}

/** A button of the bar of the field, an icon with its name in its tooltip. */
function Tool({ id, label, icon, disabled, onClick }: { id: string; label: string; icon: typeof Copy; disabled: boolean; onClick: () => void }) {
  return (
    <button type="button" className="composer-tool" data-testid={`assistant-${id}`} title={label} aria-label={label} disabled={disabled} onClick={onClick}>
      <Icon icon={icon} size={16} />
    </button>
  );
}

export interface ComposerProps {
  readonly store: StoreApi<AssistantState>;
  /** The project may not be sent to the AI: nothing can be written. */
  readonly disabled?: boolean;
  readonly placeholder: string;
  /** Ctrl+I closes the panel from the field too. */
  readonly onClose?: () => void;
}

export function Composer({ store, disabled = false, placeholder, onClose }: ComposerProps) {
  const { t } = useTranslation();
  const draft = useStore(store, (state) => state.draft);
  const intent = useStore(store, (state) => state.intent);
  const running = useStore(store, (state) => state.running);
  const files = useStore(store, (state) => state.files);
  const copying = useStore(store, (state) => state.mode === 'copy_paste');
  const field = useGrowingField(draft);
  const chooser = useRef<HTMLInputElement>(null);
  const tools = useFieldTools(store, field);
  const join = useFileJoin(store);
  const drop = useFileDrop(join, tools.readFile, disabled);
  const send = () => {
    if (!running && draft.trim() !== '') void store.getState().send();
  };
  const keyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      send();
    } else if (event.key.toLowerCase() === 'i' && event.ctrlKey && !event.altKey && !event.shiftKey && onClose) {
      event.preventDefault();
      onClose();
    }
  };
  return (
    <div className={`composer${running ? ' is-running' : ''}${drop.over ? ' is-dropping' : ''}`} data-testid="assistant-composer"
      onDragOver={drop.onDragOver} onDragLeave={drop.onDragLeave} onDrop={drop.onDrop}>
      {drop.over && <div className="composer-drop" data-testid="assistant-drop" aria-hidden="true">{t('assistant.panel.files.drop')}</div>}
      {intent !== null && (
        <div className="composer-intent">
          <span className="intent-chip" data-testid="assistant-intent">
            {t(`assistant.panel.tiles.${intent}.title`)}
            <button type="button" data-testid="assistant-intent-remove" aria-label={t('assistant.panel.intent_remove')} title={t('assistant.panel.intent_remove')} onClick={() => { store.getState().clearIntent(); }}>
              <Icon icon={X} size={12} />
            </button>
          </span>
        </div>
      )}
      <FilePills files={files} onRemove={(id) => { store.getState().removeFile(id); }} />
      {copying && <CopyPasteHint files={files} />}
      <textarea ref={field} className="composer-field" data-testid="assistant-field" rows={COMPOSER_LINES.min} value={draft} maxLength={REQUEST_MAX}
        placeholder={placeholder} aria-label={t('assistant.panel.field')} disabled={disabled} spellCheck
        onChange={(event) => { store.getState().setDraft(event.target.value); }} onKeyDown={keyDown} onPaste={pastedFiles(join)} />
      <div className="composer-bar">
        <div className="composer-tools" role="toolbar" aria-label={t('assistant.panel.tools')}>
          <AttachButton store={store} join={join} disabled={disabled || running} />
          <Tool id="copy" label={t('assistant.panel.copy')} icon={Copy} disabled={draft === ''} onClick={() => void tools.copy()} />
          <Tool id="paste" label={t('assistant.panel.paste')} icon={ClipboardPaste} disabled={disabled} onClick={() => void tools.paste()} />
          <Tool id="open-file" label={t('assistant.panel.open_file')} icon={FileText} disabled={disabled} onClick={() => chooser.current?.click()} />
          <input ref={chooser} type="file" hidden accept=".txt,.md,.markdown,text/plain,text/markdown" data-testid="assistant-file" aria-label={t('assistant.panel.open_file')}
            onChange={(event) => void tools.read(event)} />
          <Tool id="clear" label={t('assistant.panel.clear')} icon={Eraser} disabled={draft === ''} onClick={() => { tools.clear(); }} />
          {draft.length > REQUEST_MAX * 0.8 && <span className="composer-count" data-testid="assistant-count">{t('assistant.panel.count', { count: draft.length, max: REQUEST_MAX })}</span>}
        </div>
        {running
          ? <button type="button" className="composer-send is-stop" data-testid="assistant-stop" title={t('assistant.panel.stop')} aria-label={t('assistant.panel.stop')}
              onClick={() => { store.getState().stop(); }}><Icon icon={Square} size={14} /></button>
          : <button type="button" className="composer-send" data-testid="assistant-send" title={t('assistant.panel.send')} aria-label={t('assistant.panel.send')}
              disabled={disabled || draft.trim() === ''} onClick={send}><Icon icon={ArrowUp} size={18} /></button>}
      </div>
    </div>
  );
}
