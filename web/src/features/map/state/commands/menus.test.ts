// menus.test.ts
// The context menus and the ribbon of the map as data, without React: their entries, their order, their groups and states (annex B 2.1, 2.4, B-8.2-06, B-8.2-07, AR-155).

import { menuItemsOf, type CommandItem, type MenuLine } from '../../../../shared/commands/commands.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { frameOf, laneOf, mapOf, replyOf, sampleMap } from '../../../../test/maps.ts';
import { setClipboard } from '../clipboard.ts';
import type { MenuTarget } from '../floating.ts';
import { contextOf, type MapContext } from './context.ts';
import { FLOW_MENU, LANE_MENU, MAP_COMMANDS, PANE_MENU, ribbonGroups, SELECTION_MENU, STEP_MENU } from './mapCommands.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  setClipboard(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const withMacro = (structure = true) => {
  const map = sampleMap({ rights: { structure, role: structure ? 'facilitator' : 'reader' } });
  return { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) };
};

/** A detailed map under the root map « Ventes »: its breadcrumb holds the map above. */
const detailed = (structure = true) => sampleMap({
  rights: { structure, role: structure ? 'facilitator' : 'reader' },
  breadcrumb: [{ map_id: 'root', name: 'Ventes', source_language: 'fr', translations: {} }, { map_id: sampleMap().id, name: 'Traiter', source_language: 'fr', translations: {} }],
});

async function menu(map: ReturnType<typeof sampleMap>, lines: readonly MenuLine<MapContext>[], target: MenuTarget): Promise<CommandItem[]> {
  const { editor } = await editorOf(map);
  const context = contextOf(editor, target);
  if (context === null) throw new Error('No map.');
  return menuItemsOf(MAP_COMMANDS, lines, context);
}

/** An item as « id », « id | » when a separator stands before it, « id ! » when disabled, « id * » when checked. */
const shape = (items: readonly CommandItem[]) =>
  items.map((item) => `${item.separatorBefore === true ? '| ' : ''}${item.id}${item.disabled ? ' !' : ''}${item.checked === true ? ' *' : ''}`);

describe('the menu of a step (B-2.4-10 to B-2.4-30)', () => {
  it('for a facilitator, on a detailed step: its entries in the order of the annex, in four groups', async () => {
    expect(shape(await menu(withMacro(), STEP_MENU, { kind: 'step', stepId: 's2' }))).toEqual([
      'step.open', 'step.sheet', 'rename', 'step.insert-before', 'step.insert-after', 'branch', 'step.dissolve', 'step.delete-detail',
      '| cut', 'copy', 'duplicate', 'paste.before !', 'paste.after !',
      '| step.type.task *', 'step.type.wait', 'step.type.start !', 'step.type.end !', 'step.type.decision', 'step.type.parallel !', 'step.type.inclusive !',
      '| step.copy-code', 'step.delete',
    ]);
  });

  it('on a step without detail: « Détailler cette étape »; on a start: neither branch nor detail, nor its sheet, but its incoming volume (spec 14.6)', async () => {
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' })).slice(0, 7)).toEqual(['step.sheet', 'rename', 'step.insert-before', 'step.insert-after', 'branch', 'step.detail', '| cut']);
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's1' })).slice(0, 5)).toEqual(['rename', 'step.volume', 'step.insert-before', 'step.insert-after', '| cut']);
    expect(shape(await menu(sampleMap({ rights: { structure: false, role: 'reader' } }), STEP_MENU, { kind: 'step', stepId: 's1' }))).not.toContain('step.volume');
  });

  it('« Ouvrir la fiche » on a step with content; « Rôles RACI… » too, on the layer, for who may change the map only (NF08)', async () => {
    const ids = async (map: ReturnType<typeof sampleMap>, stepId: string, raci: boolean) => {
      const { editor } = await editorOf(map);
      editor.preferences.getState().set('showRaci', raci);
      const context = contextOf(editor, { kind: 'step', stepId });
      return context === null ? [] : menuItemsOf(MAP_COMMANDS, STEP_MENU, context).map((item) => item.id).filter((id) => id === 'step.sheet' || id === 'step.raci');
    };
    const reader = sampleMap({ rights: { structure: false, role: 'reader' } });
    expect([await ids(sampleMap(), 's2', false), await ids(sampleMap(), 's2', true), await ids(reader, 's2', true), await ids(sampleMap(), 's1', true)])
      .toEqual([['step.sheet'], ['step.sheet', 'step.raci'], ['step.sheet'], []]);
  });

  it('« Dissocier le sous-processus » on a detailed step, its shortcut shown, not while the map has a frame open (B-2.4-16, B-2.9-15, AR-154)', async () => {
    const ids = async (map: ReturnType<typeof sampleMap>) => (await menu(map, STEP_MENU, { kind: 'step', stepId: 's2' })).map((item) => item.id);
    const item = (await menu(withMacro(), STEP_MENU, { kind: 'step', stepId: 's2' })).find((entry) => entry.id === 'step.dissolve');
    expect([i18next.t(item?.label ?? ''), item?.shortcut]).toEqual(['Dissocier le sous-processus', 'Ctrl+Shift+G']);
    expect(await ids({ ...withMacro(), dissolution: frameOf(['s3']) })).not.toContain('step.dissolve');
    expect(await ids(sampleMap())).not.toContain('step.dissolve');
  });

  it('for a reader: open the detail and the sheet, copy the code, copy (B-2.4-28)', async () => {
    expect(shape(await menu(withMacro(false), STEP_MENU, { kind: 'step', stepId: 's2' }))).toEqual(['step.open', 'step.sheet', '| step.copy-code', 'copy']);
  });

  it('the pastes are enabled with something to paste, except on the step cut itself (B-2.4-24)', async () => {
    setClipboard({ mode: 'cut', projectId: 'p', mapId: sampleMap().id, steps: [{ key: 's3', label: '', type: 'task', lane: 0, column: 0, row: 0 }], flows: [] });
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' })).filter((item) => item.includes('paste'))).toEqual(['paste.before', 'paste.after']);
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's3' })).filter((item) => item.includes('paste'))).toEqual(['paste.before !', 'paste.after !']);
  });

  it('writes its labels with their values and its shortcuts; the red entries are marked', async () => {
    const items = await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' });
    const byId = new Map(items.map((item) => [item.id, item]));
    expect(i18next.t(byId.get('step.copy-code')?.label ?? '', byId.get('step.copy-code')?.labelValues)).toBe('Copier le code VTE-E002');
    expect(i18next.t(byId.get('step.type.decision')?.label ?? '', byId.get('step.type.decision')?.labelValues)).toBe('Changer le type : Décision');
    expect([byId.get('paste.after')?.shortcut, byId.get('step.delete')?.shortcut, byId.get('step.delete')?.danger]).toEqual(['Ctrl+V', 'Delete', true]);
  });
});

describe('the menus of a selection, of the background, of a flow and of a lane (B-2.4-31 to B-2.4-49)', () => {
  it('a selection: cut, copy, duplicate, then group into a sub-process, then delete N steps', async () => {
    const items = await menu(sampleMap(), SELECTION_MENU, { kind: 'selection', stepIds: ['s2', 's3'] });
    expect(shape(items)).toEqual(['cut', 'copy', 'duplicate', '| selection.group', '| selection.delete']);
    expect([i18next.t(items[3]?.label ?? ''), items[3]?.shortcut]).toEqual(['Grouper en sous-processus', 'Ctrl+G']);
    expect(i18next.t(items[4]?.label ?? '', items[4]?.labelValues)).toBe('Supprimer les 2 étapes');
  });

  it('« Inverser le sens » sends the flow of the menu to its reversal, with its version (AR-155)', async () => {
    const sent = fakeApi({ 'POST /api/flows/f3/reverse': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    const context = contextOf(editor, { kind: 'flow', flowId: 'f3', at: { x: 1, y: 2 } });
    if (context === null) throw new Error('No map.');
    MAP_COMMANDS.find((command) => command.id === 'flow.reverse')?.run(context);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/flows/f3/reverse').map((call) => call.headers.get('If-Match'))).toEqual(['"1"']);
    });
  });

  it('the background: paste here, enabled only with something to paste on a cell; choose all', async () => {
    expect(shape(await menu(sampleMap(), PANE_MENU, { kind: 'pane', point: { x: 500, y: 60 } }))).toEqual(['paste.here !', 'selectAll']);
    setClipboard({ mode: 'copy', projectId: 'p', mapId: 'm', steps: [{ key: 'c0', label: '', type: 'task', lane: 0, column: 0, row: 0 }], flows: [] });
    expect(shape(await menu(sampleMap(), PANE_MENU, { kind: 'pane', point: { x: 500, y: 60 } }))).toEqual(['paste.here', 'selectAll']);
    expect(shape(await menu(mapOf(), PANE_MENU, { kind: 'pane', point: { x: 500, y: 60 } }))).toEqual(['paste.here !', 'selectAll']);
  });

  it('a flow: its label, the share of its cases out of a decision (spec 14.6), « Inverser le sens », its deletion in red (AR-155)', async () => {
    const items = await menu(sampleMap(), FLOW_MENU, { kind: 'flow', flowId: 'f3', at: { x: 1, y: 2 } });
    expect([shape(items), i18next.t(items[1]?.label ?? ''), i18next.t(items[2]?.label ?? ''), items[3]?.danger])
      .toEqual([['flow.label', 'flow.share', 'flow.reverse', 'flow.delete'], 'Part des dossiers…', 'Inverser le sens', true]);
    expect(shape(await menu(sampleMap(), FLOW_MENU, { kind: 'flow', flowId: 'f1', at: { x: 1, y: 2 } }))).toEqual(['flow.label', 'flow.reverse', 'flow.delete']);
  });

  it('a lane: rename, its type among three, its colour among eight with their dots, delete but not the last lane', async () => {
    const items = await menu(sampleMap(), LANE_MENU, { kind: 'lane', laneId: 'l2' });
    expect(shape(items)).toEqual([
      'lane.rename', 'lane.headcount', '| lane.type.person', 'lane.type.department *', 'lane.type.system',
      '| lane.colour.rose', 'lane.colour.lavender *', 'lane.colour.sky', 'lane.colour.mint', 'lane.colour.peach', 'lane.colour.vanilla', 'lane.colour.lilac', 'lane.colour.aqua',
      '| lane.delete',
    ]);
    expect([items[5]?.swatch, i18next.t(items[6]?.label ?? '', items[6]?.labelValues)]).toEqual(['var(--lane-1-pastel)', 'Couleur : Lavande']);
    expect(shape(await menu(mapOf({ lanes: [laneOf('A', 0)] }), LANE_MENU, { kind: 'lane', laneId: 'A' })).at(-1)).toBe('| lane.delete !');
    // A lane of software has no headcount (spec 14.2, S4).
    expect(shape(await menu(mapOf({ lanes: [laneOf('A', 0, { type: 'system' })] }), LANE_MENU, { kind: 'lane', laneId: 'A' }))).not.toContain('lane.headcount');
  });
});

describe('the ribbon (B-2.1-08 to B-2.1-32)', () => {
  const ribbon = async (map: ReturnType<typeof sampleMap>, selection: string[] = []) => {
    const { editor } = await editorOf(map);
    editor.store.setState({ selection: { steps: selection, flows: [] } });
    const context = contextOf(editor);
    if (context === null) throw new Error('No map.');
    return ribbonGroups(context).map((group) => [group.id, ...shape(group.items)].join(' '));
  };

  it('a facilitator sees six zones; with nothing chosen nor any history, undo, redo, cut, copy, paste and detail are disabled; Hand and task are on', async () => {
    expect(await ribbon(sampleMap())).toEqual([
      'level level.up !', 'shapes shape.task * shape.wait shape.start shape.end',
      'gateways shape.decision shape.parallel shape.inclusive branch !', 'links link command', 'edit undo ! redo ! | cut ! copy ! paste ! | detail ! group ! ungroup ! align', 'display codes * pictos * raci stream',
      'tools tool.select tool.hand *',
    ]);
  });

  it('a step with content chosen enables the branch, cut, copy and detail; a start disables the branch (B-2.1-19)', async () => {
    expect((await ribbon(sampleMap(), ['s2'])).slice(2, 5)).toEqual(['gateways shape.decision shape.parallel shape.inclusive branch', 'links link command', 'edit undo ! redo ! | cut copy paste ! | detail group ! ungroup ! align']);
    expect((await ribbon(sampleMap(), ['s1']))[2]).toBe('gateways shape.decision shape.parallel shape.inclusive branch !');
  });

  it('a reader sees Tools and Display only; Align is disabled on an empty map (B-2.1-30)', async () => {
    expect(await ribbon(sampleMap({ rights: { structure: false, role: 'reader' } }))).toEqual(['level level.up !', 'display codes * pictos * raci stream', 'tools tool.select tool.hand *']);
    expect((await ribbon(mapOf({ lanes: [laneOf('A', 0)] })))[4]).toBe('edit undo ! redo ! | cut ! copy ! paste ! | detail ! group ! ungroup ! align !');
  });

  it('Détailler for one step, Grouper for several, Dissocier for one detailed step while the map has no frame open (AR-154)', async () => {
    const edit = async (map: ReturnType<typeof sampleMap>, selection: string[]) => (await ribbon(map, selection))[4];
    expect(await edit(sampleMap(), ['s2', 's3'])).toBe('edit undo ! redo ! | cut copy paste ! | detail ! group ungroup ! align');
    expect(await edit(withMacro(), ['s2'])).toBe('edit undo ! redo ! | cut copy paste ! | detail ! group ! ungroup align');
    expect(await edit({ ...withMacro(), dissolution: frameOf(['s3']) }, ['s2'])).toBe('edit undo ! redo ! | cut copy paste ! | detail ! group ! ungroup ! align');
  });

  it('« Remonter d’un niveau » first, for a reader too, enabled on a detailed map only, with Alt+Up; the tools last (AR-155)', async () => {
    expect([(await ribbon(detailed()))[0], (await ribbon(detailed(false)))[0], (await ribbon(sampleMap()))[0]]).toEqual(['level level.up', 'level level.up', 'level level.up !']);
    const { editor } = await editorOf(detailed());
    const context = contextOf(editor);
    if (context === null) throw new Error('No map.');
    const up = ribbonGroups(context)[0]?.items[0];
    expect([i18next.t(up?.label ?? ''), up?.shortcut, ribbonGroups(context).at(-1)?.id]).toEqual(['Remonter d’un niveau', 'Alt+ArrowUp', 'tools']);
  });

  it('names Grouper and Dissocier with the shortcuts of Office', async () => {
    const { editor } = await editorOf(sampleMap());
    const context = contextOf(editor);
    if (context === null) throw new Error('No map.');
    const items = ribbonGroups(context).find((group) => group.id === 'edit')?.items ?? [];
    expect(items.filter((item) => item.id.includes('group')).map((item) => [i18next.t(item.label), item.shortcut])).toEqual([['Grouper', 'Ctrl+G'], ['Dissocier', 'Ctrl+Shift+G']]);
  });
});
