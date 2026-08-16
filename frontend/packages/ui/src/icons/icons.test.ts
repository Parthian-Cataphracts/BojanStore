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
const certain = collectIconUses({ certainOnly: true }) as Map<string, string[]>;

describe('icon font subset', () => {
  /*
    Asserted against the `icon`-prop uses only, which are icons and nothing
    else. The check used to run over every collected name and tell icons from
    form fields by whether the name had an underscore in it — and single-word
    icons have none, so `analytics`, `assessment` and `collections` each shipped
    as their own name in literal text with this test passing. `assessment` had
    been drawn as the word "assessment" beside a Persian heading since the
    export screen was written.

    The underscore heuristic still guards the loose `name=` set below, where it
    is doing the job it was written for: 48 of those 49 names are form fields.
  */
  it('carries every icon an icon prop names', () => {
    const shippedSet = new Set(shipped as string[]);
    const missing = [...certain].filter(([name]) => !shippedSet.has(name));

    expect(
      missing.map(([name, files]) => `${name} (${files[0]})`),
      'icon props naming a glyph the subset has no ligature for — pick a name the font carries, or rerun `node scripts/build-icon-font.mjs`',
    ).toEqual([]);
  });

  it('carries every multi-part name that looks like an icon', () => {
    const shippedSet = new Set(shipped as string[]);

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
