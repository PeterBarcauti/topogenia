// fakeApi.ts
// A fake API for the tests of the screens: fetch answered route by route, every call recorded with its body and headers.

/** A call the screen sent. */
export interface SentCall {
  readonly method: string;
  readonly path: string;
  readonly query: URLSearchParams;
  readonly headers: Headers;
  readonly body: unknown;
}

/** A reply: a JSON body with its status, a text, bytes, or a refusal of the API. */
export type FakeReply =
  | { readonly json: unknown; readonly status?: number }
  | { readonly text: string }
  | { readonly blob: Blob }
  | { readonly refused: number; readonly code: string; readonly message?: string; readonly details?: Record<string, unknown> }
  | { readonly pending: true };

/** The replies by `METHOD /path` (the path with its values); a list gives one reply per call, the last one repeated. */
export type FakeRoutes = Record<string, FakeReply | FakeReply[]>;

function responseOf(reply: FakeReply): Promise<Response> {
  if ('pending' in reply) return new Promise<Response>(() => undefined);
  if ('json' in reply) {
    const status = reply.status ?? 200;
    // A 204 has no body: the browser refuses to build one with a body.
    return Promise.resolve(new Response(status === 204 ? null : JSON.stringify(reply.json), { status, headers: { 'Content-Type': 'application/json' } }));
  }
  if ('text' in reply) return Promise.resolve(new Response(reply.text, { status: 200, headers: { 'Content-Type': 'text/plain' } }));
  if ('blob' in reply) return Promise.resolve(new Response(reply.blob, { status: 200 }));
  const body = { code: reply.code, message: reply.message ?? '', details: reply.details ?? {} };
  return Promise.resolve(new Response(JSON.stringify(body), { status: reply.refused, headers: { 'Content-Type': 'application/json' } }));
}

function bodyOf(init: RequestInit | undefined): unknown {
  const body = init?.body;
  if (typeof body !== 'string') return body ?? undefined;
  try {
    return JSON.parse(body) as unknown;
  } catch {
    return body;
  }
}

/** Answers fetch by the routes given and gives the list of the calls sent; an unknown route fails the test loudly. */
export function fakeApi(routes: FakeRoutes): SentCall[] {
  const sent: SentCall[] = [];
  const used = new Map<string, number>();
  vi.stubGlobal('fetch', vi.fn((input: string, init?: RequestInit) => {
    const url = new URL(input, 'http://localhost');
    const key = `${(init?.method ?? 'GET').toUpperCase()} ${decodeURIComponent(url.pathname)}`;
    sent.push({ method: (init?.method ?? 'GET').toUpperCase(), path: url.pathname, query: url.searchParams, headers: new Headers(init?.headers), body: bodyOf(init) });
    const replies = routes[key];
    if (replies === undefined) return Promise.reject(new Error(`No fake reply for ${key}.`));
    const list = Array.isArray(replies) ? replies : [replies];
    const index = used.get(key) ?? 0;
    used.set(key, index + 1);
    const reply = list[Math.min(index, list.length - 1)];
    if (reply === undefined) return Promise.reject(new Error(`No fake reply for ${key}.`));
    return responseOf(reply);
  }));
  return sent;
}

/** The calls of one route. */
export const callsTo = (sent: readonly SentCall[], method: string, path: string): SentCall[] =>
  sent.filter((call) => call.method === method && call.path === path);
