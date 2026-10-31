// members.ts
// The members as the editor holds them, what is sent of them, and the language a new project is entered in (annex B 1.4, 1.5).

import type { Schema } from '../../api/client.ts';

export type MemberRole = Schema<'MemberRole'>;

/** The roles in the order of the list of the editor (B-1.4-10). */
export const ROLES: readonly MemberRole[] = ['facilitator', 'contributor', 'reader'];

/** A line of the editor of members; the key contribution is kept as it was read (B-1.5-01). */
export interface MemberLine {
  readonly login: string;
  readonly role: MemberRole;
  readonly isSponsor: boolean;
  readonly isKeyContributor: boolean;
}

/** A new line: a contributor (B-1.4-13). */
export const newMember = (): MemberLine => ({ login: '', role: 'contributor', isSponsor: false, isKeyContributor: false });

/** The lines of the members of a project, in their order. */
export const linesOf = (members: readonly Schema<'MemberView'>[]): MemberLine[] =>
  members.map((member) => ({ login: member.login, role: member.role, isSponsor: member.is_sponsor, isKeyContributor: member.is_key_contributor }));

/** What is sent: the lines with a login, trimmed; a line without login is left out (B-1.4-14). */
export const bodyOf = (lines: readonly MemberLine[]): Schema<'MemberBody'>[] =>
  lines.filter((line) => line.login.trim() !== '')
    .map((line) => ({ login: line.login.trim(), role: line.role, is_sponsor: line.isSponsor, is_key_contributor: line.isKeyContributor }));

/**
 * The language a new project is entered in, and its name and translations (B-1.4-06): the language of the interface when
 * its name is filled, else the first language of the configuration whose name is filled; undefined without any name.
 */
export function projectNames(names: Readonly<Record<string, string>>, interfaceLanguage: string, languages: readonly string[]):
  { readonly sourceLanguage: string; readonly name: string; readonly translations: Record<string, string> } | undefined {
  const filled = (language: string) => (names[language] ?? '').trim() !== '';
  const source = filled(interfaceLanguage) && languages.includes(interfaceLanguage) ? interfaceLanguage : languages.find(filled);
  if (source === undefined) return undefined;
  const translations = Object.fromEntries(languages.filter((language) => language !== source && filled(language)).map((language) => [language, (names[language] ?? '').trim()]));
  return { sourceLanguage: source, name: (names[source] ?? '').trim(), translations };
}
