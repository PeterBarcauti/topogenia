// onlineVoices.ts
// Whether a presentation may offer the online voices of the browser (F-137, audit 3.2 SEC-CRIT-02): only for a project whose classification may leave the computer, the list of the assistant; the rule is the host's alone (`AssistantExchanges.Allowed`), read from the state of the assistant of the map (`GET /api/maps/{id}/assistant`, open to any member); not read yet, unreadable or refused, the voices of the computer alone.

import { useEffect, useState } from 'react';
import { api } from '../../../api/client.ts';
import type { MapEditor } from '../state/editor.ts';

/** Whether the classification of the project of a map lets its texts leave the computer: the host, a fake in the tests. */
export type OnlineVoiceSource = (mapId: string, signal: AbortSignal) => Promise<boolean>;

/** The field `allowed` of the state of the assistant of the map: the host computes it from the classification of the project and the list of the settings of the AI, whatever the connector. */
export const hostOnlineVoices: OnlineVoiceSource = async (mapId, signal) => (await api.get('/api/maps/{id}/assistant', { path: { id: mapId }, signal })).allowed;

/**
 * Whether the online voices may be offered for the map given, read when the presentation starts: no until the host has
 * answered yes for that map, and no when it cannot be read or answers no.
 */
export function useOnlineVoicesAllowed(editor: MapEditor, mapId: string): boolean {
  const [allowedFor, setAllowedFor] = useState<string | null>(null);
  useEffect(() => {
    const control = new AbortController();
    editor.onlineVoices(mapId, control.signal).then((allowed) => {
      if (!control.signal.aborted) setAllowedFor(allowed ? mapId : null);
    }, () => {
      if (!control.signal.aborted) setAllowedFor(null);
    });
    return () => {
      control.abort();
    };
  }, [editor, mapId]);
  return allowedFor === mapId;
}
