// Procedure.tsx
// The procedure of the step: its operations in order, each with its tool and its nature; Enter adds the next line, Backspace removes an empty one, a handle, the buttons and Alt+arrows move a line (annex B 4, AR-89).

import { useEffect, useRef, useState, type DragEvent, type KeyboardEvent } from 'react';
import { ArrowDown, ArrowUp, GripVertical, Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { NATURES, type Nature, type ProcedureLine } from '../model/draft.ts';
import { insertLine, isBlank, moveLine, removeLine, shownLines } from '../model/procedure.ts';

/** The list of the tools offered to the tool of a line (B-4-31). */
export const TOOLS_LIST = 'sheet-tools-list';

interface RowProps {
  readonly line: ProcedureLine;
  readonly index: number;
  readonly count: number;
  /** The line under the operation dragged: where it would land. */
  readonly target: boolean;
  readonly onChange: (part: Partial<ProcedureLine>) => void;
  readonly onKeyDown: (event: KeyboardEvent<HTMLInputElement>) => void;
  readonly onMove: (to: number) => void;
  readonly onRemove: () => void;
  readonly register: (field: HTMLInputElement | null) => void;
  readonly drag: { readonly start: (event: DragEvent) => void; readonly over: (event: DragEvent) => void; readonly drop: () => void; readonly end: () => void };
}

/** A line: the handle left of its number, its text, its buttons shown on hover and focus, its tool and its nature; « Contrôler » in green (B-4-30 to B-4-34, B-4-38). */
function ProcedureRow({ line, index, count, target, onChange, onKeyDown, onMove, onRemove, register, drag }: RowProps) {
  const { t } = useTranslation();
  const n = index + 1;
  const classes = ['procedure-line', line.nature === 'check' ? 'is-check' : '', target ? 'is-target' : ''].filter((name) => name !== '').join(' ');
  const button = (label: string, icon: typeof X, disabled: boolean, run: () => void, testId: string) => (
    <button type="button" className="icon-button" aria-label={label} title={label} disabled={disabled} data-testid={testId} onClick={run}><Icon icon={icon} size={14} /></button>
  );
  return (
    <li className={classes} data-testid={`procedure-line-${String(n)}`} onDragOver={drag.over} onDrop={drag.drop}>
      <span className="procedure-handle" draggable title={t('sheet.operation.drag', { n })} aria-hidden="true" data-testid={`procedure-handle-${String(n)}`}
        onDragStart={drag.start} onDragEnd={drag.end}>
        <Icon icon={GripVertical} size={14} />
      </span>
      <span className="procedure-number" aria-hidden="true">{n}</span>
      <input ref={register} type="text" className="procedure-text" aria-label={t('sheet.operation.text', { n })} maxLength={1000}
        placeholder={index === 0 ? t('sheet.operation.example') : undefined} value={line.text} data-testid={`procedure-text-${String(n)}`}
        onChange={(event) => { onChange({ text: event.target.value }); }} onKeyDown={onKeyDown} />
      <span className="procedure-buttons">
        {button(t('sheet.operation.up', { n }), ArrowUp, index === 0, () => { onMove(index - 1); }, `procedure-up-${String(n)}`)}
        {button(t('sheet.operation.down', { n }), ArrowDown, index === count - 1, () => { onMove(index + 1); }, `procedure-down-${String(n)}`)}
        {button(t('sheet.operation.remove', { n }), X, count === 1 && isBlank(line), onRemove, `procedure-remove-${String(n)}`)}
      </span>
      <span className="procedure-meta">
        <input type="text" className="procedure-tool" list={TOOLS_LIST} aria-label={t('sheet.operation.tool', { n })} placeholder={t('sheet.operation.tool_example')}
          maxLength={200} value={line.tool} data-testid={`procedure-tool-${String(n)}`} onChange={(event) => { onChange({ tool: event.target.value }); }} />
        <select className="procedure-nature" aria-label={t('sheet.operation.nature', { n })} value={line.nature ?? ''} data-testid={`procedure-nature-${String(n)}`}
          onChange={(event) => { onChange({ nature: event.target.value === '' ? null : event.target.value as Nature }); }}>
          <option value="">{t('sheet.operation.nature_none')}</option>
          {NATURES.map((nature) => <option key={nature} value={nature}>{t(`sheet.natures.${nature}`)}</option>)}
        </select>
      </span>
    </li>
  );
}

/** The keys of the text of a line: Enter inserts the next one, Backspace on an empty one removes it, Alt+↑ and Alt+↓ move it (B-4-35, B-4-36, AR-89). */
function keyAction(event: KeyboardEvent<HTMLInputElement>, index: number, lines: readonly ProcedureLine[]): { lines: readonly ProcedureLine[]; focus: number } | null {
  if (event.key === 'Enter') return { lines: insertLine(lines, index + 1), focus: index + 1 };
  if (event.key === 'Backspace' && (lines[index]?.text ?? '') === '' && lines.length > 1) return { lines: removeLine(lines, index), focus: Math.max(0, index - 1) };
  if (event.altKey && (event.key === 'ArrowUp' || event.key === 'ArrowDown')) {
    const to = index + (event.key === 'ArrowUp' ? -1 : 1);
    return to < 0 || to >= lines.length ? null : { lines: moveLine(lines, index, to), focus: to };
  }
  return null;
}

/** The lines, always one at least (B-4-29); the focus follows a line inserted, removed or moved (B-4-37). */
export function Procedure({ lines: given, onChange }: { lines: readonly ProcedureLine[]; onChange: (lines: readonly ProcedureLine[]) => void }) {
  const { t } = useTranslation();
  const lines = shownLines(given);
  const fields = useRef<(HTMLInputElement | null)[]>([]);
  const focus = useRef<number | null>(null);
  const [dragged, setDragged] = useState<number | null>(null);
  const [target, setTarget] = useState<number | null>(null);
  useEffect(() => {
    if (focus.current === null) return;
    fields.current[focus.current]?.focus();
    focus.current = null;
  });
  const change = (next: readonly ProcedureLine[], focusAt: number | null) => {
    focus.current = focusAt;
    onChange(next);
  };
  const dragOf = (index: number): RowProps['drag'] => ({
    start: (event) => {
      event.dataTransfer.setData('text/plain', String(index));
      event.dataTransfer.effectAllowed = 'move';
      setDragged(index);
    },
    over: (event) => {
      if (dragged === null) return;
      event.preventDefault();
      setTarget(index);
    },
    drop: () => {
      if (dragged !== null) change(moveLine(lines, dragged, index), index);
      setDragged(null);
      setTarget(null);
    },
    end: () => {
      setDragged(null);
      setTarget(null);
    },
  });
  return (
    <div className="procedure">
      <ol className="procedure-lines" data-testid="procedure-lines">
        {lines.map((line, index) => (
          <ProcedureRow key={index} line={line} index={index} count={lines.length} target={target === index && dragged !== index} drag={dragOf(index)}
            register={(field) => { fields.current[index] = field; }}
            onChange={(part) => { change(lines.map((one, at) => (at === index ? { ...one, ...part } : one)), null); }}
            onMove={(to) => { change(moveLine(lines, index, to), to); }} onRemove={() => { change(removeLine(lines, index), Math.max(0, index - 1)); }}
            onKeyDown={(event) => {
              const action = keyAction(event, index, lines);
              if (action === null) return;
              event.preventDefault();
              change(action.lines, action.focus);
            }} />
        ))}
      </ol>
      <button type="button" className="add-line" data-testid="procedure-add" onClick={() => { change(insertLine(lines, lines.length), lines.length); }}>
        <Icon icon={Plus} size={14} />{t('sheet.operation.add')}
      </button>
    </div>
  );
}
