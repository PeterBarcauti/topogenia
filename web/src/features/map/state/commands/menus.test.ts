// menus.test.ts
// The context menus and the ribbon of the map as data, without React: their entries, their order, their groups and states (annex B 2.1, 2.4, B-8.2-06, B-8.2-07, AR-155).

import { menuItemsOf, type CommandItem, type MenuLine } from '../../../../shared/commands/commands.ts';
import { i18next, PACKAGE_EXTENSION } from '../../../../shared/i18n/i18n.ts';
import { blocksOf } from '../../../../shared/ui/menuBlocks.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { memoryStorage } from '../../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { frameOf, laneOf, MAP_ID, mapOf, replyOf, sampleMap, stepOf } from '../../../../test/maps.ts';
import { setClipboard } from '../clipboard.ts';
import { createMapEditor } from '../editor.ts';
import type { MenuTarget } from '../floating.ts';
import { contextOf, type MapContext } from './context.ts';
import { FILE_COMMANDS } from './fileCommands.ts';
import { FILE_SUBMENUS } from './fileMenu.ts';
import { FILE_MENU, FLOW_MENU, LANE_MENU, MAP_COMMANDS, PANE_MENU, ribbonGroups, SELECTION_MENU, STEP_MENU } from './mapCommands.ts';

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

/** A detailed map under the root map « Visites »: its breadcrumb holds the map above. */
const detailed = (structure = true) => sampleMap({
  rights: { structure, role: structure ? 'facilitator' : 'reader' },
  breadcrumb: [{ map_id: 'root', name: 'Visites', source_language: 'fr', translations: {} }, { map_id: sampleMap().id, name: 'Traiter', source_language: 'fr', translations: {} }],
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

describe('the value of a step under the layer « Analysis » (Makigami M1, plan 3.1)', () => {
  async function analysed(map: ReturnType<typeof sampleMap>, stepId: string): Promise<CommandItem[]> {
    const { editor } = await editorOf(map);
    editor.preferences.getState().set('showAnalysis', true);
    const context = contextOf(editor, { kind: 'step', stepId });
    if (context === null) throw new Error('No map.');
    return menuItemsOf(MAP_COMMANDS, STEP_MENU, context);
  }
  const values = (items: readonly CommandItem[]) => shape(items).filter((id) => id.includes('step.value.'));

  it('offers the three classes and « Non classée » on a step with content, its class checked, under the layer only', async () => {
    const map = sampleMap({ steps: sampleMap().steps.map((step) => (step.id === 's3' ? { ...step, value_class: 'necessary' as const } : step)) });
    expect(values(await menu(map, STEP_MENU, { kind: 'step', stepId: 's3' }))).toEqual([]);
    expect(values(await analysed(map, 's3'))).toEqual(['| step.value.value_added', 'step.value.necessary *', 'step.value.waste', 'step.value.none']);
    expect(values(await analysed(map, 's2'))).toEqual(['| step.value.value_added', 'step.value.necessary', 'step.value.waste', 'step.value.none *']);
  });

  it('offers none on a start, nor on a detailed step whose value comes from its detail, nor to a reader', async () => {
    expect(values(await analysed(sampleMap(), 's1'))).toEqual([]);
    expect(values(await analysed(withMacro(), 's2'))).toEqual([]);
    expect(values(await analysed(sampleMap({ rights: { structure: false, role: 'reader' } }), 's2'))).toEqual([]);
  });
});

describe('the menu of a step (B-2.4-10 to B-2.4-30)', () => {
  it('for a facilitator, on a detailed step: its entries in the order of the annex, in four groups', async () => {
    expect(shape(await menu(withMacro(), STEP_MENU, { kind: 'step', stepId: 's2' }))).toEqual([
      'step.open', 'step.sheet', 'present.from', 'rename', 'step.insert-before', 'step.insert-after', 'branch', 'step.dissolve', 'step.delete-detail', 'step.replace-detail',
      '| cut', 'copy', 'duplicate', 'paste.before !', 'paste.after !',
      '| step.type.task *', 'step.type.wait', 'step.type.start !', 'step.type.end !', 'step.type.decision', 'step.type.parallel !', 'step.type.inclusive !',
      '| step.copy-code', 'step.delete',
    ]);
  });

  it('on a step without detail: « Détailler cette étape »; on a start: neither branch nor detail, nor its sheet, but its incoming volume (spec 14.6)', async () => {
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' })).slice(0, 8)).toEqual(['step.sheet', 'present.from', 'rename', 'step.insert-before', 'step.insert-after', 'branch', 'step.detail', '| cut']);
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's1' })).slice(0, 6)).toEqual(['present.from', 'rename', 'step.volume', 'step.insert-before', 'step.insert-after', '| cut']);
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

  it('the menu of a step offers Present from this step after its reading entries, for a facilitator, a contributor and a reader', async () => {
    const reader = sampleMap({ rights: { structure: false, role: 'reader' } });
    const contributor = sampleMap({ rights: { structure: false, role: 'contributor' } });
    const readers = [reader, contributor].map(async (map) => (await menu(map, STEP_MENU, { kind: 'step', stepId: 's2' })).map((item) => item.id).slice(0, 3));
    expect(await Promise.all(readers)).toEqual([['step.sheet', 'present.from', 'step.copy-code'], ['step.sheet', 'present.from', 'step.copy-code']]);
    const facilitator = (await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' })).map((item) => item.id);
    expect([facilitator.indexOf('present.from') - facilitator.indexOf('step.sheet'), facilitator.indexOf('rename') - facilitator.indexOf('present.from')]).toEqual([1, 1]);
    const item = (await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' })).find((entry) => entry.id === 'present.from');
    expect([i18next.t(item?.label ?? ''), item?.shortcut, item?.disabled]).toEqual(['Présenter depuis cette étape', 'Shift+F5', false]);
  });

  it('« Dissocier le sous-processus » on a detailed step, its shortcut shown, not while the map has a frame open (B-2.4-16, B-2.9-15, AR-154)', async () => {
    const ids = async (map: ReturnType<typeof sampleMap>) => (await menu(map, STEP_MENU, { kind: 'step', stepId: 's2' })).map((item) => item.id);
    const item = (await menu(withMacro(), STEP_MENU, { kind: 'step', stepId: 's2' })).find((entry) => entry.id === 'step.dissolve');
    expect([i18next.t(item?.label ?? ''), item?.shortcut]).toEqual(['Dissocier le sous-processus', 'Ctrl+Shift+G']);
    expect(await ids({ ...withMacro(), dissolution: frameOf(['s3']) })).not.toContain('step.dissolve');
    expect(await ids(sampleMap())).not.toContain('step.dissolve');
  });

  it('for a reader: open the detail and the sheet, copy the code, copy (B-2.4-28)', async () => {
    expect(shape(await menu(withMacro(false), STEP_MENU, { kind: 'step', stepId: 's2' }))).toEqual(['step.open', 'step.sheet', 'present.from', '| step.copy-code', 'copy']);
  });

  it('the pastes are enabled with something to paste, except on the step cut itself (B-2.4-24)', async () => {
    setClipboard({ mode: 'cut', projectId: 'p', mapId: sampleMap().id, steps: [{ key: 's3', label: '', type: 'task', lane: 0, column: 0, row: 0 }], flows: [] });
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' })).filter((item) => item.includes('paste'))).toEqual(['paste.before', 'paste.after']);
    expect(shape(await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's3' })).filter((item) => item.includes('paste'))).toEqual(['paste.before !', 'paste.after !']);
  });

  it('writes its labels with their values and its shortcuts; the red entries are marked', async () => {
    const items = await menu(sampleMap(), STEP_MENU, { kind: 'step', stepId: 's2' });
    const byId = new Map(items.map((item) => [item.id, item]));
    expect(i18next.t(byId.get('step.copy-code')?.label ?? '', byId.get('step.copy-code')?.labelValues)).toBe('Copier le code VIS-E002');
    expect(i18next.t(byId.get('step.type.decision')?.label ?? '', byId.get('step.type.decision')?.labelValues)).toBe('Changer le type : Décision');
    expect([byId.get('paste.after')?.shortcut, byId.get('step.delete')?.shortcut, byId.get('step.delete')?.danger]).toEqual(['Ctrl+V', 'Delete', true]);
  });
});

describe('the menus of a selection, of the background, of a flow and of a lane (B-2.4-31 to B-2.4-49)', () => {
  it('a selection: cut, copy, duplicate, then group into a sub-process, then the automatic route of its flows, then delete N steps', async () => {
    const items = await menu(sampleMap(), SELECTION_MENU, { kind: 'selection', stepIds: ['s2', 's3'] });
    // The copy as an image is greyed without the canvas of the map (lot 1 of the 3.4).
    expect(shape(items)).toEqual(['cut', 'copy', 'selection.copy-image !', 'duplicate', '| selection.group', '| selection.route.auto !', '| selection.delete']);
    expect([i18next.t(items[4]?.label ?? ''), items[4]?.shortcut]).toEqual(['Grouper en sous-processus', 'Ctrl+G']);
    expect(i18next.t(items[5]?.label ?? '')).toBe('Tracé automatique des flèches');
    expect(i18next.t(items[6]?.label ?? '', items[6]?.labelValues)).toBe('Supprimer les 2 étapes');
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
    expect(shape(await menu(sampleMap(), PANE_MENU, { kind: 'pane', point: { x: 500, y: 60 } }))).toEqual(['paste.here !', 'pane.insert-pmap', 'selectAll', '| pane.copy-image !']);
    setClipboard({ mode: 'copy', projectId: 'p', mapId: 'm', steps: [{ key: 'c0', label: '', type: 'task', lane: 0, column: 0, row: 0 }], flows: [] });
    expect(shape(await menu(sampleMap(), PANE_MENU, { kind: 'pane', point: { x: 500, y: 60 } }))).toEqual(['paste.here', 'pane.insert-pmap', 'selectAll', '| pane.copy-image !']);
    expect(shape(await menu(mapOf(), PANE_MENU, { kind: 'pane', point: { x: 500, y: 60 } }))).toEqual(['paste.here !', 'pane.insert-pmap !', 'selectAll', '| pane.copy-image !']);
  });

  it('a flow: its label, the share of its cases out of a decision (spec 14.6), « Inverser le sens », its departure and arrival, its deletion in red (AR-155, ADR 091)', async () => {
    const items = await menu(sampleMap(), FLOW_MENU, { kind: 'flow', flowId: 'f3', at: { x: 1, y: 2 } });
    expect([shape(items), i18next.t(items[1]?.label ?? ''), i18next.t(items[2]?.label ?? ''), items.at(-1)?.danger])
      .toEqual([['flow.label', 'flow.share', 'flow.reverse', 'flow.insert-pmap', '| flow.from.auto *', 'flow.from.top', 'flow.from.bottom', 'flow.from.left !', 'flow.from.right', '| flow.to.auto *', 'flow.to.top', 'flow.to.bottom', 'flow.to.left', 'flow.to.right', '| flow.route.auto !', '| flow.delete'], 'Part des dossiers…', 'Inverser le sens', true]);
    expect(shape(await menu(sampleMap(), FLOW_MENU, { kind: 'flow', flowId: 'f1', at: { x: 1, y: 2 } }))).toEqual(['flow.label', 'flow.reverse', 'flow.insert-pmap', '| flow.from.auto *', 'flow.from.top', 'flow.from.bottom', 'flow.from.left', 'flow.from.right', '| flow.to.auto *', 'flow.to.top', 'flow.to.bottom', 'flow.to.left', 'flow.to.right', '| flow.route.auto !', '| flow.delete']);
    expect(i18next.t(items[5]?.label ?? '', items[5]?.labelValues)).toBe('Départ : Haut');
  });

  it('« Départ : Haut » writes the route of the flow with its version, the arrival chosen kept; « Tracé automatique » gives it back to the drawing (ADR 091)', async () => {
    const routed = sampleMap({ flows: sampleMap().flows.map((flow) => (flow.id === 'f3' ? { ...flow, route: { source_side: null, target_side: 'left', via: null } } : flow)) });
    const sent = fakeApi({ 'PUT /api/flows/f3/route': { json: replyOf(routed) } });
    const { editor } = await editorOf(routed);
    const context = contextOf(editor, { kind: 'flow', flowId: 'f3', at: { x: 1, y: 2 } });
    if (context === null) throw new Error('No map.');
    expect(shape(menuItemsOf(MAP_COMMANDS, FLOW_MENU, context)).filter((item) => item.includes('flow.to.') || item.includes('route.auto')))
      .toEqual(['| flow.to.auto', 'flow.to.top', 'flow.to.bottom', 'flow.to.left *', 'flow.to.right', '| flow.route.auto']);
    MAP_COMMANDS.find((command) => command.id === 'flow.from.top')?.run(context);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f3/route').map((call) => [call.body, call.headers.get('If-Match')]))
        .toEqual([[{ source_side: 'top', target_side: 'left', via: null }, '"1"']]);
    });
    MAP_COMMANDS.find((command) => command.id === 'flow.route.auto')?.run(context);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f3/route').at(-1)?.body).toEqual({ source_side: null, target_side: null, via: null });
    });
  });

  it('on a gateway, its entry and a departure another exit chose are not offered (AR-24, ADR 091)', async () => {
    const map = sampleMap({ flows: sampleMap().flows.map((flow) => (flow.id === 'f4' ? { ...flow, route: { source_side: 'bottom', target_side: null, via: null } } : flow)) });
    const items = shape(await menu(map, FLOW_MENU, { kind: 'flow', flowId: 'f3', at: { x: 1, y: 2 } }));
    expect(items.filter((item) => item.includes('flow.from.'))).toEqual(['| flow.from.auto *', 'flow.from.top', 'flow.from.bottom !', 'flow.from.left !', 'flow.from.right']);
    expect(shape(await menu(map, FLOW_MENU, { kind: 'flow', flowId: 'f4', at: { x: 1, y: 2 } })).filter((item) => item.includes('flow.from.bottom'))).toEqual(['flow.from.bottom *']);
  });

});

describe('the menu of a lane (B-2.4-45 to B-2.4-49, ADR 096)', () => {
  it('a lane: rename, a row more or an empty row less at its top or bottom, its type among three, its colour among eight with their dots, delete but not the last lane', async () => {
    const items = await menu(sampleMap(), LANE_MENU, { kind: 'lane', laneId: 'l2' });
    expect(shape(items)).toEqual([
      'lane.rename', 'lane.headcount', '| lane.row.top', 'lane.row.bottom', 'lane.row.remove_top !', 'lane.row.remove_bottom !', '| lane.type.person', 'lane.type.department *', 'lane.type.system',
      '| lane.colour.rose', 'lane.colour.lavender *', 'lane.colour.sky', 'lane.colour.mint', 'lane.colour.peach', 'lane.colour.vanilla', 'lane.colour.lilac', 'lane.colour.aqua',
      '| lane.delete',
    ]);
    expect([items[9]?.swatch, i18next.t(items[10]?.label ?? '', items[10]?.labelValues)]).toEqual(['var(--lane-1-pastel)', 'Couleur : Lavande']);
    expect([2, 3, 4, 5].map((index) => i18next.t(items[index]?.label ?? ''))).toEqual([
      'Ajouter une rangée en haut', 'Ajouter une rangée en bas', 'Retirer la rangée vide du haut', 'Retirer la rangée vide du bas',
    ]);
    // A lane down to its last row (50) takes no more; its empty first row goes, never the last one that holds a step (ADR 096).
    const full = mapOf({ lanes: [laneOf('A', 0)], steps: [stepOf('s', 'A', 0, { row: 50 })] });
    expect(shape(await menu(full, LANE_MENU, { kind: 'lane', laneId: 'A' })).filter((item) => item.includes('lane.row.'))).toEqual([
      '| lane.row.top !', 'lane.row.bottom !', 'lane.row.remove_top', 'lane.row.remove_bottom !',
    ]);
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
      'gateways shape.decision shape.parallel shape.inclusive branch !', 'links link command', 'edit undo ! redo ! | cut ! copy ! paste ! | detail ! group ! ungroup ! align', 'display find codes * pictos * raci stream analysis plan bpmn',
      'tools tool.select tool.hand *',
    ]);
  });

  it('a step with content chosen enables the branch, cut, copy and detail; a start disables the branch (B-2.1-19)', async () => {
    expect((await ribbon(sampleMap(), ['s2'])).slice(2, 5)).toEqual(['gateways shape.decision shape.parallel shape.inclusive branch', 'links link command', 'edit undo ! redo ! | cut copy paste ! | detail group ! ungroup ! align']);
    expect((await ribbon(sampleMap(), ['s1']))[2]).toBe('gateways shape.decision shape.parallel shape.inclusive branch !');
  });

  it('a reader sees Tools and Display only; Align is disabled on an empty map (B-2.1-30)', async () => {
    expect(await ribbon(sampleMap({ rights: { structure: false, role: 'reader' } }))).toEqual(['level level.up !', 'display find codes * pictos * raci stream analysis plan bpmn', 'tools tool.select tool.hand *']);
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

  it('Search heads the zone Display for every role and in the BPMN view, by Ctrl+F (ADR 110 point 5)', async () => {
    for (const rights of [{ structure: true, role: 'facilitator' }, { structure: false, role: 'contributor' }, { structure: false, role: 'reader' }] as const) {
      const { editor } = await editorOf(sampleMap({ rights }));
      const context = contextOf(editor);
      if (context === null) throw new Error('No map.');
      for (const bpmn of [false, true]) {
        const first = ribbonGroups(context, bpmn).find((group) => group.id === 'display')?.items[0];
        expect([first?.id, first?.shortcut, first?.disabled, i18next.t(first?.label ?? '')]).toEqual(['find', 'Ctrl+F', false, 'Rechercher dans le projet']);
      }
    }
  });

  it('names Grouper and Dissocier with the shortcuts of Office', async () => {
    const { editor } = await editorOf(sampleMap());
    const context = contextOf(editor);
    if (context === null) throw new Error('No map.');
    const items = ribbonGroups(context).find((group) => group.id === 'edit')?.items ?? [];
    expect(items.filter((item) => item.id.includes('group')).map((item) => [i18next.t(item.label), item.shortcut])).toEqual([['Grouper', 'Ctrl+G'], ['Dissocier', 'Ctrl+Shift+G']]);
  });
});

describe('the action plan in the ribbon (ADR 112 point 5)', () => {
  it('Action plan follows Analysis in the zone Display, for every role and in the BPMN view, checked while open (ADR 112 point 5)', async () => {
    for (const rights of [{ structure: true, role: 'facilitator' }, { structure: false, role: 'contributor' }, { structure: false, role: 'reader' }] as const) {
      const { editor } = await editorOf(sampleMap({ rights }));
      for (const bpmn of [false, true]) {
        const items = () => {
          const context = contextOf(editor);
          if (context === null) throw new Error('No map.');
          return ribbonGroups(context, bpmn).find((group) => group.id === 'display')?.items ?? [];
        };
        const plan = () => items().find((item) => item.id === 'plan');
        expect(items().map((item) => item.id).slice(-3)).toEqual(['analysis', 'plan', 'bpmn']);
        expect([plan()?.shortcut, plan()?.disabled, plan()?.checked, i18next.t(plan()?.label ?? '')]).toEqual([undefined, false, false, 'Plan d’actions']);
        fakeApi({ [`GET /api/maps/${sampleMap().id}/action-plan`]: { json: { lines: [], open_lost_hours_per_month: 0 } } });
        MAP_COMMANDS.find((command) => command.id === 'plan')?.run(contextOf(editor) as MapContext);
        expect(plan()?.checked).toBe(true);
        MAP_COMMANDS.find((command) => command.id === 'plan')?.run(contextOf(editor) as MapContext);
        expect(plan()?.checked).toBe(false);
      }
    }
  });
});

describe('the File menu (B-2.4-52 to B-2.4-58)', () => {
  it('places every command of the File menu in its layout: none left out of the menu', () => {
    expect([...FILE_MENU.map((menuLine) => menuLine.id)].sort()).toEqual(FILE_COMMANDS.map((command) => command.id).sort());
  });
});

describe('the File menu in cascade (R15, ADR 117)', () => {
  const EXPORTS = 'export[file.pdf,file.png,file.svg,file.xlsx,file.md,file.bpmn,file.wall]';
  const ASSISTANT = 'assistant[file.report,file.summaries,file.comments]';
  const AT_A_CLICK = ['file.map_pmap', 'file.copy-image'];

  /** The File menu of a role as its blocks: an entry alone, or « group[its entries] ». */
  async function blocks(map: ReturnType<typeof sampleMap>, admin: boolean) {
    const editor = createMapEditor({ source: () => Promise.resolve({ map, history: { undo: [], redo: [] } }), storage: memoryStorage(), admin });
    await editor.store.getState().load(MAP_ID);
    const context = contextOf(editor, null);
    if (context === null) throw new Error('No map.');
    const items = menuItemsOf(MAP_COMMANDS, FILE_MENU, context);
    return { items, shown: blocksOf(items, [], FILE_SUBMENUS).map((block) => ('item' in block ? block.item.id : `${'submenu' in block ? block.submenu.id : block.group.name}[${block.items.map((item) => item.id).join(',')}]`)) };
  }
  const role = (name: 'facilitator' | 'contributor' | 'reader') => sampleMap({ rights: { structure: name === 'facilitator', role: name } });

  it('an administrator: three entries at a click, the four groups, the properties; eight rows for twenty-two commands', async () => {
    const { shown, items } = await blocks(role('facilitator'), true);
    expect(shown).toEqual(['file.open', ...AT_A_CLICK, EXPORTS, 'project[file.project-pdf,file.download,file.insert,file.duplicate,file.target]', 'package[file.export,file.import,file.replace]', ASSISTANT, 'file.properties']);
    expect(items).toHaveLength(22);
    // Three separators only: before the groups, before the print for a wall in its submenu, before the properties.
    expect(shape(items).filter((entry) => entry.startsWith('| '))).toEqual(['| file.pdf', '| file.wall', '| file.properties']);
  });

  it('a facilitator who is not an administrator sees the commands of today: no opening of a file, no duplicate, no target state, no import', async () => {
    const { shown } = await blocks(role('facilitator'), false);
    expect(shown).toEqual([...AT_A_CLICK, EXPORTS, 'project[file.project-pdf,file.download,file.insert]', 'package[file.export,file.replace]', ASSISTANT, 'file.properties']);
  });

  it('a contributor and a reader see the copy and the exports: no group of the package of exchange, one entry in the assistant group', async () => {
    for (const name of ['contributor', 'reader'] as const) {
      const { shown } = await blocks(role(name), false);
      expect(shown).toEqual([...AT_A_CLICK, EXPORTS, 'project[file.project-pdf,file.download]', 'assistant[file.report]', 'file.properties']);
    }
  });

  it('every command is at a click or in one group only', () => {
    const grouped = FILE_SUBMENUS.flatMap((submenu) => submenu.ids);
    expect(new Set(grouped).size).toBe(grouped.length);
    expect([...grouped, 'file.open', ...AT_A_CLICK, 'file.properties'].sort()).toEqual(FILE_COMMANDS.map((command) => command.id).sort());
    expect(blocksOf([], [], FILE_SUBMENUS)).toEqual([]);
  });

  it('the label of a group is short, by a key of the menu; the whole label stays that of the command, in the command bar and elsewhere', () => {
    const short = FILE_SUBMENUS.flatMap((submenu) => Object.entries(submenu.texts ?? {}));
    expect(short).toHaveLength(11);
    for (const [id, key] of short) {
      const full = i18next.t(FILE_COMMANDS.find((command) => command.id === id)?.label ?? '', { extension: PACKAGE_EXTENSION });
      expect([i18next.exists(key), i18next.t(key) !== full, i18next.t(key).length < full.length]).toEqual([true, true, true]);
    }
    expect([i18next.t('export.pdf'), i18next.t('export.png'), i18next.t('export.report'), i18next.t('assistant.comments.menu')])
      .toEqual(['Exporter en PDF (A3 paysage)', 'Exporter en image PNG', 'Rapport raconté par l’IA (Markdown)', 'Écrire les commentaires de présentation (IA)…']);
    expect(FILE_SUBMENUS.map((submenu) => i18next.t(submenu.name))).toEqual(['Exporter la carte', 'Projet', 'Paquet d’échange', 'Assistant IA']);
  });
});

describe('the copy as an image (lot 1 of the 3.4, ADR 109)', () => {
  /** The items of a menu for the editor given, on the target given. */
  const itemsFor = (editor: MapContext['editor'], lines: readonly MenuLine<MapContext>[], target: MenuTarget | null) => {
    const context = contextOf(editor, target);
    if (context === null) throw new Error('No map.');
    return { items: menuItemsOf(MAP_COMMANDS, lines, context), context };
  };
  const SELECTION: MenuTarget = { kind: 'selection', stepIds: ['s2', 's3'] };
  const PANE: MenuTarget = { kind: 'pane', point: { x: 500, y: 60 } };

  it('Copy the map as an image in the File menu and the menu of the background, Copy the selection as an image in the menu of a selection, for every role; greyed in the BPMN view', async () => {
    const roles = (['facilitator', 'contributor', 'reader'] as const).map((role) => sampleMap({ rights: { structure: role === 'facilitator', role } }));
    for (const map of roles) {
      for (const shown of [true, false]) {
        const { editor } = await editorOf(map);
        // The canvas of the map shown; the view in BPMN 2.0 shows none.
        if (shown) editor.canvas.show({ element: document.createElement('div'), bounds: null });
        const file = itemsFor(editor, FILE_MENU, null).items.map((item) => item.id);
        // At a click, after the save of the map (ADR 117).
        expect(file.indexOf('file.copy-image') - file.indexOf('file.map_pmap')).toBe(1);
        const entries = [
          itemsFor(editor, FILE_MENU, null).items.find((item) => item.id === 'file.copy-image'),
          itemsFor(editor, PANE_MENU, PANE).items.find((item) => item.id === 'pane.copy-image'),
          itemsFor(editor, SELECTION_MENU, SELECTION).items.find((item) => item.id === 'selection.copy-image'),
        ];
        expect(entries.map((item) => [i18next.t(item?.label ?? ''), item?.disabled, item?.shortcut])).toEqual([
          ['Copier la carte en image', !shown, undefined], ['Copier la carte en image', !shown, undefined], ['Copier la sélection en image', !shown, undefined],
        ]);
        const selection = itemsFor(editor, SELECTION_MENU, SELECTION).items.map((item) => item.id);
        expect(selection.indexOf('selection.copy-image') - selection.indexOf('copy')).toBe(1);
      }
    }
  });

  it('the entry of a selection copies its steps, that of the background the whole map', async () => {
    const { editor } = await editorOf(sampleMap({ rights: { structure: false, role: 'reader' } }));
    editor.canvas.show({ element: document.createElement('div'), bounds: null });
    const run = (lines: readonly MenuLine<MapContext>[], target: MenuTarget, id: string) => {
      const { context } = itemsFor(editor, lines, target);
      MAP_COMMANDS.find((command) => command.id === id)?.run(context);
      return editor.store.getState().exporting;
    };
    // Nothing to capture here: the copy starts, then says its failure and leaves the state of export.
    expect(run(SELECTION_MENU, SELECTION, 'selection.copy-image')).toEqual({ format: 'png', caption: null, steps: ['s2', 's3'] });
    await vi.waitFor(() => {
      expect(editor.store.getState().exporting).toBeNull();
    });
    expect(run(PANE_MENU, PANE, 'pane.copy-image')).toEqual({ format: 'png', caption: null });
    await vi.waitFor(() => {
      expect(editor.store.getState().exporting).toBeNull();
    });
  });
});
