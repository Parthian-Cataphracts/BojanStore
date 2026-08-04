/**
 * Guards the subset icon font against the two ways it silently breaks.
 *
 * The shipped font carries only the icons listed in `icons.generated.json`
 * (see `scripts/build-icon-font.mjs`). Because Material Symbols renders an icon
 * from a ligature, a name the font does not carry does not fail loudly — the
 * browser just draws the name as text, and "location_on" appears in the middle
 * of the checkout form. That is exactly how ten icons came to be broken here
 * before the subset existed, so the check is a test rather than a convention.
 */

import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

import shipped from './icons.generated.json';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');

const SOURCE_ROOTS = ['apps/storefront/src', 'apps/admin/src', 'packages/ui/src'].map((dir) =>
  path.join(root, dir),
);

/** Mirrors the extraction in `scripts/build-icon-font.mjs`. */
const PATTERNS = [
  /\b(?:name|icon)=["']([a-z0-9_]+)["']/g,
  /\b(?:name|icon)=\{["']([a-z0-9_]+)["']\}/g,
  /\bicon:\s*["']([a-z0-9_]+)["']/g,
];

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    return /\.tsx?$/.test(entry.name) ? [full] : [];
  });
}

function usedNames(): Map<string, string[]> {
  const uses = new Map<string, string[]>();
  for (const dir of SOURCE_ROOTS) {
    for (const file of sourceFiles(dir)) {
      if (file.endsWith('.test.ts') || file.endsWith('.test.tsx')) continue;
      const text = readFileSync(file, 'utf8');
      for (const pattern of PATTERNS) {
        for (const match of text.matchAll(pattern)) {
          const name = match[1];
          if (!name) continue;
          uses.set(name, [...(uses.get(name) ?? []), path.relative(root, file)]);
        }
      }
    }
  }
  return uses;
}

describe('icon font subset', () => {
  it('carries every icon the apps render', () => {
    const shippedSet = new Set(shipped as string[]);

    // A candidate that is not in the font is either a real icon the subset is
    // missing, or a `name=` that was never an icon — a form field, say. Only
    // the first is a failure, and the two are told apart by whether the name
    // looks like a Material Symbols ligature: those are always either multi-part
    // or a known single word already in the manifest.
    const missing = [...usedNames()]
      .filter(([name]) => !shippedSet.has(name) && name.includes('_'))
      // Form fields are named after the field, not an icon; these are the ones
      // in this codebase that happen to contain an underscore.
      .filter(([name]) => !['otp_code', 'postal_code'].includes(name));

    expect(
      missing.map(([name, files]) => `${name} (${files[0]})`),
      'icon names with no glyph in the subset — rerun `node scripts/build-icon-font.mjs`',
    ).toEqual([]);
  });

  it('does not ship glyphs nothing references', () => {
    const used = new Set(usedNames().keys());
    const orphans = (shipped as string[]).filter((name) => !used.has(name));
    expect(orphans, 'icons in the font that no source file names').toEqual([]);
  });
});
