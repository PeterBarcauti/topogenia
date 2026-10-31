// request.ts
// The one exchange with the API: credentials of the browser, anti-CSRF header on writes, If-Match from a version, language, errors normalised (annex B 0).

import { ApiError, INVALID_REPLY, NETWORK_ERROR, refusalOf } from './errors.ts';
import type { Exchange } from './exchange.ts';
import { EXCHANGES } from './generated/exchanges.ts';

export type Method = 'get' | 'put' | 'post' | 'patch' | 'delete';

/** A value of the path or of the query; a query value that is null or undefined is left out. */
export type QueryValue = string | number | boolean | null | undefined;

/** What a call gives besides its method and route: values of the path and the query, body, version, cancellation. */
export interface RequestParts {
  readonly path?: Readonly<Record<string, string | number>>;
  readonly query?: Readonly<Record<string, QueryValue>>;
  readonly body?: unknown;
  readonly version?: number;
  readonly signal?: AbortSignal;
}

/** The header of the anti-CSRF protection that every write carries (AR-13; 1.x `X-Ariane: 1`). */
export const ANTI_CSRF_HEADER = ['X-Requested-With', 'XMLHttpRequest'] as const;

let requestLanguage: string | null = null;

/** The language sent to the API (`Accept-Language`) so that its messages are in the language of the interface (B-0-17). */
export function setRequestLanguage(language: string | null): void {
  requestLanguage = language;
}

/** The address of a route of the contract (`/api/steps/{id}`) with the values of its path and of its query. */
export function urlOf(route: string, parts: RequestParts = {}): string {
  const path = route.replace(/\{(\w+)\}/g, (_, name: string) => {
    const value = parts.path?.[name];
    if (value === undefined) throw new Error(`The value of {${name}} is missing for ${route}.`);
    return encodeURIComponent(String(value));
  });
  const query = new URLSearchParams();
  for (const [name, value] of Object.entries(parts.query ?? {}))
    if (value !== null && value !== undefined) query.set(name, String(value));
  const search = query.toString();
  return search === '' ? path : `${path}?${search}`;
}

function headersOf(method: Method, exchange: Exchange, parts: RequestParts): Headers {
  const headers = new Headers({ Accept: exchange.accept ?? 'application/json' });
  if (method !== 'get') headers.set(...ANTI_CSRF_HEADER);
  if (parts.body !== undefined) headers.set('Content-Type', exchange.body === 'binary' ? 'application/octet-stream' : 'application/json');
  if (parts.version !== undefined) headers.set('If-Match', `"${parts.version}"`);
  if (requestLanguage !== null) headers.set('Accept-Language', requestLanguage);
  return headers;
}

function bodyOf(exchange: Exchange, body: unknown): BodyInit | undefined {
  if (body === undefined) return undefined;
  return exchange.body === 'binary' ? (body as BodyInit) : JSON.stringify(body);
}

const isAbort = (error: unknown, signal: AbortSignal | undefined): boolean =>
  signal?.aborted === true || (error instanceof DOMException && error.name === 'AbortError');

/** The success read as the contract announces it: text, file, or JSON (nothing for an empty body). */
async function replyOf(response: Response, exchange: Exchange): Promise<unknown> {
  if (exchange.reply === 'blob') return response.blob();
  const text = await response.text();
  if (exchange.reply === 'text') return text;
  if (text === '') return undefined;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    // A body that is not JSON comes from elsewhere than the API (page of IIS, proxy, application stopped).
    throw new ApiError(0, INVALID_REPLY, '');
  }
}

/**
 * Sends a POST of the contract whose reply is a stream of JSON lines (`application/x-ndjson`: the exchanges of the assistant,
 * spec 3.0 section 5) and gives each line read to `onLine`, as it comes, until the stream ends. A refusal before the stream
 * (its status) and a failure of the network are thrown as an {@link ApiError}, a cancellation as it is; a line that is not
 * JSON ends the stream as an invalid reply.
 */
export async function sendStream(route: string, parts: RequestParts, onLine: (line: unknown) => void): Promise<void> {
  const headers = headersOf('post', { accept: 'application/x-ndjson' }, parts);
  let response: Response;
  try {
    response = await fetch(urlOf(route, parts), {
      method: 'POST', headers, body: bodyOf({}, parts.body), credentials: 'same-origin', signal: parts.signal ?? null,
    });
  } catch (error) {
    if (isAbort(error, parts.signal)) throw error;
    throw new ApiError(0, NETWORK_ERROR, '');
  }
  if (!response.ok) throw refusalOf(response.status, await response.text());
  if (response.body === null) throw new ApiError(0, INVALID_REPLY, '');
  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
  let pending = '';
  const take = (text: string) => {
    if (text.trim() === '') return;
    let line: unknown;
    try {
      line = JSON.parse(text) as unknown;
    } catch {
      throw new ApiError(0, INVALID_REPLY, '');
    }
    onLine(line);
  };
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      pending += value;
      let end = pending.indexOf('\n');
      while (end >= 0) {
        take(pending.slice(0, end));
        pending = pending.slice(end + 1);
        end = pending.indexOf('\n');
      }
    }
    take(pending);
  } catch (error) {
    if (isAbort(error, parts.signal)) throw error;
    throw error instanceof ApiError ? error : new ApiError(0, NETWORK_ERROR, '');
  }
}

/**
 * Sends a call of the contract (`method`, `route` as the contract writes it) and gives its reply. The browser carries
 * the authentication (`same-origin`: Windows in server mode, the session cookie in desktop mode, B-0-14). A refusal,
 * a failure of the network or a success that is not JSON is thrown as an {@link ApiError}; a cancellation is thrown
 * as it is.
 */
export async function send(method: Method, route: string, parts: RequestParts = {}): Promise<unknown> {
  const exchange = EXCHANGES[`${method.toUpperCase()} ${route}`] ?? {};
  const url = urlOf(route, parts);
  const init: RequestInit = {
    method: method.toUpperCase(),
    headers: headersOf(method, exchange, parts),
    body: bodyOf(exchange, parts.body),
    credentials: 'same-origin',
    signal: parts.signal ?? null,
  };
  let response: Response;
  try {
    response = await fetch(url, init);
  } catch (error) {
    if (isAbort(error, parts.signal)) throw error;
    throw new ApiError(0, NETWORK_ERROR, '');
  }
  if (!response.ok) throw refusalOf(response.status, await response.text());
  return replyOf(response, exchange);
}
