/**
 * Every internal link in the storefront has to point at a route that exists.
 *
 * The panel had a button that linked to a route which never existed, for as
 * long as its screen had. Nothing catches that on its own: it type-checks, it
 * lints, and it renders as a perfectly ordinary button. The only way to find
 * such a link is to click it, and the only way to keep it found is a test that
 * walks the links. The storefront gets the same guard as the panel.
 */

import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const appDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../app');
const srcDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function walk(dir: string, match: (name: string) => boolean): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(full, match);
    return match(entry.name) ? [full] : [];
  });
}

/** Every routable path, with `[param]` segments left in place. */
function routes(): string[][] {
  return walk(appDir, (name) => name === 'page.tsx' || name === 'route.ts').map((file) =>
    path
      .relative(appDir, path.dirname(file))
      .split(path.sep)
      .filter((segment) => segment.length > 0),
  );
}

/** Route groups — `(shell)` — are organisational and not part of the URL. */
const isGroup = (segment: string) => segment.startsWith('(') && segment.endsWith(')');

function resolves(href: string, table: string[][]): boolean {
  const wanted = href.split(/[?#]/)[0]!.split('/').filter(Boolean);
  if (wanted.length === 0) return true;

  return table.some((route) => {
    const actual = route.filter((segment) => !isGroup(segment));
    if (actual.length !== wanted.length) return false;
    return actual.every((segment, i) => segment.startsWith('[') || segment === wanted[i]);
  });
}

describe('storefront navigation', () => {
  it('never links to a route that does not exist', () => {
    const table = routes();
    const broken: string[] = [];

    for (const file of walk(srcDir, (name) => /\.tsx?$/.test(name) && !name.includes('.test.'))) {
      const text = readFileSync(file, 'utf8');
      // Literal internal targets only: both the JSX attribute and the `href:`
      // field of the nav tables. Anything built from a variable at runtime is
      // beyond what a static check can follow.
      const patterns = [/href="(\/[^"{}]*)"/g, /href:\s*'(\/[^']*)'/g];

      for (const pattern of patterns) {
        for (const match of text.matchAll(pattern)) {
          const href = match[1]!;
          // Anything with an extension on its last segment is a file served
          // from public/ — the preloaded icon font, say — and not a route.
          const last = href.split(/[?#]/)[0]!.split('/').pop() ?? '';
          if (last.includes('.')) continue;
          if (!resolves(href, table)) {
            broken.push(`${path.relative(srcDir, file)} -> ${href}`);
          }
        }
      }
    }

    expect(broken, 'links whose target is not a route').toEqual([]);
  });
});
