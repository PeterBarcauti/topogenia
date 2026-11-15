// projectVoices.test.ts
// The host as the source of the voices a project keeps (lot J10): they are read by the route of the project; the voice of a language is the PUT of its line, its name and where it speaks, with the version read in If-Match and the signal of its send, and gives back every voice of the project.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { hostVoices } from './projectVoices.ts';

interface Sent {
  readonly url: string;
  readonly init: RequestInit;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

/** A host that answers every request with the body given, and keeps what it was sent. */
function answering(body: unknown): Sent[] {
  const sent: Sent[] = [];
  vi.stubGlobal('fetch', vi.fn((url: string, init: RequestInit) => {
    sent.push({ url, init });
    return Promise.resolve(new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }));
  }));
  return sent;
}

describe('the voices of a project read from and written to the host (lot J10)', () => {
  const voices = [{ language: 'fr', name: 'Hortense', local: true, version: 3 }];

  it('reads the voice of each language by the route of the project', async () => {
    const sent = answering(voices);
    await expect(hostVoices.read('p1', new AbortController().signal)).resolves.toEqual(voices);
    expect(sent.map((call) => [call.init.method ?? 'GET', call.url])).toEqual([['GET', '/api/projects/p1/presentation-voices']]);
  });

  it('puts the voice of a language, its name and where it speaks, the version read in If-Match, and gives back the voices of the project', async () => {
    const sent = answering({ voices });
    const control = new AbortController();
    await expect(hostVoices.write('p1', 'fr', { name: 'Hortense', local: true }, 2, control.signal)).resolves.toEqual(voices);
    expect(sent.map((call) => [call.init.method, call.url])).toEqual([['PUT', '/api/projects/p1/presentation-voices/fr']]);
    expect(sent[0]?.init.body).toBe(JSON.stringify({ name: 'Hortense', local: true }));
    expect([new Headers(sent[0]?.init.headers).get('If-Match'), sent[0]?.init.signal]).toEqual(['"2"', control.signal]);
  });
});
