// exchange.ts
// What an operation of the API exchanges when it is not JSON, as the table generated from the contract names it (D09).

/** A reply read as text or as a file (its media type sent as `Accept`), a body sent as bytes. */
export interface Exchange {
  readonly reply?: 'text' | 'blob';
  readonly accept?: string;
  readonly body?: 'binary';
}
