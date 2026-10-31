// Toolbar.tsx
// A toolbar drawn from commands in named groups: icon buttons with their name and shortcut, choices and switches, thin separators, one tab stop, arrows to move (spec 9.5).

import type { KeyboardEvent, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { CommandItem } from '../commands/commands.ts';
import { Icon } from './Icon.tsx';
import { useShortcutLabel } from './useShortcutLabel.ts';

export interface ToolbarGroup {
  readonly id: string;
  /** The i18n key of the name of the group (Tools, Shapes, Edit..., spec 9.5). */
  readonly label: string;
  readonly items: readonly CommandItem[];
  /** The i18n key of the name of the choice its checked items make: they are radio buttons (the tool, the shape of the next steps). */
  readonly choice?: string;
}

export interface ToolbarProps {
  readonly label: string;
  readonly testId: string;
  readonly groups: readonly ToolbarGroup[];
  readonly onRun: (id: string) => void;
  /** The commands shown as pressed besides those checked (the tool in use, a layer shown). */
  readonly pressed?: ReadonlySet<string>;
  /** What follows the groups on the same line (the button that folds the bar). */
  readonly end?: ReactNode;
}

/** Left and right move the focus among the buttons, Home and End to the ends (one tab stop for the whole bar). */
function onKeyDown(event: KeyboardEvent<HTMLDivElement>): void {
  const buttons = [...event.currentTarget.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')];
  const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
  const target = { ArrowRight: index + 1, ArrowLeft: index - 1, Home: 0, End: buttons.length - 1 }[event.key];
  if (target === undefined || buttons.length === 0) return;
  event.preventDefault();
  buttons[(target + buttons.length) % buttons.length]?.focus();
}

interface ButtonProps {
  readonly item: CommandItem;
  readonly testId: string;
  readonly first: boolean;
  readonly radio: boolean;
  readonly pressed: boolean | undefined;
  readonly onRun: (id: string) => void;
}

function ToolbarButton({ item, testId, first, radio, pressed, onRun }: ButtonProps) {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const name = t(item.label, item.labelValues);
  return (
    <button type="button" className="toolbar-button" disabled={item.disabled} tabIndex={first ? 0 : -1} aria-label={name}
      title={item.shortcut === undefined ? name : `${name} (${shortcut(item.shortcut)})`} data-testid={`${testId}-${item.id}`}
      role={radio ? 'radio' : undefined} aria-checked={radio ? item.checked === true : undefined} aria-pressed={radio ? undefined : pressed}
      onClick={() => {
        onRun(item.id);
      }}>
      {item.icon !== undefined ? <Icon icon={item.icon} size={17} strokeWidth={1.75} /> : name}
    </button>
  );
}

export function Toolbar({ label, testId, groups, onRun, pressed, end }: ToolbarProps) {
  const { t } = useTranslation();
  const first = groups.flatMap((group) => group.items).find((item) => !item.disabled)?.id;
  const button = (item: CommandItem, radio: boolean) => (
    <ToolbarButton key={item.id} item={item} testId={testId} first={item.id === first} radio={radio} onRun={onRun}
      pressed={item.checked ?? (pressed === undefined ? undefined : pressed.has(item.id))} />
  );
  return (
    <div className="toolbar" role="toolbar" aria-label={t(label)} data-testid={testId} onKeyDown={onKeyDown}>
      {groups.map((group) => {
        const choices = group.choice === undefined ? [] : group.items.filter((item) => item.checked !== undefined);
        const others = group.items.filter((item) => !choices.includes(item));
        return (
          <div key={group.id} className="toolbar-group" role="group" aria-label={t(group.label)} data-testid={`${testId}-group-${group.id}`}>
            <div className="toolbar-buttons">
              {choices.length > 0 && (
                <span className="toolbar-choice" role="radiogroup" aria-label={t(group.choice ?? group.label)}>{choices.map((item) => button(item, true))}</span>
              )}
              {others.map((item, index) => (
                <span key={item.id} className="toolbar-slot">
                  {(item.separatorBefore === true || (index === 0 && choices.length > 0)) && <span className="toolbar-sep" aria-hidden="true" />}
                  {button(item, false)}
                </span>
              ))}
            </div>
            <span className="toolbar-caption" aria-hidden="true">{t(group.label)}</span>
          </div>
        );
      })}
      {end}
    </div>
  );
}
