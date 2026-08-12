/**
 * The footer's trust marks, as the panel stores and edits them.
 *
 * Its own module rather than living beside the settings reader: that one
 * imports the API client, which reaches `next/headers` — server-only, and a
 * build error the moment a `'use client'` form pulls it in. The same reason
 * `lib/invoice-fields` exists.
 */

/** The settings section these live in — the same one the storefront reads. */
export const STORE_SECTION = 'store';

/** One row holding the whole list as JSON. */
export const TRUST_SEALS_KEY = 'trustSeals';

export interface TrustSeal {
  title: string;
  subtitle: string;
  link: string;
  enabled: boolean;
}

/**
 * The stored row as a list the form can edit.
 *
 * Nothing here throws. The value is a settings row like any other — it can be
 * absent on a shop that has never set one, and it can be hand-edited to
 * nonsense by somebody with database access. Either reads as "no marks", which
 * loses an editable list at worst; letting it throw would take down the whole
 * settings screen, including the fields that are fine.
 */
export function parseTrustSeals(raw: unknown): TrustSeal[] {
  if (typeof raw !== 'string' || raw.trim().length === 0) return [];

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return [];
  }

  if (!Array.isArray(parsed)) return [];

  return parsed
    .filter((row): row is Record<string, unknown> => typeof row === 'object' && row !== null)
    .map((row) => ({
      title: text(row.title),
      subtitle: text(row.subtitle),
      link: text(row.link),
      // Absent reads as shown. A row written by an older panel that had no
      // switch is a mark somebody entered to display, not to hide.
      enabled: row.enabled !== false,
    }))
    .filter((seal) => seal.title.length > 0);
}

function text(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}
