// languageNames.ts
// The name of a language from the browser (ADR 083): in the language itself for a list to choose from, in the language of the interface inside a sentence; no list of names written in the code (NF12).

const capitalized = (text: string, language: string): string => text.charAt(0).toLocaleUpperCase(language) + text.slice(1);

/** The name of a language in itself, its first letter in capitals (« Português », « 日本語 »); its code when the browser does not know it. */
export function ownName(code: string): string {
  try {
    return capitalized(new Intl.DisplayNames([code], { type: 'language' }).of(code) ?? code, code);
  } catch {
    return code;
  }
}

/** The name of a language in the language of the interface, as a sentence writes it (« portugais »); its code when the browser does not know it. */
export function nameIn(code: string, language: string): string {
  try {
    return new Intl.DisplayNames([language], { type: 'language' }).of(code) ?? code;
  } catch {
    return code;
  }
}

/** The same name at the start of a line or a label (« Portugais »). */
export function titleIn(code: string, language: string): string {
  return capitalized(nameIn(code, language), language);
}
