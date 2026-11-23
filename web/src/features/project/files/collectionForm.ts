// collectionForm.ts
// The state of the dialog of a collection (3.4, lot 6, ADR 114): the preview read once when it opens, the ticks and the codes, then the creation one project at a time by the opening of a file; nothing is written before « Créer les projets ».

import { useEffect, useState } from 'react';
import { api, type Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { typedCode } from './pmapText.ts';
import { bodyOf, changeRow, DUPLICATE_CODE, duplicatesOf, mayCreate, pendingRows, rowsOf, tickAll, type CollectionRow, type Refusal } from './collection.ts';
import { faultsOf } from './fileFaults.ts';

/** What the dialog shows: the preview being read, refused, or read. */
export type Loading =
  | { readonly status: 'loading' }
  | { readonly status: 'refused'; readonly refusal: Refusal }
  | { readonly status: 'ready'; readonly name: string; readonly description: string | null };

/** Where the creation stands: the lines being chosen, the creation under way (its rank and its count), the results shown. */
export type Phase = { readonly step: 'choose' } | { readonly step: 'creating'; readonly done: number; readonly total: number } | { readonly step: 'done' };

const refusalOf = (failure: unknown): Refusal => ({ message: messageOf(failure), code: isApiError(failure) ? failure.code : null, faults: faultsOf(failure) });

/** The preview of the text of a collection, read once when the dialog opens: read-only, the host writes nothing. */
function usePreview(content: string) {
  const [loading, setLoading] = useState<Loading>({ status: 'loading' });
  const [rows, setRows] = useState<CollectionRow[]>([]);
  useEffect(() => {
    let current = true;
    api.post('/api/projects/import-collection/preview', { body: { content } })
      .then((preview: Schema<'CollectionPreview'>) => {
        if (!current) return;
        setRows(rowsOf(preview));
        setLoading({ status: 'ready', name: preview.name, description: preview.description });
      })
      .catch((failure: unknown) => {
        if (current) setLoading({ status: 'refused', refusal: refusalOf(failure) });
      });
    return () => {
      current = false;
    };
  }, [content]);
  return { loading, rows, setRows };
}

/** The whole state of the dialog, and what it does: tick, type a code, create the lines ticked, try a refused one again. */
export function useCollectionForm(content: string, onCreated: (() => void) | undefined) {
  const { loading, rows, setRows } = usePreview(content);
  const [phase, setPhase] = useState<Phase>({ step: 'choose' });
  const busy = phase.step === 'creating';

  const tick = (index: number, ticked: boolean) => { setRows((current) => changeRow(current, index, { ticked })); };
  const tickEvery = (ticked: boolean) => { setRows((current) => tickAll(current, ticked)); };
  const typeCode = (index: number, typed: string) => { setRows((current) => changeRow(current, index, { code: typedCode(typed) })); };

  /**
   * Creates the lines given, one after the other, by the opening of a file: each is one project, whole or not at all; a
   * refusal marks its line and the next goes on. A line with a code already given is refused first, nothing sent for it.
   */
  const run = async (targets: readonly CollectionRow[]) => {
    const twice = duplicatesOf(rows, targets);
    setRows((current) => current.map((row) => (twice.has(row.index) ? { ...row, state: 'failed', refusal: { message: '', code: DUPLICATE_CODE, faults: [], triedCode: row.code } } : row)));
    const sent = targets.filter((row) => !twice.has(row.index));
    let created = 0;
    for (const [position, row] of sent.entries()) {
      setPhase({ step: 'creating', done: position + 1, total: sent.length });
      setRows((current) => changeRow(current, row.index, { state: 'creating', refusal: null }));
      try {
        const opened = await api.post('/api/projects/import-pmap', { body: bodyOf(row) });
        created += 1;
        setRows((current) => changeRow(current, row.index, { state: 'created', opened }));
      } catch (failure) {
        setRows((current) => changeRow(current, row.index, { state: 'failed', refusal: { ...refusalOf(failure), triedCode: row.code } }));
      }
    }
    setPhase({ step: 'done' });
    if (created > 0) onCreated?.();
  };

  const create = () => {
    if (busy || !mayCreate(rows)) return;
    void run(pendingRows(rows));
  };
  const retry = (index: number) => {
    const row = rows.find((candidate) => candidate.index === index);
    if (busy || row === undefined || row.state !== 'failed' || !mayCreate([row])) return;
    void run([row]);
  };
  return { loading, rows, phase, busy, tick, tickEvery, typeCode, create, retry };
}
