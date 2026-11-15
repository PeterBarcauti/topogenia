// CommandBar.tsx
// The command bar of the map: flows typed by the codes of their steps, one a line, sent all or nothing; the refusal of the host line by line in the bar (annex B 2.13, B-2.5-33, B-2.5-34).

import { useEffect, useId, useRef, useState, type KeyboardEvent, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { Button } from '../../../shared/ui/Button.tsx';
import { runCommandBar, type CommandOutcome } from '../state/actions/commandBar.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';

/** The field grows with its lines, up to six (B-2.13-02). */
const MAX_ROWS = 6;

export const rowsOf = (text: string): number => Math.min(MAX_ROWS, text.split('\n').length);

type Refusal = Extract<CommandOutcome, { ok: false }>;

/** The refusal of the host in the bar: its message, then the message of each line refused (B-2.13-05). */
function RefusalShown({ refusal }: { refusal: Refusal }) {
  return (
    <div className="command-bar-error" role="alert" data-testid="command-bar-error">
      {refusal.message !== '' && <p>{refusal.message}</p>}
      {refusal.lines.length > 0 && (
        <ul>
          {refusal.lines.map((line, index) => <li key={index}>{line}</li>)}
        </ul>
      )}
    </div>
  );
}

/**
 * The life of the bar: opened by Ctrl+K or its button, its field takes the focus, given back when it closes; a click
 * elsewhere closes it; the shortcuts of the map are silent meanwhile (annex B 2.5). Gives whether it is still shown: closed
 * while the text is sent, it leaves alone what is open by then (the map of the reply is applied all the same).
 */
function useBarPresence(box: RefObject<HTMLDivElement | null>, field: RefObject<HTMLTextAreaElement | null>, onClose: () => void): RefObject<boolean> {
  const shown = useRef(false);
  useEffect(() => {
    shown.current = true;
    return () => {
      shown.current = false;
    };
  }, []);
  useKeyLayer('modal', () => false);
  useEffect(() => {
    const before = document.activeElement;
    field.current?.focus();
    return () => {
      if (before instanceof HTMLElement) before.focus();
    };
  }, [field]);
  useEffect(() => {
    const outside = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || box.current?.contains(event.target) !== true) onClose();
    };
    document.addEventListener('pointerdown', outside, true);
    return () => {
      document.removeEventListener('pointerdown', outside, true);
    };
  }, [box, onClose]);
  return shown;
}

export function CommandBar({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const code = useMapState((state) => state.map?.project_code ?? '');
  const [text, setText] = useState('');
  const [refusal, setRefusal] = useState<Refusal | null>(null);
  const [sending, setSending] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  const field = useRef<HTMLTextAreaElement>(null);
  const titleId = useId();
  const fieldId = useId();
  const shown = useBarPresence(box, field, onClose);

  const run = async () => {
    if (text.trim() === '' || sending) return;
    setSending(true);
    const outcome = await runCommandBar(editor, text);
    if (!shown.current) return;
    setSending(false);
    if (!outcome.ok) {
      setRefusal(outcome);
      return;
    }
    onClose();
    editor.notify(t('command.done', { count: outcome.count }), 'success');
  };

  const onFieldKey = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    // Enter sends, Shift+Enter goes to the next line (B-2.5-33, B-2.5-34).
    if (event.key !== 'Enter' || event.shiftKey) return;
    event.preventDefault();
    void run();
  };

  return (
    <div ref={box} className="command-bar" role="dialog" aria-labelledby={titleId} data-testid="command-bar"
      onKeyDown={(event) => {
        if (event.key !== 'Escape') return;
        event.stopPropagation();
        onClose();
      }}>
      <label id={titleId} htmlFor={fieldId} className="command-bar-title">{t('command.title')}</label>
      <textarea id={fieldId} ref={field} rows={rowsOf(text)} value={text} placeholder={t('command.placeholder', { code })} spellCheck={false} data-testid="command-bar-input"
        onChange={(event) => {
          setText(event.target.value);
        }}
        onKeyDown={onFieldKey} />
      <p className="muted">{t('command.help', { code })}</p>
      {refusal !== null && <RefusalShown refusal={refusal} />}
      <div className="command-bar-foot">
        <Button testId="command-bar-cancel" onClick={onClose}>{t('app.cancel')}</Button>
        <Button testId="command-bar-run" variant="primary" disabled={text.trim() === '' || sending} onClick={() => void run()}>{t('command.run')}</Button>
      </div>
    </div>
  );
}
