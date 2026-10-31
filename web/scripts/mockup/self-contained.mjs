// self-contained.mjs
// Lists the resources a page takes from outside itself (relative or absolute path, network URL): the mockup stays one file (D14).

/** Values of src, href, xlink:href, poster, srcset, data and action attributes, of url() and of @import rules. */
function references(html) {
  const attributes = html.matchAll(/(?<![\w-])(?:src|href|xlink:href|poster|srcset|data|action)\s*=\s*(["']?)([^"'\s>]*)\1/gi);
  const urls = html.matchAll(/url\(\s*(["']?)([^"')]*)\1\s*\)/gi);
  return [...[...attributes].map((m) => m[2] ?? ''), ...[...urls].map((m) => m[2] ?? '')];
}

/** Every reference that is neither an anchor of the page (#id) nor an embedded data: URL, and every @import. */
export function outsideResources(html) {
  const outside = references(html).filter((ref) => !/^(?:#|data:)/i.test(ref.trim()));
  const imports = [...html.matchAll(/@import\b[^;]*/gi)].map((m) => m[0]);
  return [...outside, ...imports];
}
