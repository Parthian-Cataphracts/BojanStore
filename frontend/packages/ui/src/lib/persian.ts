/**
 * Persian text, reduced to the form two people typing the same word agree on.
 *
 * Persian is written several ways for the same word, and none of them is a
 * mistake. «آبرنگ» and «ابرنگ» differ only in whether the writer bothered with
 * the madda; «کیف» is spelled with Persian `ک` or Arabic `ك` depending on the
 * keyboard, and the two are different characters that look identical; `ی` and
 * `ي` likewise. A compound is written with a half-space, a full space, or
 * nothing at all — «نیم‌فاصله», «نیم فاصله», «نیمفاصله». Somebody who types any
 * of these is looking for the same thing.
 *
 * This is the same fold the API applies — `PersianText.Fold` in C# and
 * `bojan_fold` in SQL — kept here so the fixture-backed screens narrow the way
 * the real ones do rather than being stricter than the shop they stand in for.
 */

/** Characters that become another character, paired with what they become. */
const MAPPED =
  'آأإٱ' + // every alef that carries a mark
  'يىئ' + // Arabic yeh, alef maksura, yeh with hamza
  'ك' + //   Arabic kaf
  'ةۀ' + //  teh marbuta, heh with yeh above
  'ؤ' + //   waw with hamza
  '۰۱۲۳۴۵۶۷۸۹' + // Persian digits
  '٠١٢٣٤٥٦٧٨٩'; //  Arabic-Indic digits

const REPLACEMENTS = 'اااا' + 'ییی' + 'ک' + 'هه' + 'و' + '0123456789' + '0123456789';

/**
 * Characters dropped rather than replaced — they change how a word is
 * pronounced or spaced, never which word it is.
 */
const DROPPED =
  'ًٌٍَُِّْٰ' + // harakat, superscript alef
  'ـ' + // tatweel, the decorative stretch
  '‌‍' + // zero-width non-joiner and joiner
  ' \t\r\n'; // a compound is one word however it was typed

/** The comparison form of a string: what both sides of a search go through. */
export function foldPersian(value: string | null | undefined): string {
  if (!value) return '';

  let folded = '';

  for (const character of value) {
    if (DROPPED.includes(character)) continue;

    const index = MAPPED.indexOf(character);
    folded += index >= 0 ? REPLACEMENTS[index] : character.toLowerCase();
  }

  return folded;
}

/** Whether `haystack` contains `needle`, with both folded first. */
export function matchesPersian(haystack: string | null | undefined, needle: string): boolean {
  const folded = foldPersian(needle);
  return folded.length === 0 || foldPersian(haystack).includes(folded);
}
