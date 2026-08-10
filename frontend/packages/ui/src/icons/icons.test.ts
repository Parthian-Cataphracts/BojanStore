/**
 * Guards the subset icon font against the two ways it silently breaks.
 *
 * The shipped font carries only the icons listed in `icons.generated.json`
 * (see `scripts/build-icon-font.mjs`). Because Material Symbols renders an icon
 * from a ligature, a name the font does not carry does not fail loudly — the
 * browser just draws the name as text, and "location_on" appears in the middle
 * of the checkout form. That is exactly how ten icons came to be broken here
 * before the subset existed, so the check is a test rather than a convention.
 *
 * The call sites are found by importing the build script's own collector rather
 * than by repeating its patterns here. This file used to carry a hand-copied
 * set of them under a comment saying they mirrored the script — and they had
 * stopped mirroring it. Both copies were blind to `name={a ? 'x' : 'y'}`, so
 * eleven icons were missing from the font, rendered as their own names in
 * literal text, and this test passed the whole time: it was comparing the font
 * against the same blind spot that built it.
 */

import { describe, expect, it } from 'vitest';

import { collectIconUses } from '../../../../scripts/build-icon-font.mjs';

import shipped from './icons.generated.json';

const uses = collectIconUses() as Map<string, string[]>;

describe('icon font subset', () => {
  it('carries every icon the apps render', () => {
    const shippedSet = new Set(shipped as string[]);

    // A candidate that is not in the font is either a real icon the subset is
    // missing, or a `name=` that was never an icon — a form field, say. Only
    // the first is a failure, and the two are told apart by whether the name
    // looks like a Material Symbols ligature: those are always either multi-part
    // or a known single word already in the manifest.
    const missing = [...uses]
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
    const used = new Set(uses.keys());
    const orphans = (shipped as string[]).filter((name) => !used.has(name));
    expect(orphans, 'icons in the font that no source file names').toEqual([]);
  });
});
