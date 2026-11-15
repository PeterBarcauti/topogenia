// documentVoice.ts
// What a presentation says of a document (3.2, ADR 101): its type, from the medium entered or else an extension of its name, then what it holds in a few words read from its name, the numbers of its title with them (audit 3.2, UX-MAJ-06); never the name as a file is named, nor an address, a path, an extension, a date or a code.

import type { Translate } from '../../../../shared/i18n/i18n.ts';

/** A document as a presentation says it: its name and the medium it is carried on (a code of the list of the media), when one was entered. */
export interface SpokenDocument {
  readonly name: string;
  readonly medium?: string | null | undefined;
}

/** The types a document is said as, each with its article in the languages of the interface (`presenter.say.kind.*`). */
export type DocumentKind = 'paper' | 'email' | 'software' | 'spreadsheet' | 'excel' | 'csv' | 'word' | 'pdf' | 'visio' | 'powerpoint' | 'image' | 'file' | 'document';

/** The type the extension of a name tells. */
const BY_EXTENSION: Readonly<Record<string, DocumentKind>> = {
  xlsx: 'excel', xls: 'excel', xlsm: 'excel', csv: 'csv', docx: 'word', doc: 'word', pdf: 'pdf', vsdx: 'visio', vsd: 'visio', pptx: 'powerpoint', ppt: 'powerpoint',
  msg: 'email', eml: 'email', png: 'image', jpg: 'image', jpeg: 'image', gif: 'image', bmp: 'image', tif: 'image', tiff: 'image', webp: 'image', svg: 'image',
};

/** The other extensions of files: they tell no type, and are no more said than the first ones. None is a word of a title. */
const OTHER_EXTENSIONS: ReadonlySet<string> = new Set([
  'xlsb', 'xltx', 'xltm', 'xlt', 'docm', 'dotx', 'dotm', 'rtf', 'txt', 'md', 'odt', 'ods', 'odp', 'odg', 'pptm', 'ppsx', 'potx', 'vsdm', 'vssx', 'mpp', 'accdb', 'mdb',
  'pbix', 'xps', 'epub', 'indd', 'zip', 'rar', '7z', 'gz', 'tar', 'tgz', 'xml', 'json', 'html', 'htm', 'aspx', 'php', 'yaml', 'yml', 'sql', 'log', 'tsv', 'dat', 'dbf',
  'dwg', 'dxf', 'shp', 'kml', 'kmz', 'gpx', 'qgs', 'qgz', 'psd', 'eps', 'heic', 'ico', 'mp3', 'mp4', 'm4a', 'mov', 'avi', 'wav', 'mkv', 'mpg', 'mpeg', 'wmv', 'webm',
  'lnk', 'url', 'ics', 'vcf', 'pst', 'ost', 'oft', 'bak', 'tmp', 'ini', 'exe', 'msi', 'iso',
]);

const tellsType = (extension: string): boolean => Object.hasOwn(BY_EXTENSION, extension);
const isExtension = (extension: string): boolean => tellsType(extension) || OTHER_EXTENSIONS.has(extension);

/** The most words said of what a document holds: a title, not a sentence. */
export const CONTENT_WORDS = 8;

/** A dot, then two to five letters and digits no letter nor digit follows: an extension when it is a known one, wherever it stands in the name. */
const EXTENSION = /\.([a-z0-9]{2,5})(?![\p{L}\p{N}])/giu;
/** A web address, by its scheme or its « www. »: here with its host, which is never said. */
const WEB_ADDRESS = /^(?:[a-z][a-z0-9+.-]*:\/\/|www\.)[^/?#]*/iu;
/** A path of files: from a drive, a share, a root, the home or the folder beside, or with a backslash anywhere. */
const FILE_PATH = /^(?:[a-z]:|~|\.{1,2})?[\\/]|\\/iu;

/** The last segment of a path that is not empty; none, nothing. */
const lastSegment = (path: string): string => path.split(/[\\/]+/u).findLast((segment) => segment.trim() !== '') ?? '';

/** A segment of a web address as it was written before its encoding (« %20 »); left as it is when it is not one. */
function decoded(segment: string): string {
  try {
    return decodeURIComponent(segment);
  } catch {
    return segment;
  }
}

/**
 * What is read of a name. An address or a path typed as a name gives its last segment alone, without what follows a
 * « ? » or a « # » in an address: the server and the folders say nothing of the document. Any other name is read whole:
 * a slash between two words of a title is no path.
 */
function readOf(name: string): string {
  const typed = name.trim();
  const host = WEB_ADDRESS.exec(typed)?.[0];
  if (host !== undefined) return decoded(lastSegment(typed.slice(host.length).replace(/[?#].*$/su, '')));
  return FILE_PATH.test(typed) ? lastSegment(typed) : typed;
}

/**
 * The extension that tells of a name: the last known one that tells a type, else the last known one; none otherwise.
 * A last word after a dot that is no known extension is a word (« Rapport.final »).
 */
export function extensionOf(name: string): string | null {
  const known = [...readOf(name).matchAll(EXTENSION)].map((found) => (found[1] ?? '').toLowerCase()).filter(isExtension);
  return known.findLast(tellsType) ?? known.at(-1) ?? null;
}

/**
 * The type of a document. The medium entered says it: paper, an email, a software. A spreadsheet is said an Excel one or
 * a CSV file when its name tells so. A file, another medium or none: the extension of the name, else a file when the
 * medium says so, else a document.
 */
export function kindOf(document: SpokenDocument): DocumentKind {
  const named = BY_EXTENSION[extensionOf(document.name) ?? ''] ?? null;
  switch (document.medium) {
    case 'paper':
    case 'email':
    case 'software':
      return document.medium;
    case 'spreadsheet':
      return named === 'excel' || named === 'csv' ? named : 'spreadsheet';
    default:
      return named ?? (document.medium === 'file' ? 'file' : 'document');
  }
}

/** The marks of an email answered or forwarded, at the head of its subject. */
const MAIL_MARKS = /^(?:(?:re|tr|fw|fwd)\s*:\s*)+/iu;
/** An email address: no word of a title. */
const MAIL_ADDRESS = /\S+@\S+/gu;
/**
 * What separates the words of a name written as a file is named: underscores, the long dash, dots, brackets, quotes and
 * the like, those of the East Asian writings too, and the space of no width, which nobody sees.
 */
const SEPARATORS = /[\s\u200B_—.,;:+~#()[\]{}<>|/\\\x22«»“”„‹›\u3001\u3002\u300C-\u300F\u3010\u3011\uFF08\uFF09\uFF0C\uFF0E\uFF1A\uFF3F]+/u;
/** The dash that sticks the parts of a compound word, or of a code: the hyphen and its kin, down to the en dash, and the full width one. */
const HYPHEN = /[-\u2010-\u2013\uFF0D]/u;
/** A mark of version in one word: v2, V10b, rev3, version4. */
const VERSION = /^(?:v|ver|vers|version|rev|rév|revision|révision)\d+[a-z]?$/iu;
/** A word that marks a version when a number follows it: « v 2 », « version 3 », « rév. 4 »; not « vers », a word of its own (« Transfert vers 2 sites »). */
const VERSION_WORD = /^(?:v|ver|version|rev|rév|revision|révision)$/iu;
const NUMBER = /^\d+$/u;
/** An acronym: four capital letters at most, digits after the first one (« PR », « PROD », « R4 »); a longer word in capitals is a word (« DEVIS »). */
const ACRONYM = /^\p{Lu}[\p{Lu}\p{N}]{0,3}$/u;
/** An acronym with its number stuck to it, two digits at least, and the letter of its index: a code (« PR012 », « PR012A »); « A3 » and « 8D » are words. */
const STUCK_CODE = /^\p{Lu}{1,4}(\d{2,})\p{Lu}?$/u;
/**
 * An acronym, an underscore, then a number of three digits or more: a code too, left out with its number (« IMG_1234 »),
 * unless the number is a date (« CP_2024 »); a shorter number is no code (« RH_3 »).
 */
const UNDERSCORED_CODE = /(?<![\p{L}\p{N}])\p{Lu}[\p{Lu}\p{N}]{0,3}_(\d{3,})(?![\p{L}\p{N}])/gu;
/** A year of the last two centuries, alone or with its month and its day in one piece (« 20240312 »). */
const YEAR = /^(?:19|20)\d{2}(?:\d{4})?$/u;
const DAY_OR_MONTH = /^\d{1,2}$/u;
/** What is left of a time once the dashes are gone: « 12T10 », « 10h30 ». */
const TIME = /^\d+[th]\d+$/iu;
/** A date as Chinese and Japanese write it: « 2024年3月 ». */
const EAST_ASIAN_DATE = /^(?:\d+[年月日])+$/u;
/** « n° », with its number or without. */
const NUMBER_SIGN = /^n[°º]\d*$/iu;
/** A word that may be said: a letter with another letter or a digit, even apart (« CP », « 8D », « R&D »). */
const READABLE = /\p{L}.*[\p{L}\p{N}]|\p{N}.*\p{L}/u;
/** A number a title says (audit 3.2, UX-MAJ-06): one to four digits, no zero at its head, its sign stuck to it or not (« 2 », « 9001 », « 100% »). */
const TITLE_NUMBER = /^[1-9]\d{0,3}[%‰\p{Sc}]?$/u;
/** The sign of a number written apart from it: a percentage, a currency (« 100 % », « 1500 € »). */
const SIGN = /^[%‰\p{Sc}]$/u;
/** A word of letters, without a digit: what a number of a title stands beside (« Annexe 2 », « 3 devis »). */
const LETTERS = /^\P{N}*\p{L}\P{N}*$/u;

/** Numbers that read as a date: a year first or last with a month and a day at most, or two years (« 2024-2025 »). */
function isDate(numbers: readonly string[]): boolean {
  const years = numbers.filter((number) => YEAR.test(number)).length;
  if (years === numbers.length) return years <= 2;
  const yearAtAnEnd = YEAR.test(numbers[0] ?? '') || YEAR.test(numbers.at(-1) ?? '');
  return years === 1 && yearAtAnEnd && numbers.length <= 3 && numbers.every((number) => YEAR.test(number) || DAY_OR_MONTH.test(number));
}

/**
 * The parts of a word written with dashes, without its codes: acronyms stuck by a dash to a number are a code, left
 * out with it (« PR-012 », « FOR-RH-007 », « DOC-2024-001 »). An acronym before a date is no code (« CP-2024-03 »), nor
 * a word in lower case before a number (« compte-rendu-12 »).
 */
function withoutCodes(parts: readonly string[]): string[] {
  const kept: string[] = [];
  let index = 0;
  while (index < parts.length) {
    let letters = index;
    while (ACRONYM.test(parts[letters] ?? '')) letters += 1;
    let numbers = letters;
    while (NUMBER.test(parts[numbers] ?? '')) numbers += 1;
    const code = letters > index && numbers > letters && !isDate(parts.slice(letters, numbers));
    if (!code) kept.push(parts[index] ?? '');
    index = code ? numbers : index + 1;
  }
  return kept;
}

/**
 * Whether a word of a name is said. Not a word without a letter (a number, a number with its sign, a sign alone, unless
 * it is a number of the title, `isTitleNumber`; « & » stays, it is said « and »), a mark of version, a code in one piece
 * unless its number is a date (« CA2023 »), what is left of a time or of a date, « n° ».
 */
function isSaid(word: string, next: string): boolean {
  if (word === '&') return true;
  if (!/\p{L}/u.test(word) || VERSION.test(word) || TIME.test(word) || EAST_ASIAN_DATE.test(word) || NUMBER_SIGN.test(word)) return false;
  const stuck = STUCK_CODE.exec(word)?.[1];
  if (stuck !== undefined && !isDate([stuck])) return false;
  return !(VERSION_WORD.test(word) && NUMBER.test(next));
}

/**
 * Whether the number at `index` of the words of a name is said (audit 3.2, UX-MAJ-06): one to four digits without a zero
 * at its head, alone beside a word of letters, before or after it, its sign after it if it has one (« Certificat ISO
 * 9001 », « 3 devis », « Budget 100 % »). Not one of a run of numbers (a date, a time: « 12.03.2024 », « 2024-03 »),
 * nor one after a mark of version (« version 3 »); five digits or more, a zero at the head: a reference, never said.
 */
function isTitleNumber(words: readonly string[], index: number): boolean {
  const before = words[index - 1] ?? '';
  const after = words[index + 1] ?? '';
  const beyond = SIGN.test(after) ? words[index + 2] ?? '' : after;
  if (!TITLE_NUMBER.test(words[index] ?? '') || NUMBER.test(before) || NUMBER.test(beyond) || VERSION_WORD.test(before)) return false;
  return LETTERS.test(before) || LETTERS.test(beyond);
}

/** Whether the word at `index` is said: a word of the title, a number of the title, or the sign written apart from such a number. */
function saidAt(words: readonly string[], index: number): boolean {
  const word = words[index] ?? '';
  if (SIGN.test(word)) return NUMBER.test(words[index - 1] ?? '') && isTitleNumber(words, index - 1);
  return isSaid(word, words[index + 1] ?? '') || isTitleNumber(words, index);
}

/**
 * What a document holds, read from its name: the marks of an email, the email addresses, the known extensions, the
 * signs that stand for spaces, the codes, the dates, the numbers but those of the title (UX-MAJ-06) and the marks of
 * version left out; a run of three digits or more stuck to a word is a number too (« scan0001 »). What stays is said as
 * written, eight words at most; nothing readable (no word of two letters, or of a letter and a digit), nothing is said.
 */
export function contentOf(name: string): string {
  const bare = readOf(name).replace(MAIL_MARKS, '').replace(MAIL_ADDRESS, ' ')
    .replace(EXTENSION, (found, extension: string) => (isExtension(extension.toLowerCase()) ? ' ' : found))
    .replace(UNDERSCORED_CODE, (found, number: string) => (isDate([number]) ? found : ' '));
  const words = bare.split(SEPARATORS).flatMap((chain) => withoutCodes(chain.split(HYPHEN).filter((part) => part !== '')));
  // The digits stuck to a word go (« scan0001 »); a number of the title stays whole.
  const kept = words.filter((_, index) => saidAt(words, index)).map((word) => (/\p{L}/u.test(word) ? word.replace(/^\d{3,}|\d{3,}$/gu, '') : word)).filter((word) => word !== '');
  return kept.some((word) => READABLE.test(word)) ? kept.slice(0, CONTENT_WORDS).join(' ') : '';
}

/**
 * A document as said: its type with its article, then what it holds when its name tells it. A document whose type
 * nothing tells is said by what it holds alone, as a title (« Bon de commande »), and « a document » when its name tells
 * nothing either.
 */
export function documentSaid(document: SpokenDocument, t: Translate): string {
  const kind = kindOf(document);
  const content = contentOf(document.name);
  if (content === '') return t(`presenter.say.kind.${kind}`);
  return kind === 'document' ? content : t('presenter.say.document', { type: t(`presenter.say.kind.${kind}`), content });
}

/** The documents of a sentence, in the order of the map; two said the same way are said once. */
export function documentsSaid(documents: readonly SpokenDocument[], t: Translate): string[] {
  return [...new Set(documents.map((document) => documentSaid(document, t)))];
}
