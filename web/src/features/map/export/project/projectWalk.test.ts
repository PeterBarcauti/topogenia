// projectWalk.test.ts
// The walk of the maps of the PDF of the project, read from a fake host: level by level in the reading order of the parent steps, from the map asked and under it, stopped at the 101st map and below twelve levels, nothing more asked once aborted.

import type { Schema } from '../../../../api/client.ts';
import { callsTo, fakeApi, heldReply, type FakeRoutes } from '../../../../test/fakeApi.ts';
import { laneOf, mapOf, stepOf } from '../../../../test/maps.ts';
import { walkProject } from './projectWalk.ts';

type MapView = Schema<'MapView'>;

afterEach(() => {
  vi.unstubAllGlobals();
});

const lanes = [laneOf('l1', 0)];

/** A map whose steps, in the order given (the reading order of the view), detail the maps named; a null names a step not detailed. */
const viewOf = (id: string, parent: string | null, details: readonly (string | null)[]): MapView => mapOf({
  id, name: `Carte ${id}`, parent_step_id: parent, lanes,
  steps: details.map((sub, index) => stepOf(`${id}-s${String(index)}`, 'l1', index, { code: `VIS-E${id}${String(index)}`, sub_map_id: sub, sub_map_empty: sub === null })),
});

/** The reply of the host for a map: its view and the history of its writes. */
const replyOf = (view: MapView) => ({ json: { map: view, history: { undo: [], redo: [] } } });

/** The routes answering each map by its identifier. */
const routesOf = (views: readonly MapView[]): FakeRoutes => Object.fromEntries(views.map((view) => [`GET /api/maps/${view.id}`, replyOf(view)]));

/** The identifiers of the maps read, in the order they were asked. */
const readIds = (sent: ReturnType<typeof fakeApi>): string[] => sent.filter((call) => call.method === 'GET').map((call) => call.path.replace('/api/maps/', ''));

/**
 * A project of three levels whose steps are stored in another order than they are read: the root reads B (detailed by mb)
 * before A (detailed by ma); mb reads its step detailed by mb2; ma has a step without detail then one detailed by ma2.
 */
const ROOT = viewOf('root', null, ['mb', null, 'ma']);
const MB = viewOf('mb', 'root-s0', ['mb2']);
const MA = viewOf('ma', 'root-s2', [null, 'ma2']);
const MB2 = viewOf('mb2', 'mb-s0', []);
const MA2 = viewOf('ma2', 'ma-s1', [null]);
const PROJECT = [ROOT, MB, MA, MB2, MA2];

describe('the walk of the maps', () => {
  it('the maps level by level, each level in the reading order of the parent steps', async () => {
    const sent = fakeApi(routesOf(PROJECT));
    const counts: number[] = [];
    const walk = await walkProject('root', { signal: new AbortController().signal, progress: (count) => counts.push(count) });
    expect(walk).toEqual({ kind: 'walked', maps: [ROOT, MB, MA, MB2, MA2], depthCut: false });
    // Each map read once, in that order, a count said after each.
    expect(readIds(sent)).toEqual(['root', 'mb', 'ma', 'mb2', 'ma2']);
    expect(counts).toEqual([1, 2, 3, 4, 5]);
  });

  it('the walk starts from the map asked and stays under it', async () => {
    const sent = fakeApi(routesOf(PROJECT));
    const walk = await walkProject('ma', { signal: new AbortController().signal });
    expect(walk).toEqual({ kind: 'walked', maps: [MA, MA2], depthCut: false });
    expect(readIds(sent)).toEqual(['ma', 'ma2']);
  });

  it('at the 101st map the walk stops and says so, nothing more is read', async () => {
    // A root detailing 60 maps, each of the first two detailing 30 more: 1 + 60 + 60 maps; the 101st is known from the second map of the second level.
    const children = Array.from({ length: 60 }, (_, index) => `c${String(index)}`);
    const grand = (parent: string) => Array.from({ length: 30 }, (_, index) => `${parent}g${String(index)}`);
    const views = [
      viewOf('root', null, children),
      ...children.map((child, index) => viewOf(child, `root-s${String(index)}`, index < 2 ? grand(child) : [])),
      ...children.slice(0, 2).flatMap((child) => grand(child).map((id, index) => viewOf(id, `${child}-s${String(index)}`, []))),
    ];
    const sent = fakeApi(routesOf(views));
    const walk = await walkProject('root', { signal: new AbortController().signal });
    expect(walk).toEqual({ kind: 'too_many' });
    // The root, then c0 (91 maps known), then c1 (121 known): nothing more.
    expect(readIds(sent)).toEqual(['root', 'c0', 'c1']);
  });

  it('a hundred maps are walked whole', async () => {
    const children = Array.from({ length: 99 }, (_, index) => `c${String(index)}`);
    const views = [viewOf('root', null, children), ...children.map((child, index) => viewOf(child, `root-s${String(index)}`, []))];
    const sent = fakeApi(routesOf(views));
    const walk = await walkProject('root', { signal: new AbortController().signal });
    expect(walk.kind === 'walked' ? walk.maps.length : 0).toBe(100);
    expect(callsTo(sent, 'GET', '/api/maps/c98')).toHaveLength(1);
  });

});

describe('the twelve levels, the abandon, a refusal of the host', () => {
  it('below twelve levels the maps are left out and said', async () => {
    // Fourteen levels, each map detailing the step of the one before.
    const chain = Array.from({ length: 14 }, (_, index) => viewOf(`n${String(index)}`, index === 0 ? null : `n${String(index - 1)}-s0`, index === 13 ? [] : [`n${String(index + 1)}`]));
    const sent = fakeApi(routesOf(chain));
    const walk = await walkProject('n0', { signal: new AbortController().signal });
    expect(walk).toEqual({ kind: 'walked', maps: chain.slice(0, 12), depthCut: true });
    expect(readIds(sent)).toEqual(chain.slice(0, 12).map((view) => view.id));
    // Twelve levels exactly: nothing left out.
    const twelve = chain.slice(0, 12).map((view, index) => (index === 11 ? viewOf(view.id, view.parent_step_id, [null]) : view));
    fakeApi(routesOf(twelve));
    expect(await walkProject('n0', { signal: new AbortController().signal })).toEqual({ kind: 'walked', maps: twelve, depthCut: false });
  });

  it('an empty detailed map below the twelfth level leaves nothing out; above it, it is walked and counted like any map', async () => {
    // The twelfth map details a step by a map created then emptied: no map with steps is left out.
    const chain = Array.from({ length: 12 }, (_, index) => viewOf(`n${String(index)}`, index === 0 ? null : `n${String(index - 1)}-s0`, index === 11 ? ['vide'] : [`n${String(index + 1)}`]));
    const last = chain[11];
    if (last === undefined) throw new Error('No twelfth map.');
    chain[11] = { ...last, steps: last.steps.map((step) => ({ ...step, sub_map_empty: true })) };
    const sent = fakeApi(routesOf(chain));
    expect(await walkProject('n0', { signal: new AbortController().signal })).toEqual({ kind: 'walked', maps: chain, depthCut: false });
    expect(callsTo(sent, 'GET', '/api/maps/vide')).toEqual([]);
    // Above the twelfth level, an empty detailed map has its page, so it is read and counts in the hundred maps.
    const empty = viewOf('vide', 'root-s0', []);
    const root = viewOf('root', null, ['vide']);
    const top = { ...root, steps: root.steps.map((step) => ({ ...step, sub_map_empty: true })) };
    fakeApi(routesOf([top, empty]));
    expect(await walkProject('root', { signal: new AbortController().signal })).toEqual({ kind: 'walked', maps: [top, empty], depthCut: false });
  });

  it('aborted, the walk asks nothing more', async () => {
    const held = heldReply();
    const sent = fakeApi({ ...routesOf(PROJECT), 'GET /api/maps/mb': held.reply });
    const controller = new AbortController();
    const walking = walkProject('root', { signal: controller.signal });
    await vi.waitFor(() => {
      expect(readIds(sent)).toEqual(['root', 'mb']);
    });
    controller.abort();
    // The host answers anyway (a reply already on its way): it is not taken, and no other map is asked.
    held.give(replyOf(MB));
    await expect(walking).rejects.toMatchObject({ name: 'AbortError' });
    expect(readIds(sent)).toEqual(['root', 'mb']);
  });

  it('a refusal of the host ends the walk with that refusal', async () => {
    const sent = fakeApi({ ...routesOf(PROJECT), 'GET /api/maps/mb': { refused: 404, code: 'INTROUVABLE' } });
    await expect(walkProject('root', { signal: new AbortController().signal })).rejects.toMatchObject({ status: 404 });
    expect(readIds(sent)).toEqual(['root', 'mb']);
  });
});
