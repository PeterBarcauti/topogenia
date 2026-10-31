// localizedName.ts
// The name of an object in the language shown: its translation when there is one, else the name as it was entered; whether it is written in that language (ADR 083).

import { useTranslation } from 'react-i18next';

/** A name entered in a language, with its translations into the others (projects, maps, lanes, steps, flows). */
export interface NamedObject {
  readonly name: string;
  readonly source_language: string;
  readonly name_translations: Readonly<Record<string, string>>;
}

/** The name in a language: the original in its own language, else its translation, else the original. */
export function localizedName(object: NamedObject, language: string): string {
  if (language === object.source_language) return object.name;
  return object.name_translations[language] ?? object.name;
}

/** Whether an object is written in a language: entered in it, or translated into it (ADR 083: else it is shown to translate). */
export function isWrittenIn(object: NamedObject, language: string): boolean {
  return language === object.source_language || object.name_translations[language] !== undefined;
}

/** The text a name is typed from in a language: the name entered in its own language, else its translation, else nothing (ADR 083). */
export function typedFrom(object: NamedObject, language: string): string {
  return language === object.source_language ? object.name : object.name_translations[language] ?? '';
}

/** The function that names an object in the language shown, followed by the component. */
export function useLocalizedName(): (object: NamedObject) => string {
  const { i18n } = useTranslation();
  return (object) => localizedName(object, i18n.language);
}
