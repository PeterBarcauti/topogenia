// comments.test.ts
// The host as the source of the comments of presentation (ADR 099): a write is the PUT of the line of a step, or of a flow by its own route (lot J10), with the version read in If-Match, and carries the signal of its send, for a send given up at its time limit to cancel the request; the lines of the flows of a map are read by their own route.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { hostComments } from './comments.ts';

interface Sent {
  readonly url: string;
  readonly init: RequestInit;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the comments of presentation written to the host (ADR 099)', () => {
  it('puts the line of a step with the signal of its send, and gives back the lines of the step', async () => {
    const sent: Sent[] = [];
    const lines = [{ step_id: 's2', text: 'Le salarié saisit sa demande.', language: 'fr', source: 'manual', version: 3 }];
    vi.stubGlobal('fetch', vi.fn((url: string, init: RequestInit) => {
      sent.push({ url, init });
      return Promise.resolve(new Response(JSON.stringify({ comments: lines }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    }));
    const control = new AbortController();
    await expect(hostComments.write('s2', 'Le salarié saisit sa demande.', 'fr', 2, control.signal)).resolves.toEqual(lines);
    expect(sent.map((call) => [call.init.method, call.url])).toEqual([['PUT', '/api/steps/s2/presentation-comment']]);
    expect(sent[0]?.init.body).toBe(JSON.stringify({ text: 'Le salarié saisit sa demande.', language: 'fr' }));
    expect(sent[0]?.init.signal).toBe(control.signal);
  });

  it('puts the line of a flow by the route of the flows, the version read in If-Match, and gives back the lines of the flow', async () => {
    const sent: Sent[] = [];
    const lines = [{ flow_id: 'f2', text: 'Le dossier passe au contrôle.', language: 'fr', source: 'manual', version: 3 }];
    vi.stubGlobal('fetch', vi.fn((url: string, init: RequestInit) => {
      sent.push({ url, init });
      return Promise.resolve(new Response(JSON.stringify({ comments: lines }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    }));
    const control = new AbortController();
    await expect(hostComments.writeFlow('f2', 'Le dossier passe au contrôle.', 'fr', 2, control.signal)).resolves.toEqual(lines);
    expect(sent.map((call) => [call.init.method, call.url])).toEqual([['PUT', '/api/flows/f2/presentation-comment']]);
    expect(sent[0]?.init.body).toBe(JSON.stringify({ text: 'Le dossier passe au contrôle.', language: 'fr' }));
    expect([new Headers(sent[0]?.init.headers).get('If-Match'), sent[0]?.init.signal]).toEqual(['"2"', control.signal]);
  });

  it('reads the lines of the flows of a map by their own route', async () => {
    const sent: Sent[] = [];
    const lines = [{ flow_id: 'f2', text: 'Le dossier passe au contrôle.', language: 'fr', source: 'manual', version: 3 }];
    vi.stubGlobal('fetch', vi.fn((url: string, init: RequestInit) => {
      sent.push({ url, init });
      return Promise.resolve(new Response(JSON.stringify(lines), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    }));
    await expect(hostComments.readFlows('m1', new AbortController().signal)).resolves.toEqual(lines);
    expect(sent.map((call) => [call.init.method ?? 'GET', call.url])).toEqual([['GET', '/api/maps/m1/flow-presentation-comments']]);
  });
});
