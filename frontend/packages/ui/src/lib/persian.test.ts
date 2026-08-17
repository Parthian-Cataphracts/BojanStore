import { describe, expect, it } from 'vitest';
import { foldPersian, matchesPersian } from './persian';

/**
 * The third copy of the search fold.
 *
 * The other two are `PersianText.Fold` in C# and `bojan_fold` in SQL, and the
 * backend holds those against each other. This one only runs on the
 * fixture-backed screens, but it has to agree with them or a developer's local
 * search behaves differently from the shop's — which is the kind of difference
 * that gets found in production.
 *
 * The cases below are the same ones the backend asserts.
 */
describe('the Persian search fold', () => {
  it.each([
    ['آبرنگ', 'ابرنگ'],
    ['آبرنگ', 'آب‌رنگ'],
    ['آبرنگ', 'آب رنگ'],
    ['كيف', 'کیف'],
    ['مدرسة', 'مدرسه'],
    ['خانۀ', 'خانه'],
    ['مُحَمَّد', 'محمد'],
    ['کـــیف', 'کیف'],
    ['۱۲۳', '123'],
    ['١٢٣', '123'],
    ['BZ-P-01', 'bz-p-01'],
  ])('folds %s and %s together', (typed, stored) => {
    expect(foldPersian(typed)).toBe(foldPersian(stored));
  });

  it.each([
    ['آبرنگ', 'روغنی'],
    ['کیف', 'کفش'],
    ['۱۲۳', '۳۲۱'],
  ])('keeps %s and %s apart', (one, other) => {
    expect(foldPersian(one)).not.toBe(foldPersian(other));
  });

  it('matches a folded needle inside a folded haystack', () => {
    // What the shopper typed on the left, what the shop stored on the right.
    expect(matchesPersian('ست آبرنگ حرفه‌ای ۱۲ رنگ', 'ابرنگ')).toBe(true);
    expect(matchesPersian('مداد رنگی پلی‌کروم', 'پليكروم')).toBe(true);
    expect(matchesPersian('دفتر ۱۲۰ برگ', '120')).toBe(true);
    expect(matchesPersian('ست آبرنگ', 'مداد')).toBe(false);
  });

  it('treats an empty needle as no filter at all', () => {
    // The call sites guard on this too, but a fold that answered "no match" to
    // an empty box would empty every list the moment somebody cleared it.
    expect(matchesPersian('هرچیزی', '')).toBe(true);
    expect(matchesPersian('هرچیزی', '   ')).toBe(true);
  });

  it('survives what a search box can actually hold', () => {
    expect(foldPersian(null)).toBe('');
    expect(foldPersian(undefined)).toBe('');
    expect(foldPersian('')).toBe('');
  });
});
