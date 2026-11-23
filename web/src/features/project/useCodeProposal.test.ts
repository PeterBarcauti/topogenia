// useCodeProposal.test.ts
// The code of a new project proposed from its name: the pause after the last key, the request before cancelled, a code typed kept, an empty proposal.

import { act, renderHook } from '@testing-library/react';
import { callsTo, fakeApi, heldReply, type FakeRoutes } from '../../test/fakeApi.ts';
import { useCodeProposal } from './useCodeProposal.ts';

const ROUTE = 'POST /api/projects/code-proposal';
const answer = (code: string | null) => ({ json: { code } });

function setup(routes: FakeRoutes, first = '') {
  const sent = fakeApi(routes);
  const view = renderHook(({ name }) => useCodeProposal(name), { initialProps: { name: first } });
  return { sent, view };
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('the code follows the name', () => {
  it('the code follows the name 300 ms after the last key, the request before cancelled', async () => {
    const first = heldReply();
    const { sent, view } = setup({ [ROUTE]: [first.reply, answer('ACHATS')] }, 'Ach');
    await act(async () => { await vi.advanceTimersByTimeAsync(299); });
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal')).toHaveLength(0);
    await act(async () => { await vi.advanceTimersByTimeAsync(1); });
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal').map((call) => call.body)).toEqual([{ name: 'Ach' }]);
    // The name changes while the first request is still out: only the last counts.
    view.rerender({ name: 'Achats' });
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current).toMatchObject({ code: 'ACHATS', proposed: true });
    await act(async () => {
      first.give(answer('OLD'));
      await vi.advanceTimersByTimeAsync(10);
    });
    expect(view.result.current.code).toBe('ACHATS');
  });

  it('one request for several keys typed within the pause, the name in the body', async () => {
    const { sent, view } = setup({ [ROUTE]: answer('TDFF') }, 'T');
    for (const name of ['Tr', 'Tra', 'Traitement des factures']) {
      await act(async () => { await vi.advanceTimersByTimeAsync(100); });
      view.rerender({ name });
    }
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal').map((call) => call.body)).toEqual([{ name: 'Traitement des factures' }]);
    expect(view.result.current.code).toBe('TDFF');
  });

  it('a name emptied empties the proposal without a request', async () => {
    const { sent, view } = setup({ [ROUTE]: answer('ACHATS') }, 'Achats');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current.code).toBe('ACHATS');
    view.rerender({ name: '  ' });
    await act(async () => { await vi.advanceTimersByTimeAsync(600); });
    expect(view.result.current.code).toBe('');
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal')).toHaveLength(1);
  });
});

describe('what the person types', () => {
  it('a code typed is never replaced by the proposal of a new name', async () => {
    const { sent, view } = setup({ [ROUTE]: [answer('ACHATS'), answer('RECRUT')] }, 'Achats');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    act(() => { view.result.current.type('ma-p2'); });
    expect(view.result.current).toMatchObject({ code: 'MAP', proposed: false });
    view.rerender({ name: 'Recrutement' });
    await act(async () => { await vi.advanceTimersByTimeAsync(600); });
    expect(view.result.current.code).toBe('MAP');
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal')).toHaveLength(1);
  });

  it('a code erased letter by letter stays empty, and one letter typed then is that letter alone', async () => {
    const { sent, view } = setup({ [ROUTE]: answer('TDFF') }, 'Traitement des factures');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current.code).toBe('TDFF');
    for (const left of ['TDF', 'TD', 'T', '']) {
      act(() => { view.result.current.type(left); });
      expect(view.result.current).toMatchObject({ code: left, proposed: false });
    }
    await act(async () => { await vi.advanceTimersByTimeAsync(600); });
    expect(view.result.current).toMatchObject({ code: '', proposed: false });
    act(() => { view.result.current.type('a'); });
    expect(view.result.current).toMatchObject({ code: 'A', proposed: false });
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal')).toHaveLength(1);
  });

  it('a code emptied is proposed again only when the name changes afterwards, for the new name', async () => {
    const { sent, view } = setup({ [ROUTE]: [answer('ACHATS'), answer('RECRUT')] }, 'Achats');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    act(() => { view.result.current.type(''); });
    expect(view.result.current.code).toBe('');
    view.rerender({ name: 'Recrutement' });
    expect(view.result.current.code).toBe('');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current).toMatchObject({ code: 'RECRUT', proposed: true });
    expect(callsTo(sent, 'POST', '/api/projects/code-proposal').map((call) => call.body)).toEqual([{ name: 'Achats' }, { name: 'Recrutement' }]);
  });

  it('a request under way when the person types is not taken', async () => {
    const held = heldReply();
    const { view } = setup({ [ROUTE]: held.reply }, 'Achats');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    act(() => { view.result.current.type('abc'); });
    await act(async () => {
      held.give(answer('ACHATS'));
      await vi.advanceTimersByTimeAsync(10);
    });
    expect(view.result.current.code).toBe('ABC');
  });
});

describe('no proposal', () => {
  it('no proposal leaves the field empty', async () => {
    const { view } = setup({ [ROUTE]: [answer('ACHATS'), answer(null)] }, 'Achats');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current.code).toBe('ACHATS');
    view.rerender({ name: 'RH' });
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current).toMatchObject({ code: '', proposed: false });
  });

  it('a request that fails leaves the field empty, to be typed', async () => {
    const { view } = setup({ [ROUTE]: { refused: 500, code: 'ERREUR_INTERNE' } }, 'Achats');
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(view.result.current).toMatchObject({ code: '', proposed: false });
  });
});
