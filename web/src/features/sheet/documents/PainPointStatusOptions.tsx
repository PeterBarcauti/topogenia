// PainPointStatusOptions.tsx
// The status of a pain point chosen from a button, as in the task boards (ADR 116): the button shows the status alone and opens its list, each status with its help on its own line, dropped after a separator; the arrows move, only a choice writes, Escape closes; drawn by the sheet of the receiving step and by the lines of the action plan.

import { useCallback, useState, type KeyboardEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import type { CommandItem } from '../../../shared/commands/commands.ts';
import { capitalized } from '../../../shared/i18n/languageNames.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { Menu } from '../../../shared/ui/Menu.tsx';
import { STATUS_APART, statusChoices, type StatusCode } from '../model/painPointStatus.ts';

type Status = Schema<'PainPointStatus'>;

export interface StatusOptionsProps {
  /** The code of the pain point, in the names of the button and of its list. */
  readonly code: string;
  /** The status the host holds: the list offers the five and this one when another. */
  readonly held: Status;
  /** The status shown and marked: the one held, or the one chosen while it is written. */
  readonly shown: Status;
  readonly testId: string;
  readonly className: string;
  readonly disabled?: boolean;
  /** What the field of the sheet gives the button: its identifier and its help. */
  readonly id?: string;
  readonly describedBy?: string | undefined;
  /** A status other than the one shown, chosen by a click or by Enter: written once. */
  readonly onChoose: (status: StatusCode) => void;
}

/** A status as the button and its list write it: its name, its first letter in capitals, and its help. */
interface StatusTexts {
  readonly name: string;
  readonly help: string;
}

/**
 * The entries of the list: each status by its name and its help on its own line, seen by the mouse, the keyboard and the
 * touch alike, both read as its name; dropped after a separator; the one shown checked.
 */
function itemsOf(held: Status, shown: Status, textsOf: (status: StatusCode) => StatusTexts): CommandItem[] {
  return statusChoices(held).map((status) => {
    const { name, help } = textsOf(status);
    return {
      id: status, label: 'sheet.pain_point.status_choice', labelValues: { status: name, help }, lines: [name, help], icon: undefined, shortcut: undefined,
      disabled: false, checked: status === shown, separatorBefore: status === STATUS_APART,
    };
  });
}

/**
 * The status button: a click, Enter, Space or Alt+Down opens its list on the status shown, under the button; the arrows
 * move in it and write nothing; a click or Enter on a choice closes it and writes that choice once, none when it is the
 * status shown; Escape closes it and gives the focus back to the button (review M1). Its name holds the word it shows.
 */
export function PainPointStatusOptions({ code, held, shown, testId, className, disabled = false, id, describedBy, onChoose }: StatusOptionsProps) {
  const { t, i18n } = useTranslation();
  const [at, setAt] = useState<{ x: number; y: number } | null>(null);
  // One function for the life of the button: the menu follows the focus from its opening, not from each drawing.
  const close = useCallback(() => {
    setAt(null);
  }, []);
  const openUnder = (button: HTMLElement) => {
    const box = button.getBoundingClientRect();
    setAt({ x: box.left, y: box.bottom + 4 });
  };
  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key !== 'ArrowDown' || !event.altKey) return;
    event.preventDefault();
    openUnder(event.currentTarget);
  };
  const textsOf = (status: StatusCode): StatusTexts => ({
    name: capitalized(t(`sheet.pain_point.statuses.${status}`), i18n.language), help: t(`sheet.pain_point.status_helps.${status}`),
  });
  const name = shown === null ? '' : textsOf(shown).name;
  // A button disabled while its list is open closes it, adjusted while drawing: it never opens again by itself.
  if (disabled && at !== null) setAt(null);
  const open = at !== null && !disabled;
  return (
    <>
      <button type="button" id={id} className={`${className} status-button`} aria-haspopup="menu" aria-expanded={open} aria-describedby={describedBy}
        aria-label={t('sheet.pain_point.status_now', { code, status: name })} data-testid={testId} disabled={disabled} onKeyDown={onKeyDown}
        onClick={(event) => { openUnder(event.currentTarget); }}>
        <span className="status-button-text">{name}</span>
        <Icon icon={ChevronDown} size={14} />
      </button>
      {open && (
        <Menu label="sheet.pain_point.status_of" labelValues={{ code }} testId={`${testId}-menu`} items={itemsOf(held, shown, textsOf)} at={at} focusChecked
          onClose={close} onRun={(status) => {
            if (status !== shown) onChoose(status as StatusCode);
          }} />
      )}
    </>
  );
}
