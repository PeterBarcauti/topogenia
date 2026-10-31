// client.test.ts
// The client typed by the contract: routes by method, values, body, version required where If-Match is read, replies (D09, S-D09-02).

import { api, type CallOptions, type Reply, type RouteOf, type Schema } from './client.ts';
import type { ErrorCode, ServerErrorCode } from './errors.ts';

/**
 * The checks on types run with `npm run typecheck` (tsc reads the tests): an `expectTypeOf` that does not hold is a
 * compilation error; the runtime checks run with vitest.
 */
describe('the types of a call come from the contract', () => {
  it('answers each method with the routes that take it, and only those', () => {
    expectTypeOf<'/api/me'>().toExtend<RouteOf<'get'>>();
    expectTypeOf<'/api/projects/import'>().toExtend<RouteOf<'post'>>();
    expectTypeOf<'/api/projects/import'>().not.toExtend<RouteOf<'get'>>();
    expectTypeOf<'/api/health'>().not.toExtend<RouteOf<'delete'>>();
    expectTypeOf<'/api/nowhere'>().not.toExtend<RouteOf<'get'>>();
  });

  it('gives the reply of the contract: its schema, a text for the .pmap, a file for the export, nothing for a 204', () => {
    expectTypeOf<Reply<'/api/me', 'get'>>().toEqualTypeOf<Schema<'ProfileView'>>();
    expectTypeOf<Reply<'/api/projects/{id}', 'get'>>().toEqualTypeOf<Schema<'ProjectDetail'>>();
    expectTypeOf<Reply<'/api/projects/{id}/pmap', 'get'>>().toEqualTypeOf<string>();
    expectTypeOf<Reply<'/api/projects/{id}/export', 'post'>>().toEqualTypeOf<Blob>();
    expectTypeOf<Reply<'/api/trash/{id}/restore', 'post'>>().toEqualTypeOf<undefined>();
  });

  it('requires the values of the path and the body the operation declares, typed by its schemas', () => {
    expectTypeOf<CallOptions<'/api/steps/{id}', 'patch'>>().toHaveProperty('path').toEqualTypeOf<{ id: string }>();
    expectTypeOf<CallOptions<'/api/steps/{id}', 'patch'>>().toHaveProperty('body').toEqualTypeOf<Schema<'UpdateStepBody'>>();
    expectTypeOf<CallOptions<'/api/projects/import', 'post'>>().toHaveProperty('body').toEqualTypeOf<Blob | ArrayBuffer | Uint8Array<ArrayBuffer>>();
    expectTypeOf<CallOptions<'/api/projects', 'get'>>().not.toHaveProperty('path');
    expectTypeOf<CallOptions<'/api/projects', 'get'>>().not.toHaveProperty('body');
  });

  it('requires a version on each route that reads If-Match, and takes none elsewhere (D06)', () => {
    expectTypeOf<CallOptions<'/api/steps/{id}', 'patch'>>().toHaveProperty('version').toEqualTypeOf<number>();
    expectTypeOf<CallOptions<'/api/me', 'patch'>>().toHaveProperty('version').toEqualTypeOf<number>();
    expectTypeOf<CallOptions<'/api/projects/{id}', 'delete'>>().toHaveProperty('version').toEqualTypeOf<number>();
    expectTypeOf<CallOptions<'/api/maps/{id}/steps', 'post'>>().not.toHaveProperty('version');
    expectTypeOf<CallOptions<'/api/me', 'get'>>().not.toHaveProperty('version');
  });

  it('types the query of the operation, its values optional when the contract does not require them', () => {
    expectTypeOf<CallOptions<'/api/steps/{id}', 'delete'>>().toHaveProperty('query')
      .toEqualTypeOf<{ sub_map?: string; to?: string; stitch?: boolean } | undefined>();
  });

  it('types the codes of the errors: those of the host, and the three of the client', () => {
    expectTypeOf<'CONFLIT_VERSION'>().toExtend<ServerErrorCode>();
    expectTypeOf<'PMAP_JSON'>().toExtend<ServerErrorCode>();
    expectTypeOf<'RESEAU' | 'REPONSE_INVALIDE' | 'HTTP_502'>().toExtend<ErrorCode>();
    expectTypeOf<'RESEAU'>().not.toExtend<ServerErrorCode>();
    expectTypeOf<'NOT_A_CODE'>().not.toExtend<ErrorCode>();
  });
});

describe('a call of the client', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the method, the route with its values, the body and the version, and gives the reply', async () => {
    const fetch = vi.fn<(url: string, init: RequestInit) => Promise<Response>>(() =>
      Promise.resolve(new Response('{"id":"s1","version":4}', { status: 200, headers: { 'Content-Type': 'application/json' } })));
    vi.stubGlobal('fetch', fetch);
    const reply = await api.patch('/api/steps/{id}', { path: { id: 's1' }, body: { label: 'Livrer' }, version: 3 });
    const [url, init] = fetch.mock.calls[0] ?? [];
    expect([url, init?.method, init?.body, new Headers(init?.headers).get('If-Match')]).toEqual(['/api/steps/s1', 'PATCH', '{"label":"Livrer"}', '"3"']);
    expect(reply).toEqual({ id: 's1', version: 4 });
  });

  it('takes no options for a route that declares nothing', async () => {
    const fetch = vi.fn<(url: string, init: RequestInit) => Promise<Response>>(() => Promise.resolve(new Response('[]', { status: 200 })));
    vi.stubGlobal('fetch', fetch);
    expect(await api.get('/api/projects')).toEqual([]);
    expect(fetch.mock.calls[0]?.[0]).toBe('/api/projects');
  });
});
