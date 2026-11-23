// useCodeProposal.ts
// The short code of a new project proposed from its name: asked of the host after a pause, the last name alone counts, a code typed by the person is never replaced.

import { useEffect, useState } from 'react';
import { api } from '../../api/client.ts';
import { typedCode } from './files/pmapText.ts';

/** The pause after the last key in the name before the host is asked. */
export const CODE_PROPOSAL_DELAY_MS = 300;

/** The code shown, whether it is the proposal, and the typing of the person in the field. */
export interface CodeProposal {
  readonly code: string;
  readonly proposed: boolean;
  readonly type: (text: string) => void;
}

/**
 * The code of the field. It follows the name until the person types in it. A code emptied stays empty: the proposal
 * comes back only when the name changes afterwards, for that new name. A new name, or a key in the code field,
 * cancels the request before it; a refusal or no proposal leaves the field empty.
 */
export function useCodeProposal(name: string): CodeProposal {
  const [own, setOwn] = useState<{ readonly value: string; readonly at: string } | null>(null);
  const [proposal, setProposal] = useState('');
  const text = name.trim();
  const following = own === null || (own.value === '' && own.at !== text);

  useEffect(() => {
    if (!following || text === '') return undefined;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      api.post('/api/projects/code-proposal', { body: { name: text }, signal: controller.signal }).then((reply) => {
        if (!controller.signal.aborted) setProposal(typedCode(reply.code ?? ''));
      }, () => {
        if (!controller.signal.aborted) setProposal('');
      });
    }, CODE_PROPOSAL_DELAY_MS);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [text, following]);

  const type = (value: string) => {
    const code = typedCode(value);
    setOwn({ value: code, at: text });
    if (code === '') setProposal('');
  };
  const shown = following ? (text === '' ? '' : proposal) : own.value;
  return { code: shown, proposed: following && shown !== '', type };
}
