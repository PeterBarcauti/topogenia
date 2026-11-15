// LabelInput.tsx
// The name of a step or a lane typed in place: Enter validates, Shift+Enter goes to the line, Escape gives up, leaving validates, an empty text gives up; begun by a character typed, the caret after it (B-2.5-28 to B-2.5-32, AR-151).

import { useEffect, useRef, useState, type KeyboardEvent } from 'react';

export interface LabelInputProps {
  readonly initial: string;
  /** The name of the field for assistive technologies (« Renommer »). */
  readonly label: string;
  readonly max: number;
  readonly testId: string;
  /** The text was begun by a character typed on the step: the caret goes after it instead of choosing the whole text (AR-151). */
  readonly begun?: boolean;
  /** Shown in the empty field: the name of origin of a translation to type (ADR 083). */
  readonly placeholder?: string | undefined;
  readonly onDone: (text: string) => void;
  readonly onCancel: () => void;
}

export function LabelInput({ initial, label, max, testId, begun = false, placeholder, onDone, onCancel }: LabelInputProps) {
  const [text, setText] = useState(initial);
  const field = useRef<HTMLTextAreaElement>(null);
  const done = useRef(false);
  // Where the caret starts, fixed at the mount: a new label from the host while typing does not move it.
  const [caret] = useState(() => (begun ? initial.length : null));
  useEffect(() => {
    const input = field.current;
    input?.focus();
    if (caret === null) input?.select();
    else input?.setSelectionRange(caret, caret);
  }, [caret]);
  const finish = (keep: boolean) => {
    if (done.current) return;
    done.current = true;
    const typed = text.trim();
    if (keep && typed !== '') onDone(typed);
    else onCancel();
  };
  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    // The keys of the field are its own: neither the canvas nor React Flow sees them.
    event.stopPropagation();
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      finish(true);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      finish(false);
    }
  };
  return (
    <textarea ref={field} className="label-input nodrag nopan nowheel" value={text} maxLength={max} rows={2} aria-label={label} data-testid={testId} placeholder={placeholder}
      onChange={(event) => {
        setText(event.target.value);
      }}
      onKeyDown={onKeyDown}
      onBlur={() => {
        finish(true);
      }} />
  );
}
