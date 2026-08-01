/**
 * Serialise a JSON-LD payload for embedding in a `<script>` block.
 *
 * `JSON.stringify` does not escape `<`, so a product title or an article body
 * containing `</script>` would close the block early and let whatever followed
 * be parsed as markup — an injection route straight through any field the
 * catalogue or the CMS controls. Escaping the characters that can begin a tag,
 * plus the two line separators that are valid in JSON but not in JavaScript
 * string literals, makes the payload inert while leaving it valid JSON.
 */
export function serializeJsonLd(data: unknown): string {
  return JSON.stringify(data).replace(
    /[<>&\u2028\u2029]/g,
    (character) => `\\u${character.charCodeAt(0).toString(16).padStart(4, '0')}`,
  );
}
