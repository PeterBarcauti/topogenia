// attachments.ts
// The files joined to the requests of the panel of the assistant: their kinds, the checks of the panel against the bounds of the connector (the host checks them again), their content read in base64, their size written for a person (spec 3.0 section 3.4, lot 4).

import type { Schema } from '../../../api/client.ts';

export type FileBounds = Schema<'AssistantFileBoundsView'>;
export type FileKind = 'pdf' | 'image' | 'word' | 'excel' | 'visio';

/** A file joined, kept by the page for the conversation (never in the browser nor the host): its name, size, kind and content in base64. */
export interface JoinedFile {
  readonly id: number;
  readonly name: string;
  readonly size: number;
  readonly kind: FileKind;
  readonly data: string;
}

/** What a request shows of its files. */
export type FileLabel = Pick<JoinedFile, 'name' | 'size' | 'kind'>;

/** A file the panel does not join, and why; the maximum in megabytes or in files. */
export interface FileRefusal {
  readonly reason: 'outdated' | 'format' | 'too_large' | 'too_many' | 'too_heavy' | 'unreadable';
  readonly name: string;
  readonly format?: string;
  readonly max?: number;
}

const KINDS: Readonly<Record<string, FileKind>> = {
  '.pdf': 'pdf', '.png': 'image', '.jpg': 'image', '.jpeg': 'image', '.gif': 'image', '.webp': 'image', '.docx': 'word', '.xlsx': 'excel', '.vsdx': 'visio',
};

/** The text files read into the field rather than joined: a prompt prepared in a file. */
export const TEXT_EXTENSIONS: readonly string[] = ['.txt', '.md', '.markdown'];

const MEGABYTE = 1024 * 1024;

export function extensionOf(name: string): string {
  const dot = name.lastIndexOf('.');
  return dot < 0 ? '' : name.slice(dot).toLowerCase();
}

export function kindOf(name: string): FileKind | null {
  return KINDS[extensionOf(name)] ?? null;
}

/** A size in megabytes rounded to the tenth, as the refusals say it. */
const megabytes = (bytes: number) => Math.round((bytes / MEGABYTE) * 10) / 10;

/**
 * The files the panel joins, in their order, after those of the conversation (`joined`), and those it refuses with their
 * reason: a former format of Office, another kind, an empty file, a file past the bound of its kind, past the number of
 * files of a conversation or past their whole.
 */
export function choose(candidates: readonly File[], joined: readonly { readonly size: number }[], bounds: FileBounds): { accepted: File[]; refusals: FileRefusal[] } {
  const accepted: File[] = [];
  const refusals: FileRefusal[] = [];
  let count = joined.length;
  let total = joined.reduce((sum, file) => sum + file.size, 0);
  for (const file of candidates) {
    const extension = extensionOf(file.name);
    const kind = kindOf(file.name);
    if (Object.hasOwn(bounds.outdated, extension)) refusals.push({ reason: 'outdated', name: file.name, format: bounds.outdated[extension] });
    else if (kind === null || !bounds.extensions.includes(extension)) refusals.push({ reason: 'format', name: file.name });
    else if (file.size === 0) refusals.push({ reason: 'unreadable', name: file.name });
    else if (file.size > (kind === 'image' ? bounds.image_bytes : bounds.file_bytes)) {
      refusals.push({ reason: 'too_large', name: file.name, max: megabytes(kind === 'image' ? bounds.image_bytes : bounds.file_bytes) });
    } else if (count >= bounds.files) refusals.push({ reason: 'too_many', name: file.name, max: bounds.files });
    else if (total + file.size > bounds.total_bytes) refusals.push({ reason: 'too_heavy', name: file.name, max: megabytes(bounds.total_bytes) });
    else {
      accepted.push(file);
      count += 1;
      total += file.size;
    }
  }
  return { accepted, refusals };
}

/** The content of a file in base64, as the host reads it. */
export function base64Of(file: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const url = typeof reader.result === 'string' ? reader.result : '';
      resolve(url.slice(url.indexOf(',') + 1));
    };
    reader.onerror = () => {
      reject(reader.error ?? new Error('unreadable'));
    };
    reader.readAsDataURL(file);
  });
}

/** A size as a person reads it: kilobytes under a megabyte, megabytes to the tenth beyond, in the language given. */
export function sizeOf(bytes: number, language: string): { readonly unit: 'kb' | 'mb'; readonly size: string } {
  if (bytes < MEGABYTE) return { unit: 'kb', size: new Intl.NumberFormat(language).format(Math.max(1, Math.ceil(bytes / 1024))) };
  return { unit: 'mb', size: new Intl.NumberFormat(language, { maximumFractionDigits: 1 }).format(bytes / MEGABYTE) };
}

/** The file is sent as it is (a PDF, an image): in the copy and paste, the person joins it in their AI. */
export const isBinary = (kind: FileKind): boolean => kind === 'pdf' || kind === 'image';
