// Ribbon.tsx
// The ribbon of the map: named zones of icon buttons drawn from the commands, folded to a thin line by its button or Ctrl+F1 (annex B 2.1, spec 9.5, AR-90).

import { PanelTopClose, PanelTopOpen, Sparkles } from 'lucide-react';
import { useStore } from 'zustand';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { Toolbar } from '../../../shared/ui/Toolbar.tsx';
import { useShortcutLabel } from '../../../shared/ui/useShortcutLabel.ts';
import { ribbonGroups } from '../state/commands/mapCommands.ts';
import { runMapCommand, useCommandContext, useEditor } from '../state/EditorContext.tsx';

/** The button that folds or unfolds the ribbon, its name and its shortcut in its tooltip (AR-90). */
function FoldButton({ folded }: { folded: boolean }) {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const editor = useEditor();
  const name = t(folded ? 'ribbon.unfold' : 'ribbon.fold');
  return (
    <button type="button" className="ribbon-fold" aria-label={name} title={`${name} (${shortcut('Ctrl+F1')})`} aria-expanded={!folded} data-testid="ribbon-fold"
      onClick={() => {
        runMapCommand(editor, 'ribbon');
      }}>
      <Icon icon={folded ? PanelTopOpen : PanelTopClose} size={16} />
    </button>
  );
}

/**
 * The button of the assistant at the right margin of the ribbon, folded or not (spec 3.0 section 3.4): it opens and closes
 * the panel, as Ctrl+I; a reader sees it too, for the questions and the analyses.
 */
function AssistantButton() {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const editor = useEditor();
  const open = useStore(editor.assistant, (state) => state.open);
  const name = t('assistant.panel.name');
  return (
    <button type="button" className={`ribbon-assistant${open ? ' is-open' : ''}`} aria-pressed={open} data-testid="ribbon-assistant"
      title={`${name} (${shortcut('Ctrl+I')})`} aria-label={name}
      onClick={() => {
        runMapCommand(editor, 'assistant');
      }}>
      <Icon icon={Sparkles} size={17} />
    </button>
  );
}

export function Ribbon() {
  const editor = useEditor();
  const context = useCommandContext();
  if (context === null) return null;
  if (context.preferences.ribbonCollapsed)
    return (
      <div className="ribbon-folded" data-testid="ribbon-folded">
        <AssistantButton />
        <FoldButton folded />
      </div>
    );
  return (
    <Toolbar label="ribbon.name" testId="ribbon" groups={ribbonGroups(context)} end={<><AssistantButton /><FoldButton folded={false} /></>}
      onRun={(id) => {
        runMapCommand(editor, id);
      }} />
  );
}
