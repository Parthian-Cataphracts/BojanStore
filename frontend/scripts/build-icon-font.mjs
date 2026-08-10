/**
 * Rebuilds the subset Material Symbols font from the full upstream file.
 *
 * The upstream variable font carries 6,593 glyphs and weighs 1.1 MB. The two
 * apps between them name fewer than 200 icons, so every visitor was downloading
 * roughly fifty times the glyph data they would ever see — and doing it after
 * the stylesheet parsed, which put it squarely on the critical path.
 *
 * This script scans the source for every icon name that can be statically
 * known, resolves each to a glyph in the font, and writes a subset containing
 * only those (plus their `.fill` variants and the Latin letters the ligature
 * lookups need as input). `icons.generated.json` is written alongside it so the
 * unit test can assert that no name in the source is missing from the font —
 * a typo'd or renamed icon otherwise fails silently, rendering as its own
 * literal text.
 *
 * Requires fonttools (`pip install fonttools[woff]`). Run after adding an icon:
 *   node scripts/build-icon-font.mjs
 */

import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const UPSTREAM = path.join(root, 'assets/material-symbols-full.woff2');
const OUTPUTS = [
  path.join(root, 'apps/storefront/public/fonts/material-symbols.woff2'),
  path.join(root, 'apps/admin/public/fonts/material-symbols.woff2'),
];
const MANIFEST = path.join(root, 'packages/ui/src/icons/icons.generated.json');

const SOURCE_ROOTS = [
  path.join(root, 'apps/storefront/src'),
  path.join(root, 'apps/admin/src'),
  path.join(root, 'packages/ui/src'),
];

/** Every character a Material Symbols ligature name can be spelled with. */
const LIGATURE_INPUT = 'abcdefghijklmnopqrstuvwxyz0123456789_';

function sourceFiles(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    // Tests are excluded: they name icons to assert about them, and a glyph
    // shipped only because a test mentions it is dead weight in every bundle.
    if (/\.test\.tsx?$/.test(entry.name)) return [];
    return /\.tsx?$/.test(entry.name) ? [full] : [];
  });
}

/**
 * Icon names that can be read straight out of the source.
 *
 * Three shapes cover every call site: the `name`/`icon` props on the shared
 * components — as a plain string or as a braced expression — and the `icon:`
 * field of the data objects the pages map over. Names that arrive from the API
 * at runtime cannot be seen here — see the note in `Icon.tsx` about why the
 * admin icon field is a fixed list.
 *
 * The braced expression is the shape that used to be missed, and it was missed
 * silently. `name={open ? 'a' : 'b'}` matched none of the old patterns, so
 * neither glyph reached the subset and the component rendered its own ligature
 * name as literal text — three hundred and sixty pixels of
 * `radio_button_unchecked`, which is how the order screen came to overflow on a
 * phone. Eleven names were in that state, the password reveal on the panel's
 * sign-in screen among them.
 *
 * Nothing caught it because the unit test had its own hand-copied version of
 * these patterns, so the test that existed to find the bug had the bug. It
 * imports this now: one description of what an icon use looks like, and the
 * test fails when this and the shipped font disagree rather than when two
 * copies of a regular expression do.
 */
export function collectIconUses() {
  const direct = [
    /\b(?:name|icon)=["']([a-z0-9_]+)["']/g,
    /\bicon:\s*["']([a-z0-9_]+)["']/g,
  ];

  /** A braced prop, whose expression may name more than one icon. */
  const braced = /\b(?:name|icon)=\{([^{}]*)\}/g;

  /**
   * The right-hand side of a comparison is a value being tested, not an icon:
   * `step === 'otp' ? 'sms' : 'person'` names `sms` and `person`, and `otp` is
   * a step. Dropped before the remaining literals are collected, or the subset
   * grows a glyph for every string any icon expression happens to compare
   * against.
   */
  const comparison = /(?:===|!==|==|!=)\s*["'][^"']*["']/g;

  const literal = /["']([a-z][a-z0-9_]+)["']/g;

  /** Every name, against the files that ask for it. */
  const uses = new Map();
  const note = (name, file) => uses.set(name, [...(uses.get(name) ?? []), file]);

  for (const dir of SOURCE_ROOTS) {
    for (const file of sourceFiles(dir)) {
      const text = readFileSync(file, 'utf8');
      const relative = path.relative(root, file);

      for (const pattern of direct) {
        for (const match of text.matchAll(pattern)) note(match[1], relative);
      }

      for (const match of text.matchAll(braced)) {
        const branches = match[1].replace(comparison, '');
        for (const name of branches.matchAll(literal)) note(name[1], relative);
      }
    }
  }

  return uses;
}

/** Just the names, sorted — what the subsetter is handed. */
export function collectIconNames() {
  return [...collectIconUses().keys()].sort();
}

function main() {
  const candidates = collectIconNames();

  const work = mkdtempSync(path.join(tmpdir(), 'iconfont-'));
  const candidatesFile = path.join(work, 'candidates.json');
  const prunedFile = path.join(work, 'pruned.ttf');
  const glyphsFile = path.join(work, 'glyphs.txt');

  writeFileSync(candidatesFile, JSON.stringify(candidates), 'utf8');

  // Drops the ligature rules for every icon the apps never name, so the layout
  // closure below expands to exactly the wanted set rather than the whole font.
  execFileSync(
    'python',
    [
      path.join(root, 'scripts/subset-icon-font.py'),
      UPSTREAM,
      candidatesFile,
      prunedFile,
      glyphsFile,
      MANIFEST,
    ],
    { stdio: 'inherit' },
  );

  execFileSync(
    'pyftsubset',
    [
      prunedFile,
      `--glyphs-file=${glyphsFile}`,
      // `liga` is what turns the text "shopping_cart" into its glyph; the
      // others are the shaping features the retained Latin glyphs rely on.
      '--layout-features=liga,calt,rlig,ccmp',
      // FILL and wght are the two axes `Icon` varies, so the variation data
      // has to survive — without this the font flattens to a single weight.
      '--drop-tables-=fvar,gvar,avar,STAT',
      '--flavor=woff2',
      `--output-file=${OUTPUTS[0]}`,
    ],
    { stdio: 'inherit' },
  );

  const icons = JSON.parse(readFileSync(MANIFEST, 'utf8'));

  const subset = readFileSync(OUTPUTS[0]);
  for (const target of OUTPUTS.slice(1)) writeFileSync(target, subset);

  stampVersion(subset);

  const before = statSync(UPSTREAM).size;
  const after = subset.length;
  const saved = (100 * (1 - after / before)).toFixed(1);
  console.log(
    `${icons.length} icons — ${(before / 1024).toFixed(0)} KB -> ${(after / 1024).toFixed(0)} KB (${saved}% smaller)`,
  );
}

/**
 * Writes the font's own content hash into every URL that asks for it.
 *
 * `/fonts/*` is served `immutable, max-age=31536000`, which is right for a file
 * whose URL changes when its content does — and this one's did not. So a
 * visitor who had ever loaded the site kept the font they already had for a
 * year: an icon added today would render as its own name in literal text for
 * every returning user, which is the exact failure this subset already has a
 * history of. Rebuilding the font silently did nothing for anyone but a first
 * visit.
 *
 * A query string is enough — it is part of the cache key — and it keeps the
 * file name stable so nothing else has to know about the hash.
 */
function stampVersion(subset) {
  const version = createHash('sha256').update(subset).digest('hex').slice(0, 8);

  const targets = [
    path.join(root, 'apps/storefront/src/app/globals.css'),
    path.join(root, 'apps/admin/src/app/globals.css'),
    path.join(root, 'apps/storefront/src/app/layout.tsx'),
    path.join(root, 'apps/admin/src/app/layout.tsx'),
  ];

  const reference = /\/fonts\/material-symbols\.woff2(\?v=[0-9a-f]+)?/g;

  for (const file of targets) {
    const text = readFileSync(file, 'utf8');
    const stamped = text.replace(reference, `/fonts/material-symbols.woff2?v=${version}`);
    if (stamped !== text) writeFileSync(file, stamped, 'utf8');
  }

  console.log(`font version ${version}`);
}

// Only when run as a script. The unit test imports `collectIconUses` from here
// so there is one description of what an icon use looks like — and without this
// guard that import also rebuilt the font and rewrote the manifest underneath
// whichever tests happened to be reading it.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
