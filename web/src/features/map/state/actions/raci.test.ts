// raci.test.ts
// The writes of the RACI and the copy of the matrix, without React: nothing sent for the same role, the two formats copied together, the text alone as a fallback (annex B 2.11).

import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { replyOf, sampleMap } from '../../../../test/maps.ts';
import { copyMatrix, setRaci } from './raci.ts';

afterEach(() => {
  vi.unstubAllGlobals();
});

const matrix = { columns: [{ id: 'l1', name: 'Commercial' }], rows: [{ id: 's2', code: 'VTE-E002', label: 'Vérifier', cells: ['R' as const] }] };
const headers = { code: 'Code', step: 'Étape' };

/** A clipboard that records what it is given; its rich writing fails when told. */
function clipboardOf(richFails = false) {
  const written: { items: ClipboardItem[]; texts: string[] } = { items: [], texts: [] };
  vi.stubGlobal('navigator', {
    clipboard: {
      write: (items: ClipboardItem[]) => (richFails ? Promise.reject(new Error('refused')) : Promise.resolve(void written.items.push(...items))),
      writeText: (text: string) => Promise.resolve(void written.texts.push(text)),
    },
  });
  return written;
}

class FakeItem {
  constructor(readonly parts: Record<string, Blob>) {}
}

describe('the RACI of a step (B-2.11-11 to B-2.11-13)', () => {
  it('sends the role of a lane, or null to take it back; nothing when the lane has that role already, nor for a step gone', async () => {
    const sent = fakeApi({ 'PUT /api/steps/s3/raci': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap({ raci: [{ step_id: 's3', lane_id: 'l2', role: 'C' }] }));
    await setRaci(editor, 's3', 'l2', 'C');
    await setRaci(editor, 'gone', 'l2', 'A');
    await setRaci(editor, 's3', 'l2', 'A');
    expect(callsTo(sent, 'PUT', '/api/steps/s3/raci').map((call) => call.body)).toEqual([{ lane_id: 'l2', role: 'A' }]);
  });
});

describe('the copy of the matrix (B-2.11-15 to B-2.11-17)', () => {
  it('puts the HTML table and the tabulated text together', async () => {
    vi.stubGlobal('ClipboardItem', FakeItem);
    const written = clipboardOf();
    expect(await copyMatrix(matrix, headers)).toBe(true);
    const parts = (written.items[0] as unknown as FakeItem | undefined)?.parts ?? {};
    expect([Object.keys(parts), parts['text/html']?.type, parts['text/plain']?.type]).toEqual([['text/html', 'text/plain'], 'text/html', 'text/plain']);
    expect([await parts['text/html']?.text(), await parts['text/plain']?.text()]).toEqual([expect.stringMatching(/^<table .*<td [^>]*>VTE-E002<\/td>/), 'Code\tÉtape\tCommercial\r\nVTE-E002\tVérifier\tR']);
    expect(written.texts).toEqual([]);
  });

  it('falls back on the text alone when the browser has no rich copy, or refuses it', async () => {
    vi.stubGlobal('ClipboardItem', undefined);
    const without = clipboardOf();
    expect(await copyMatrix(matrix, headers)).toBe(true);
    vi.stubGlobal('ClipboardItem', FakeItem);
    const refused = clipboardOf(true);
    expect(await copyMatrix(matrix, headers)).toBe(true);
    expect([without.texts, refused.texts, refused.items]).toEqual([['Code\tÉtape\tCommercial\r\nVTE-E002\tVérifier\tR'], ['Code\tÉtape\tCommercial\r\nVTE-E002\tVérifier\tR'], []]);
  });

  it('says false when nothing could be copied', async () => {
    vi.stubGlobal('ClipboardItem', undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText: () => Promise.reject(new Error('refused')) } });
    expect(await copyMatrix(matrix, headers)).toBe(false);
  });
});
