// request.test.ts
// The exchange with the API: credentials, anti-CSRF header, If-Match, language, routes, files, and each normalised error (annex B 0, B-0-14 to B-0-21).

import { ApiError, isApiError } from './errors.ts';
import { send, setRequestLanguage, urlOf } from './request.ts';

interface Sent {
  url: string;
  init: RequestInit;
  headers: Headers;
}

const sent: Sent[] = [];

/** fetch answers each call with the next reply given, and records what it was sent. */
function answer(...replies: (Response | Error | DOMException)[]): void {
  vi.stubGlobal('fetch', vi.fn((url: string, init: RequestInit) => {
    sent.push({ url, init, headers: new Headers(init.headers) });
    const reply = replies.shift();
    if (reply === undefined) throw new Error('No reply left.');
    return reply instanceof Response ? Promise.resolve(reply) : Promise.reject(reply);
  }));
}

const json = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const last = (): Sent => {
  const call = sent.at(-1);
  if (!call) throw new Error('Nothing was sent.');
  return call;
};

async function refusal(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiError) return error;
    throw error;
  }
  throw new Error('The call was not refused.');
}

beforeEach(() => {
  sent.length = 0;
  setRequestLanguage(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('what every call carries', () => {
  it('lets the browser carry the authentication of the same origin, a read as a write (B-0-14)', async () => {
    answer(json({}), json({}));
    await send('get', '/api/me');
    await send('patch', '/api/me', { body: {}, version: 1 });
    expect(sent.map((call) => call.init.credentials)).toEqual(['same-origin', 'same-origin']);
  });

  it('sends the anti-CSRF header on every write and never on a read (B-0-15, AR-13)', async () => {
    answer(json({}), json({}), json({}), json({}), json({}));
    for (const method of ['get', 'post', 'put', 'patch', 'delete'] as const) await send(method, '/api/me');
    expect(sent.map((call) => [call.init.method, call.headers.get('X-Requested-With')])).toEqual([
      ['GET', null], ['POST', 'XMLHttpRequest'], ['PUT', 'XMLHttpRequest'], ['PATCH', 'XMLHttpRequest'], ['DELETE', 'XMLHttpRequest'],
    ]);
  });

  it('sends the version as a quoted If-Match, and no If-Match without a version (B-0-16, D06)', async () => {
    answer(json({}), json({}), json({}));
    await send('patch', '/api/steps/{id}', { path: { id: 's1' }, body: { label: 'Livrer' }, version: 7 });
    await send('delete', '/api/flows/{id}', { path: { id: 'f1' }, version: 0 });
    await send('post', '/api/maps/{id}/steps', { path: { id: 'm1' }, body: {} });
    expect(sent.map((call) => call.headers.get('If-Match'))).toEqual(['"7"', '"0"', null]);
  });

  it('sends the language of the interface once it is known, and none before (B-0-17)', async () => {
    answer(json({}), json({}), json({}));
    await send('get', '/api/me');
    setRequestLanguage('en');
    await send('get', '/api/me');
    setRequestLanguage(null);
    await send('get', '/api/me');
    expect(sent.map((call) => call.headers.get('Accept-Language'))).toEqual([null, 'en', null]);
  });

  it('writes a JSON body as JSON and asks for JSON', async () => {
    answer(json({ id: 's2' }));
    const reply = await send('post', '/api/maps/{id}/steps', { path: { id: 'm1' }, body: { label: 'Créer', column: 2 } });
    expect(reply).toEqual({ id: 's2' });
    expect(last().init.body).toBe('{"label":"Créer","column":2}');
    expect(last().headers.get('Content-Type')).toBe('application/json');
    expect(last().headers.get('Accept')).toBe('application/json');
  });

  it('sends no body and no content type when the call has none, and gives nothing for an empty success', async () => {
    answer(new Response(null, { status: 204 }));
    expect(await send('post', '/api/trash/{id}/restore', { path: { id: 'p1' } })).toBeUndefined();
    expect(last().init.body).toBeUndefined();
    expect(last().headers.get('Content-Type')).toBeNull();
  });
});

describe('the address of a route', () => {
  it('puts the encoded values of the path and the query, and leaves out an absent query value', () => {
    expect(urlOf('/api/steps/{id}', { path: { id: 'a b/c' }, query: { sub_map: 'reattach', to: null, stitch: true, other: undefined } }))
      .toBe('/api/steps/a%20b%2Fc?sub_map=reattach&stitch=true');
    expect(urlOf('/api/projects/{id}', { path: { id: 'p1' }, query: { confirmation: 'VTE&1' } })).toBe('/api/projects/p1?confirmation=VTE%261');
    expect(urlOf('/api/projects')).toBe('/api/projects');
  });

  it('refuses a route whose value of the path is missing, before any request', async () => {
    answer(json({}));
    expect(() => urlOf('/api/steps/{id}')).toThrow('{id}');
    await expect(send('get', '/api/maps/{id}')).rejects.toThrow('{id}');
    expect(sent).toHaveLength(0);
  });
});

describe('the files of the exchange (contract: generated table)', () => {
  it('reads the copy .pmap as text, asking for text', async () => {
    answer(new Response('{"format":"pmap"}', { status: 200, headers: { 'Content-Type': 'text/plain' } }));
    expect(await send('get', '/api/projects/{id}/pmap', { path: { id: 'p1' } })).toBe('{"format":"pmap"}');
    expect(last().headers.get('Accept')).toBe('text/plain');
  });

  it('reads the export as a file, asking for a ZIP archive', async () => {
    answer(new Response(new Uint8Array([80, 75, 3, 4]), { status: 200, headers: { 'Content-Type': 'application/zip' } }));
    const file = await send('post', '/api/projects/{id}/export', { path: { id: 'p1' }, body: { without_people: true } });
    // The Blob of the fetch of Node, not the one of jsdom: read by its shape.
    expect(file).toMatchObject({ size: 4, type: 'application/zip' });
    expect(new Uint8Array(await (file as Blob).arrayBuffer())).toEqual(new Uint8Array([80, 75, 3, 4]));
    expect(last().headers.get('Accept')).toBe('application/zip');
  });

  it('sends a package as its bytes, unchanged, with the binary content type', async () => {
    const bytes = new Uint8Array([80, 75, 3, 4, 0, 255]);
    answer(json({ id: 'p2' }, 201));
    await send('post', '/api/projects/import', { query: { with_members: 'false' }, body: bytes });
    expect(last().init.body).toBe(bytes);
    expect(last().headers.get('Content-Type')).toBe('application/octet-stream');
    expect(last().url).toBe('/api/projects/import?with_members=false');
  });
});

describe('the errors, all normalised', () => {
  it('turns a refusal of the API into its status, code, message and details (B-0-18)', async () => {
    answer(json({ code: 'CONFLIT_VERSION', message: 'Modifié par Alice il y a 2 minutes.', details: { by: 'Alice', when: 'il y a 2 minutes' } }, 409));
    const error = await refusal(send('patch', '/api/steps/{id}', { path: { id: 's1' }, body: {}, version: 3 }));
    expect([error.status, error.code, error.message, error.details]).toEqual([409, 'CONFLIT_VERSION', 'Modifié par Alice il y a 2 minutes.', { by: 'Alice', when: 'il y a 2 minutes' }]);
    expect(error.name).toBe('ApiError');
    expect(isApiError(error, 'CONFLIT_VERSION')).toBe(true);
    expect(isApiError(error, 'INTROUVABLE')).toBe(false);
  });

  it('gives RESEAU, status 0, when the request gets no answer (B-0-19)', async () => {
    answer(new TypeError('Failed to fetch'));
    const error = await refusal(send('get', '/api/projects'));
    expect([error.status, error.code, error.message]).toEqual([0, 'RESEAU', '']);
  });

  it('lets a cancellation through as it is, not as an error of the network', async () => {
    const controller = new AbortController();
    controller.abort();
    const abort = new DOMException('The operation was aborted.', 'AbortError');
    answer(abort);
    await expect(send('get', '/api/maps/{id}', { path: { id: 'm1' }, signal: controller.signal })).rejects.toBe(abort);
    expect(last().init.signal).toBe(controller.signal);
  });

  it('gives REPONSE_INVALIDE, status 0, for a success that is not JSON (B-0-20)', async () => {
    answer(new Response('<!doctype html><title>IIS</title>', { status: 200, headers: { 'Content-Type': 'text/html' } }));
    const error = await refusal(send('get', '/api/projects'));
    expect([error.status, error.code, error.message]).toEqual([0, 'REPONSE_INVALIDE', '']);
  });

  it('gives HTTP_<status> for a refusal that is not JSON or names no code (B-0-21)', async () => {
    answer(
      new Response('<html>Bad gateway</html>', { status: 502 }),
      json({ message: 'sans code' }, 500),
      json({ code: '', message: 'code vide' }, 400),
      new Response('', { status: 401 }),
    );
    const errors: ApiError[] = [];
    for (let i = 0; i < 4; i++) errors.push(await refusal(send('get', '/api/projects')));
    expect(errors.map((error) => [error.status, error.code, error.message])).toEqual([
      [502, 'HTTP_502', ''], [500, 'HTTP_500', ''], [400, 'HTTP_400', ''], [401, 'HTTP_401', ''],
    ]);
  });

  it('keeps the code and message of a refusal whose details are missing, with empty details', async () => {
    answer(json({ code: 'INTROUVABLE', message: 'Élément introuvable.' }, 404), json({ code: 'ACCES_REFUSE', details: [1] }, 403));
    const missing = await refusal(send('get', '/api/maps/{id}', { path: { id: 'm1' } }));
    const list = await refusal(send('get', '/api/maps/{id}', { path: { id: 'm1' } }));
    expect([missing.code, missing.message, missing.details]).toEqual(['INTROUVABLE', 'Élément introuvable.', {}]);
    expect([list.code, list.message, list.details]).toEqual(['ACCES_REFUSE', '', {}]);
  });
});
