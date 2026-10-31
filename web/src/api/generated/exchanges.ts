// exchanges.ts
// Operations of the API whose reply or body is not JSON, generated from openapi.json (D09): never edited by hand.

import type { Exchange } from '../exchange.ts';

/** Keyed `METHOD /path` as the contract writes them; an operation that is not listed takes and gives JSON. */
export const EXCHANGES: Readonly<Record<string, Exchange>> = {
  'GET /api/projects/{id}/pmap': { reply: 'text', accept: 'text/plain' },
  'POST /api/assistant/exchanges': { reply: 'blob', accept: 'application/x-ndjson' },
  'POST /api/maps/{id}/assistant/exchanges': { reply: 'blob', accept: 'application/x-ndjson' },
  'POST /api/projects/import': { body: 'binary' },
  'POST /api/projects/import/preview': { body: 'binary' },
  'POST /api/projects/{id}/export': { reply: 'blob', accept: 'application/zip' },
  'POST /api/projects/{id}/import': { body: 'binary' },
  'POST /api/projects/{id}/import/preview': { body: 'binary' },
};
