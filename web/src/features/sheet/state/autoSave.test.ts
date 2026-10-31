// autoSave.test.ts
// The saving of the sheet as typed: one send after the last change, one at a time with the version known, the typing never lost, a conflict read again (annex B 4).

import { ApiError } from '../../../api/errors.ts';
import { sheetViewOf } from '../../../test/sheets.ts';
import { createAutoSave, SAVE_DELAY } from './autoSave.ts';

const body = (text: string) => ({ fields: {}, procedure: [], free_text: text });

/** A queue whose sends answer the views given in turn, a refusal for an error, and what it did. */
function queueOf(replies: (number | ApiError)[], read: number | ApiError = 9) {
  const sent: [string | null | undefined, number][] = [];
  const seen: (number | null)[] = [];
  const failures: unknown[] = [];
  const answer = (reply: number | ApiError) => (reply instanceof ApiError ? Promise.reject(reply) : Promise.resolve(sheetViewOf({ version: reply })));
  const save = createAutoSave({
    save: (draft, version) => {
      sent.push([draft.free_text, version]);
      return answer(replies.shift() ?? 99);
    },
    read: () => answer(read),
  }, null);
  save.listen({ onView: (view) => seen.push(view.version), onFailure: (error) => failures.push(error) });
  return { save, sent, seen, failures };
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('the automatic saving (B-4-39 to B-4-45)', () => {
  it('sends once, 1200 ms after the last change, with the version 0 of a sheet never saved, then the version given back', async () => {
    const { save, sent, seen } = queueOf([4, 5]);
    expect(SAVE_DELAY).toBe(1200);
    save.change(body('a'));
    await vi.advanceTimersByTimeAsync(1000);
    save.change(body('ab'));
    await vi.advanceTimersByTimeAsync(1199);
    expect([sent, save.state.getState().status]).toEqual([[], 'pending']);
    await vi.advanceTimersByTimeAsync(1);
    expect([sent, seen, save.version(), save.state.getState().status]).toEqual([[['ab', 0]], [4], 4, 'saved']);
    save.change(body('abc'));
    await save.flush();
    expect([sent, save.state.getState().status, vi.getTimerCount()]).toEqual([[['ab', 0], ['abc', 4]], 'saved', 0]);
  });

  it('one send at a time: a change during a send waits for it, then leaves with the new version', async () => {
    let answer: (view: ReturnType<typeof sheetViewOf>) => void = () => undefined;
    const sent: [string | null | undefined, number][] = [];
    const save = createAutoSave({
      save: (draft, version) => {
        sent.push([draft.free_text, version]);
        return sent.length === 1 ? new Promise((resolve) => { answer = resolve; }) : Promise.resolve(sheetViewOf({ version: 5 }));
      },
      read: () => Promise.resolve(sheetViewOf()),
    }, null);
    save.change(body('a'));
    const first = save.flush();
    save.change(body('ab'));
    const second = save.flush();
    await vi.advanceTimersByTimeAsync(0);
    expect([[...sent], save.state.getState().status]).toEqual([[['a', 0]], 'saving']);
    answer(sheetViewOf({ version: 4 }));
    await first;
    expect(save.state.getState().status).toBe('saving');
    await second;
    expect([sent, save.state.getState().status]).toEqual([[['a', 0], ['ab', 4]], 'saved']);
  });

  it('a send not answered yet is no saving either: the tab would ask before closing (B-4-46)', async () => {
    let answer: (view: ReturnType<typeof sheetViewOf>) => void = () => undefined;
    const save = createAutoSave({ save: () => new Promise((resolve) => { answer = resolve; }), read: () => Promise.resolve(sheetViewOf()) }, null);
    save.change(body('a'));
    const sending = save.flush();
    await vi.advanceTimersByTimeAsync(0);
    expect(save.unsaved()).toBe(true);
    answer(sheetViewOf({ version: 1 }));
    await sending;
    expect(save.unsaved()).toBe(false);
  });

  it('a sheet read without version was never saved: the next send carries 0', () => {
    const { save } = queueOf([]);
    save.know(sheetViewOf({ version: 4 }));
    save.know(sheetViewOf({ version: null }));
    expect(save.version()).toBe(0);
  });

  it('a change typed while the send runs keeps the state pending after it', async () => {
    const { save } = queueOf([4]);
    save.change(body('a'));
    const sending = save.flush();
    save.change(body('ab'));
    await sending;
    expect([save.state.getState().status, save.unsaved()]).toEqual(['pending', true]);
  });

});

describe('a send that fails (B-4-43, B-4-44)', () => {
  it('a failure keeps the typing, says it, and sends it again at the next send; nothing to send gives the queue as it is', async () => {
    const refused = new ApiError(503, 'HTTP_503', 'Indisponible', {});
    const { save, sent, failures } = queueOf([refused, 6]);
    save.change(body('a'));
    await save.flush();
    expect([save.state.getState().status, save.failed(), save.unsaved(), failures]).toEqual(['failed', true, true, [refused]]);
    await save.flush();
    expect([sent, save.state.getState().status, save.failed(), save.unsaved()]).toEqual([[['a', 0], ['a', 0]], 'saved', false, false]);
    await save.flush();
    expect(sent).toHaveLength(2);
  });

  it('a newer typing replaces the one that failed, typed meanwhile', async () => {
    const refused = new ApiError(503, 'HTTP_503', 'Indisponible', {});
    const { save, sent } = queueOf([refused, 6]);
    save.change(body('a'));
    const sending = save.flush();
    save.change(body('ab'));
    await sending;
    await save.flush();
    expect(sent).toEqual([['a', 0], ['ab', 0]]);
  });

  it('a failure never brings back an older typing over a newer one already on its way (1.x: the older one left again after it)', async () => {
    const refused = new ApiError(503, 'HTTP_503', 'Indisponible', {});
    const { save, sent } = queueOf([refused, 6]);
    save.change(body('a'));
    void save.flush();
    save.change(body('ab'));
    await save.flush();
    await save.flush();
    expect([sent, save.state.getState().status, save.unsaved()]).toEqual([[['a', 0], ['ab', 0]], 'saved', false]);
  });

  it('a conflict reads the sheet again for its version, without giving its content to the typing, which leaves with that version', async () => {
    const conflict = new ApiError(409, 'CONFLIT_VERSION', 'Modifiée par ailleurs', {});
    const { save, sent, seen } = queueOf([conflict, 8], 7);
    save.know(sheetViewOf({ version: 3 }));
    save.change(body('a'));
    await save.flush();
    expect([seen, save.version(), save.state.getState().status]).toEqual([[7], 7, 'failed']);
    await save.flush();
    expect(sent).toEqual([['a', 3], ['a', 7]]);
  });

  it('a conflict whose reading fails keeps the version and the typing', async () => {
    const conflict = new ApiError(409, 'CONFLIT_VERSION', '', {});
    const { save, failures } = queueOf([conflict], new ApiError(503, 'HTTP_503', '', {}));
    save.know(sheetViewOf({ version: 3 }));
    save.change(body('a'));
    await save.flush();
    expect([save.version(), save.unsaved(), failures]).toEqual([3, true, [conflict]]);
  });
});
