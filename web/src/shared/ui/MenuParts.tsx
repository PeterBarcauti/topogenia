// MenuParts.tsx
// The parts of a context menu as the validated mock-up draws them: an entry with its icon, its radio and its shortcut, a named group of choices, a grid of swatches with its caption (spec 9.5, ARIA 1.2).

import { useId, type CSSProperties } from 'react';
import { Check } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { CommandItem } from '../commands/commands.ts';
import { Icon } from './Icon.tsx';
import type { MenuGroup } from './menuBlocks.ts';
import { useShortcutLabel } from './useShortcutLabel.ts';

type Look = MenuGroup['look'] | 'plain';

interface EntryProps {
  readonly item: CommandItem;
  readonly testId: string;
  readonly onChoose: (item: CommandItem) => void;
  readonly look?: Look;
  /** The i18n key of a shorter text shown, the whole label staying the name of the entry. */
  readonly text?: string | undefined;
}

const Radio = ({ on }: { on: boolean }) => <span className={on ? 'menu-check is-on' : 'menu-check'} aria-hidden="true" />;

const IconOrSpace = ({ item }: { item: CommandItem }) => (item.icon !== undefined ? <Icon icon={item.icon} size={15} /> : <span className="menu-icon-space" />);

/** What stands before the label: the radio of a choice (and the colour of a lane), else the icon, or a space. */
function Lead({ item, look }: { item: CommandItem; look: Look }) {
  if (look === 'icon-radio') return <IconOrSpace item={item} />;
  if (item.checked === undefined) return <IconOrSpace item={item} />;
  return (
    <>
      <Radio on={item.checked} />
      {item.swatch !== undefined && <span className="menu-swatch-dot" style={{ '--swatch': item.swatch } as CSSProperties} aria-hidden="true" />}
    </>
  );
}

/** What stands at the end: the shortcut, else the radio of the type of a lane, or the shape of the type of a step. */
function Trail({ item, look }: { item: CommandItem; look: Look }) {
  const shortcut = useShortcutLabel();
  if (item.shortcut !== undefined) return <kbd className="menu-shortcut" aria-hidden="true">{shortcut(item.shortcut)}</kbd>;
  if (look === 'icon-radio') return <span className="menu-trail"><Radio on={item.checked === true} /></span>;
  if (look === 'radio-icon' && item.icon !== undefined) return <span className="menu-trail"><Icon icon={item.icon} size={18} strokeWidth={1.5} /></span>;
  return null;
}

/** The shortcut for assistive technologies, in the names of ARIA (`Control+X`). */
const keyShortcut = (chord: string | undefined) => (chord === undefined ? undefined : chord.replace(/\bCtrl\b/g, 'Control'));

export function MenuEntry({ item, testId, onChoose, look = 'plain', text }: EntryProps) {
  const { t } = useTranslation();
  const label = t(item.label, item.labelValues);
  const shown = text === undefined ? label : t(text);
  const className = ['menu-item', item.danger === true && 'menu-item--danger', item.separatorBefore === true && look === 'plain' && 'menu-item--group'].filter(Boolean).join(' ');
  return (
    <button type="button" role={item.checked === undefined ? 'menuitem' : 'menuitemradio'} aria-checked={item.checked} className={className} tabIndex={-1}
      aria-disabled={item.disabled} aria-keyshortcuts={keyShortcut(item.shortcut)} aria-label={shown === label ? undefined : label} data-testid={`${testId}-${item.id}`}
      onClick={() => {
        onChoose(item);
      }}>
      <Lead item={item} look={look} />
      <span className="menu-label">{shown}</span>
      <Trail item={item} look={look} />
    </button>
  );
}

/** The colours of a lane: a grid of four a row, the one chosen ticked, its name and its rank under the grid (validated mock-up). */
function Swatches({ group, items, testId, onChoose }: GroupProps) {
  const { t } = useTranslation();
  const chosen = items.findIndex((item) => item.checked === true);
  const name = chosen < 0 ? undefined : group.texts?.[items[chosen]?.id ?? ''];
  return (
    <>
      <div className="menu-swatches">
        {items.map((item) => (
          <button key={item.id} type="button" role="menuitemradio" aria-checked={item.checked === true} className="menu-swatch-button" tabIndex={-1} aria-disabled={item.disabled}
            aria-label={t(item.label, item.labelValues)} data-testid={`${testId}-${item.id}`}
            onClick={() => {
              onChoose(item);
            }}>
            <span className="menu-swatch" style={{ '--swatch': item.swatch, '--ink': group.inks?.[item.id] } as CSSProperties} aria-hidden="true">
              {item.checked === true && <Icon icon={Check} size={14} strokeWidth={2.75} />}
            </span>
          </button>
        ))}
      </div>
      {name !== undefined && group.caption !== undefined && (
        <div className="menu-swatch-name" aria-hidden="true"><b>{t(name)}</b><span>{t(group.caption, { index: chosen + 1, count: items.length })}</span></div>
      )}
    </>
  );
}

interface GroupProps {
  readonly group: MenuGroup;
  readonly items: readonly CommandItem[];
  readonly testId: string;
  readonly onChoose: (item: CommandItem) => void;
}

/** A group of choices named by its title (`aria-labelledby`) or by its name, after a separator when it opens a group of the menu. */
export function MenuGroupView({ group, items, testId, onChoose }: GroupProps) {
  const { t } = useTranslation();
  const title = useId();
  const naming = group.titled === true ? { 'aria-labelledby': title } : { 'aria-label': t(group.name) };
  return (
    <div role="group" {...naming} className={items[0]?.separatorBefore === true ? 'menu-group menu-group--separated' : 'menu-group'}>
      {group.titled === true && <div className="menu-title" id={title} aria-hidden="true">{t(group.name)}</div>}
      {group.look === 'swatches'
        ? <Swatches group={group} items={items} testId={testId} onChoose={onChoose} />
        : items.map((item) => <MenuEntry key={item.id} item={item} testId={testId} onChoose={onChoose} look={group.look} text={group.texts?.[item.id]} />)}
    </div>
  );
}
