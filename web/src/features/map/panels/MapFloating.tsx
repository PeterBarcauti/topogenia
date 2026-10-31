// MapFloating.tsx
// The floating element open over the map: the menu of its object or the File menu, the field of the label of a flow, the questions before a deletion, the command bar, the dialogs of the File menu, the stored remainder, the RACI panel and matrix, the translations (B-7-19).

import { useCallback, useEffect, useRef, useState, type SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { menuItemsOf, type MenuLine } from '../../../shared/commands/commands.ts';
import { Menu, type MenuGroup } from '../../../shared/ui/Menu.tsx';
import { DuplicateDialog } from '../../project/files/DuplicateDialog.tsx';
import { OpenPmapDialog } from '../../project/files/OpenPmapDialog.tsx';
import { ExportPackageDialog } from '../../project/package/ExportPackageDialog.tsx';
import { ImportPackageDialog } from '../../project/package/ImportPackageDialog.tsx';
import { projectOf } from '../state/actions/files.ts';
import { FLOW_LABEL_MAX } from '../state/actions/flows.ts';
import { flowTyped, relabelFlowIn } from '../state/actions/rename.ts';
import { contextOf, type MapContext } from '../state/commands/context.ts';
import { FILE_MENU, FLOW_MENU, LANE_MENU, LANE_MENU_GROUPS, MAP_COMMANDS, PANE_MENU, SELECTION_MENU, STEP_MENU, STEP_MENU_GROUPS } from '../state/commands/mapCommands.ts';
import { runMapCommand, useEditor, useMapState } from '../state/EditorContext.tsx';
import type { MenuTarget } from '../state/floating.ts';
import { ArrangementDialog } from './ArrangementDialog.tsx';
import { CommandBar } from './CommandBar.tsx';
import { DetailDeleteDialog, LaneDialog, MacroDialog, SelectionDeleteDialog } from './DeletionDialogs.tsx';
import { RaciMatrixDialog } from './RaciMatrixDialog.tsx';
import { RaciPanel } from './RaciPanel.tsx';
import { StreamEntryField } from './StreamEntryField.tsx';
import { ObjectTranslations, ProjectTranslations } from './TranslationDialogs.tsx';

/** The lines, the groups of choices and the name of the menu of each kind of object, and of the File menu (annex B 2.4). */
const MENUS: Readonly<Record<MenuTarget['kind'], { lines: readonly MenuLine<MapContext>[]; label: string; groups?: readonly MenuGroup[] }>> = {
  step: { lines: STEP_MENU, label: 'menu.step', groups: STEP_MENU_GROUPS },
  selection: { lines: SELECTION_MENU, label: 'menu.selection' },
  pane: { lines: PANE_MENU, label: 'menu.pane' },
  flow: { lines: FLOW_MENU, label: 'menu.flow' },
  lane: { lines: LANE_MENU, label: 'menu.lane', groups: LANE_MENU_GROUPS },
  file: { lines: FILE_MENU, label: 'menu.file' },
};

function ContextMenu({ target, at, onClose }: { target: MenuTarget; at: { x: number; y: number }; onClose: () => void }) {
  const editor = useEditor();
  const context = contextOf(editor, target);
  const menu = MENUS[target.kind];
  if (context === null) return null;
  const items = menuItemsOf(MAP_COMMANDS, menu.lines, context);
  if (items.length === 0) return null;
  return <Menu label={menu.label} testId={`menu-${target.kind}`} items={items} groups={menu.groups} at={at} onClose={onClose} onRun={(id) => { runMapCommand(editor, id, target); }} />;
}

/** The floating field of the label of a flow: Enter validates, Escape or a click elsewhere gives up; 60 characters at most (B-2.3-33, B-2.3-34). */
function FlowLabelInput({ flowId, at, onClose }: { flowId: string; at: { x: number; y: number }; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  // In the language shown: the label of origin in its own language, else its translation into the language shown (ADR 083).
  const initial = useMapState((state) => (state.map === null ? '' : flowTyped(editor, state.map, flowId)));
  const [text, setText] = useState(initial);
  const field = useRef<HTMLInputElement>(null);
  // Opened by the person's own gesture (double click or menu): the field takes the focus at once.
  useEffect(() => {
    field.current?.focus();
    field.current?.select();
  }, []);
  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    onClose();
    void relabelFlowIn(editor, flowId, text);
  };
  return (
    <form className="floating-field" style={{ left: Math.max(0, Math.min(at.x, window.innerWidth - 280)), top: at.y }} data-testid="flow-label" onSubmit={submit}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) onClose();
      }}>
      <label>
        <span>{t('map.flow_label')}</span>
        <input ref={field} value={text} maxLength={FLOW_LABEL_MAX} data-testid="flow-label-input"
          onChange={(event) => { setText(event.target.value); }}
          onKeyDown={(event) => {
            if (event.key === 'Escape') onClose();
          }} />
      </label>
    </form>
  );
}

export function MapFloating() {
  const editor = useEditor();
  const floating = useStore(editor.floating, (state) => state.floating);
  const map = useMapState((state) => state.map);
  const close = useCallback(() => {
    editor.floating.getState().close();
  }, [editor]);
  if (floating === null || map === null) return null;
  switch (floating.kind) {
    case 'menu':
      return <ContextMenu target={floating.target} at={floating.at} onClose={close} />;
    case 'flow-label':
      return <FlowLabelInput flowId={floating.flowId} at={floating.at} onClose={close} />;
    case 'lane-delete':
      return <LaneDialog map={map} laneId={floating.laneId} onClose={close} />;
    case 'macro-delete':
      return <MacroDialog map={map} stepId={floating.stepId} onClose={close} />;
    case 'selection-delete':
      return <SelectionDeleteDialog steps={floating.steps} flows={floating.flows} onClose={close} />;
    case 'detail-delete':
      return <DetailDeleteDialog map={map} stepId={floating.stepId} onClose={close} />;
    case 'command':
      return <CommandBar onClose={close} />;
    case 'remainder':
      return null;
    case 'arrangements':
      return <ArrangementDialog code={floating.code} arrangements={floating.arrangements} auto={floating.auto} onClose={close} />;
    case 'raci':
      return <RaciPanel map={map} stepId={floating.stepId} at={floating.at} onClose={close} />;
    case 'matrix':
      return <RaciMatrixDialog map={map} onClose={close} />;
    case 'open-pmap':
      return <OpenPmapDialog onClose={close} />;
    case 'import-package':
      return <ImportPackageDialog onClose={close} />;
    case 'duplicate':
      return <DuplicateDialog projectId={map.project_id} sourceName={projectOf(map).name} onClose={close} />;
    case 'export-package':
      return <ExportPackageDialog project={projectOf(map)} onClose={close} />;
    case 'replace':
      return <ImportPackageDialog replaced={floating.replaced} onClose={close} />;
    case 'translations':
      return <ProjectTranslations map={map} assistant={floating.assistant} onClose={close} />;
    case 'translate':
      return <ObjectTranslations map={map} object={floating.object} onClose={close} />;
    case 'stream-entry':
      return <StreamEntryField map={map} entry={floating.entry} at={floating.at} onClose={close} />;
  }
}
