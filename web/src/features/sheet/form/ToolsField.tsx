// ToolsField.tsx
// The tools of a step typed as pills (essai J10, asked by the project owner): each tool a pill with its cross, a tool added by typing then Enter, a comma or leaving the field, the last one removed by Backspace; under it, folded, the other tools of the map, each added by a click. The field stays the text it was: what is typed is in it at once, as in any field of the sheet.

import { useState, type KeyboardEvent } from 'react';
import { ChevronDown, ChevronRight, Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { offeredTools } from '../model/draft.ts';
import { distinctTools, joinTools, settledTools, toolTint, TOOLS_MAX, typedTools } from '../model/tools.ts';

export interface ToolsFieldProps {
  /** The field as the sheet keeps it: its tools, a comma between two. */
  readonly value: string;
  /** The tools cited on the map. */
  readonly suggestions: readonly string[];
  readonly onChange: (value: string) => void;
}

/** The other tools of the map, folded until asked: an aid to fill the sheet, not a content of it. */
function Offers({ offered, onAdd }: { offered: readonly string[]; onAdd: (tool: string) => void }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  if (offered.length === 0) return null;
  return (
    <div className="tool-offers">
      <button type="button" className="tool-offers-head" aria-expanded={open} data-testid="sheet-tool-offers" onClick={() => { setOpen(!open); }}>
        <Icon icon={open ? ChevronDown : ChevronRight} size={12} />
        {t('sheet.suggestion.tools')}
        <span className="count">{offered.length}</span>
      </button>
      {open && (
        <div className="suggestions" role="group" aria-label={t('sheet.suggestion.tools')} data-testid="sheet-tool-suggestions">
          {offered.map((tool) => (
            <button key={tool} type="button" className={`suggest is-tint-${String(toolTint(tool))}`} aria-label={t('sheet.suggestion.add_tool', { tool })}
              data-testid={`sheet-tool-suggestion-${tool}`} onClick={() => { onAdd(tool); }}>
              <Icon icon={Plus} size={12} />{tool}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export function ToolsField({ value, suggestions, onChange }: ToolsFieldProps) {
  const { t } = useTranslation();
  const [typing, setTyping] = useState('');
  const settled = settledTools(value, typing);
  // The field with these tools and what is being typed after them; false when it would pass its length, and nothing is written.
  const write = (tools: readonly string[], tail: string): boolean => {
    const text = joinTools([...tools, tail.trim()].filter((tool) => tool !== ''));
    if (text.length > TOOLS_MAX) return false;
    onChange(text);
    return true;
  };
  const type = (text: string) => {
    const typed = typedTools(text);
    if (write(distinctTools([...settled, ...typed.ended]), typed.typing)) setTyping(typed.typing);
  };
  const settle = () => {
    if (typing.trim() !== '') write(distinctTools([...settled, typing.trim()]), '');
    setTyping('');
  };
  const keyed = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      settle();
    } else if (event.key === 'Backspace' && typing === '' && settled.length > 0) write(settled.slice(0, -1), '');
  };
  return (
    <div className="sheet-tools">
      <div className="tool-field" data-testid="sheet-tools-field">
        {settled.map((tool, index) => (
          <span key={`${String(index)}-${tool}`} className={`tool-pill is-tint-${String(toolTint(tool))}`} data-testid={`sheet-tool-${tool}`}>
            {tool}
            <button type="button" aria-label={t('sheet.tool_remove', { tool })} data-testid={`sheet-tool-remove-${tool}`}
              onClick={() => { write(settled.filter((_, at) => at !== index), typing); }}>
              <Icon icon={X} size={11} />
            </button>
          </span>
        ))}
        <input type="text" className="tool-field-input" maxLength={TOOLS_MAX} value={typing} aria-label={t('sheet.tools')}
          placeholder={t('sheet.tools_add')} data-testid="sheet-tools" onChange={(event) => { type(event.target.value); }} onKeyDown={keyed} onBlur={settle} />
      </div>
      <Offers offered={offeredTools(suggestions, value)} onAdd={(tool) => { write(distinctTools([...settled, tool]), typing); }} />
    </div>
  );
}
