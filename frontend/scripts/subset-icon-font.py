"""
Prunes the Material Symbols ligature table down to the icons the apps name.

Called by `build-icon-font.mjs`; see that file for why the subset exists.

Subsetting this font is not the usual `--text` job. Every icon is reached by a
ligature spelled out of the plain Latin letters, so the letters have to stay --
and once they do, pyftsubset's layout closure walks back out from them and
retains every icon they can spell, which is all 6,593 of them. Turning the
closure off instead deletes the ligature rules wholesale and the font then
renders each icon as its own literal name.

So the pruning happens here, before pyftsubset runs: every ligature rule whose
output glyph is not a wanted icon is dropped from GSUB. What is left is a font
whose closure is already exactly the wanted set, which pyftsubset can then
subset normally with the closure doing the right thing on its own.
"""

import json
import sys

from fontTools.ttLib import TTFont

LIGATURE_INPUT = "abcdefghijklmnopqrstuvwxyz0123456789_"


def main() -> None:
    source, candidates_path, pruned_path, glyphs_path, manifest_path = sys.argv[1:6]

    font = TTFont(source)
    glyph_order = set(font.getGlyphOrder())
    cmap = font.getBestCmap()

    with open(candidates_path, encoding="utf-8") as handle:
        candidates = json.load(handle)

    # `name=` also matches form-field names, so the font is the authority on
    # what is an icon -- and the test is that the name spells a ligature, not
    # merely that a glyph shares its name. A search field called `q` would pass
    # the weaker test on the strength of the letter q.
    spellings = {}
    for lookup in font["GSUB"].table.LookupList.Lookup:
        for subtable in lookup.SubTable:
            if subtable.__class__.__name__ == "ExtensionSubst":
                subtable = subtable.ExtSubTable
            for first, entries in getattr(subtable, "ligatures", {}).items():
                for lig in entries:
                    spellings[(first, *lig.Component)] = lig.LigGlyph

    letters = {chr(code): cmap[code] for code in map(ord, LIGATURE_INPUT) if code in cmap}

    def spells_icon(name: str) -> bool:
        if any(char not in letters for char in name):
            return False
        return spellings.get(tuple(letters[char] for char in name)) == name

    icons = [name for name in candidates if name in glyph_order and spells_icon(name)]

    keep = set()
    for name in icons:
        keep.add(name)
        # The `filled` prop selects these at render time, so they are as
        # reachable as the outlined glyph and have to survive with it.
        if f"{name}.fill" in glyph_order:
            keep.add(f"{name}.fill")

    for char in LIGATURE_INPUT:
        glyph = cmap.get(ord(char))
        if glyph:
            keep.add(glyph)

    kept_rules = 0
    dropped_rules = 0
    for lookup in font["GSUB"].table.LookupList.Lookup:
        for subtable in lookup.SubTable:
            # The ligature lookups are wrapped in extension subtables (type 7),
            # which is how the font keeps its GSUB offsets inside 16 bits.
            if subtable.__class__.__name__ == "ExtensionSubst":
                subtable = subtable.ExtSubTable
            ligatures = getattr(subtable, "ligatures", None)
            if not ligatures:
                continue
            for first, entries in list(ligatures.items()):
                surviving = [lig for lig in entries if lig.LigGlyph in keep]
                dropped_rules += len(entries) - len(surviving)
                kept_rules += len(surviving)
                if surviving:
                    ligatures[first] = surviving
                else:
                    del ligatures[first]

    font.save(pruned_path)

    with open(glyphs_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(sorted(keep)))

    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(icons, handle, indent=2, ensure_ascii=False)
        handle.write("\n")

    print(f"{len(icons)} icons, {len(keep)} glyphs")
    print(f"ligature rules: kept {kept_rules}, dropped {dropped_rules}")


if __name__ == "__main__":
    main()
