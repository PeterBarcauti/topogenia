// client.ts
// The client of the API typed by the contract: each route, its values, its body, its version and its reply come from the generated types (D09).

import type { components, paths } from './generated/schema.ts';
import { send, type Method } from './request.ts';

/** A schema of the contract by its name (`Schema<'ProfileView'>`). */
export type Schema<Name extends keyof components['schemas']> = components['schemas'][Name];

type Route = keyof paths;

/** The routes of the contract that answer a method. */
export type RouteOf<M extends Method> = { [R in Route]: paths[R] extends Record<M, object> ? R : never }[Route];

/** The operation of a route for a method. */
export type Operation<R extends Route, M extends Method> = paths[R] extends Record<M, infer O> ? O : never;

type ParametersOf<O> = O extends { parameters: infer P } ? P : never;

/** Whether a type is `never` (an operation without that part). */
type IsNever<T> = [T] extends [never] ? true : false;

type PathPart<P> = P extends { path: infer V } ? { readonly path: V } : unknown;

type QueryPart<P> = P extends { query: infer Q }
  ? { readonly query: Q }
  : P extends { query?: infer Q }
    ? IsNever<NonNullable<Q>> extends true ? unknown : { readonly query?: NonNullable<Q> }
    : unknown;

/**
 * A route that reads `If-Match` takes the version of the object it changes, required: the mode Serveur refuses the
 * change without it (D06, 428 VERSION_REQUISE), and the client sends it in both modes.
 */
type VersionPart<P> = P extends { header?: infer H }
  ? IsNever<NonNullable<H>> extends true ? unknown : 'If-Match' extends keyof NonNullable<H> ? { readonly version: number } : unknown
  : unknown;

/** A body sent as bytes: a package of the exchange (spec 7). */
export type BinaryBody = Blob | ArrayBuffer | Uint8Array<ArrayBuffer>;

type MediaOf<C> = C extends { 'application/json': infer J } ? J : C extends { 'application/octet-stream': unknown } ? BinaryBody : never;

type BodyPart<O> = O extends { requestBody: { content: infer C } }
  ? { readonly body: MediaOf<C> }
  : O extends { requestBody?: infer B }
    ? IsNever<NonNullable<B>> extends true ? unknown : { readonly body?: MediaOf<NonNullable<B> extends { content: infer C } ? C : never> }
    : unknown;

/** What a call of a route takes besides its route: the parts its operation declares, and a cancellation. */
export type CallOptions<R extends Route, M extends Method> = PathPart<ParametersOf<Operation<R, M>>>
  & QueryPart<ParametersOf<Operation<R, M>>>
  & VersionPart<ParametersOf<Operation<R, M>>>
  & BodyPart<Operation<R, M>>
  & { readonly signal?: AbortSignal };

type Success<O> = O extends { responses: infer R } ? R[Extract<keyof R, 200 | 201 | 202 | 204>] : never;

type ContentOf<S> = S extends { content: infer C }
  ? C extends { 'application/json': infer J } ? J : C extends { 'text/plain': unknown } ? string : Blob
  : undefined;

/** What a call of a route gives: its JSON, its text, its file, or nothing. */
export type Reply<R extends Route, M extends Method> = ContentOf<Success<Operation<R, M>>>;

/** The options are required when the operation declares a value, a body or a version. */
type Arguments<R extends Route, M extends Method> = object extends CallOptions<R, M>
  ? [options?: CallOptions<R, M>]
  : [options: CallOptions<R, M>];

function call<M extends Method>(method: M) {
  return <R extends RouteOf<M>>(route: R, ...[options]: Arguments<R, M>): Promise<Reply<R, M>> =>
    send(method, route, options) as Promise<Reply<R, M>>;
}

/** The API, one function per method: `api.patch('/api/steps/{id}', { path: { id }, body, version })`. */
export const api = {
  get: call('get'),
  post: call('post'),
  put: call('put'),
  patch: call('patch'),
  delete: call('delete'),
};
