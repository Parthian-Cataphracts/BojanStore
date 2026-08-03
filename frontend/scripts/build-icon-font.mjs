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

import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

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
    return /\.tsx?$/.test(entry.name) ? [full] : [];
  });
}

/**
 * Icon names that can be read straight out of the source.
 *
 * Three shapes cover every call site: the `name`/`icon` props on the shared
 * components, and the `icon:` field of the data objects the pages map over.
 * Names that arrive from the API at runtime cannot be seen here — see the
 * note in `Icon.tsx` about why the admin icon field is a fixed list.
 */
export function collectIconNames() {
  const patterns = [
    /\b(?:name|icon)=["']([a-z0-9_]+)["']/g,
    /\b(?:name|icon)=\{["']([a-z0-9_]+)["']\}/g,
    /\bicon:\s*["']([a-z0-9_]+)["']/g,
  ];

  const found = new Set();
  for (const dir of SOURCE_ROOTS) {
    for (const file of sourceFiles(dir)) {
      const text = readFileSync(file, 'utf8');
      for (const pattern of patterns) {
        for (const match of text.matchAll(pattern)) found.add(match[1]);
      }
    }
  }
  return [...found].sort();
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

  const before = statSync(UPSTREAM).size;
  const after = subset.length;
  const saved = (100 * (1 - after / before)).toFixed(1);
  console.log(
    `${(before / 1024).toFixed(0)} KB -> ${(after / 1024).toFixed(0)} KB (${saved}% smaller)`,
  );
}

main();
