// pmapText.ts
// The text of a .pmap opened by hand: what a copy from a conversation adds around it, and the code and name it proposes (annex B 1.6).

/** A project code: 3 to 6 capital letters (A-1.2-09). */
export const PROJECT_CODE = /^[A-Z]{3,6}$/;

/** What a field of project code keeps of what is typed: capital letters, 6 at most (B-1.4-02). */
export const typedCode = (typed: string): string => typed.toUpperCase().replace(/[^A-Z]/g, '').slice(0, 6);

/**
 * Drops a byte order mark and a Markdown block of code around the content, as the host does (B-1.6-06, B-1.6-07). The
 * mark U+FEFF is a blank for `trim`, which drops it with the spaces.
 */
export function cleanContent(text: string): string {
  let clean = text.trim();
  if (clean.startsWith('```')) {
    const end = clean.indexOf('\n');
    clean = end < 0 ? '' : clean.slice(end + 1);
    const close = clean.lastIndexOf('```');
    if (close >= 0) clean = clean.slice(0, close);
    clean = clean.trim();
  }
  return clean;
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);

/** The code and name of the project written in the file, to fill the dialog; empty when unreadable or invalid (B-1.6-08). */
export function headerOf(text: string): { readonly code: string; readonly name: string } {
  try {
    const document: unknown = JSON.parse(cleanContent(text));
    const project = isRecord(document) && isRecord(document.project) ? document.project : {};
    const code = typeof project.code === 'string' && PROJECT_CODE.test(project.code) ? project.code : '';
    const name = typeof project.name === 'string' ? project.name.trim() : '';
    return { code, name };
  } catch {
    return { code: '', name: '' };
  }
}
