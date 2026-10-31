// errors.ts
// The typed errors of the API: each refusal of the host and each failure of the exchange is one ApiError (annex B 0, spec 3.6).

import type { components } from './generated/schema.ts';

/** The body of every refusal of the host, as the contract describes it. */
export type ErrorBody = components['schemas']['ErrorBody'];

/** A code a refusal of the host names: one of the catalogue of its messages, listed by the contract. */
export type ServerErrorCode = ErrorBody['code'];

/** The codes the client gives itself: no answer, a success that is not JSON, a refusal without a code. */
export type ClientErrorCode = typeof NETWORK_ERROR | typeof INVALID_REPLY | `HTTP_${number}`;

export type ErrorCode = ServerErrorCode | ClientErrorCode;

/** The request did not get an answer (B-0-19). */
export const NETWORK_ERROR = 'RESEAU';

/** A success whose body is not the JSON the contract announces (B-0-20). */
export const INVALID_REPLY = 'REPONSE_INVALIDE';

/** An error of the API (1.x `ErreurApi`): the HTTP status (0 without answer), the code, the message, the values. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: ErrorCode;
  readonly details: Readonly<Record<string, unknown>>;

  constructor(status: number, code: ErrorCode, message: string, details: Readonly<Record<string, unknown>> = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

/** Whether an error is an error of the API, and of this code when one is given. */
export function isApiError(error: unknown, code?: ErrorCode): error is ApiError {
  return error instanceof ApiError && (code === undefined || error.code === code);
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

function parsed(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

/**
 * The error of a refused request (B-0-18, B-0-21): the code, message and details of its body when the body is a refusal
 * of the API, else `HTTP_<status>` with an empty message (a page of a proxy, of IIS, a body without a code).
 */
export function refusalOf(status: number, text: string): ApiError {
  const body = parsed(text);
  if (!isRecord(body) || typeof body.code !== 'string' || body.code === '') return new ApiError(status, `HTTP_${status}`, '');
  const message = typeof body.message === 'string' ? body.message : '';
  return new ApiError(status, body.code as ServerErrorCode, message, isRecord(body.details) ? body.details : {});
}
