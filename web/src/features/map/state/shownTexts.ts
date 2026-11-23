// shownTexts.ts
// The texts of the language a map is shown in, which may not be the language of the interface: what speaks or writes the map in that language takes them (the presentation, ADR 097; the view in BPMN 2.0, ADR 106).

import i18next from 'i18next';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { loadLanguage, type Translate } from '../../../shared/i18n/i18n.ts';

/** The texts of the language shown, read when it is first shown; those of the interface until then, or if it cannot be read. */
export function useShownTexts(language: string): Translate {
  const { i18n } = useTranslation();
  const [ready, setReady] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    loadLanguage(language).then(() => {
      if (live) setReady(language);
    }, () => undefined);
    return () => {
      live = false;
    };
  }, [language]);
  return useMemo(() => i18next.getFixedT(ready ?? i18n.language), [ready, i18n.language]);
}

/** The texts of a language, read first: those of the interface if it cannot be read. */
export async function shownTexts(language: string): Promise<Translate> {
  try {
    await loadLanguage(language);
    return i18next.getFixedT(language);
  } catch {
    return i18next.getFixedT(i18next.language);
  }
}
