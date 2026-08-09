import { describe, expect, it } from 'vitest';

import {
  fromIsoDate,
  jalaliMonthLength,
  jalaliMonthStartColumn,
  toGregorian,
  toIsoDate,
  toJalali,
} from './jalali';

/** `toGregorian` as the ISO string the form actually posts. */
function iso(year: number, month: number, day: number): string | null {
  const date = toGregorian(year, month, day);
  return date ? toIsoDate(date) : null;
}

describe('toJalali', () => {
  it('converts Nowruz', () => {
    expect(toJalali(new Date('2024-03-20T00:00:00Z'))).toEqual({ year: 1403, month: 1, day: 1 });
  });

  it('converts the day before Nowruz to the last of Esfand', () => {
    expect(toJalali(new Date('2024-03-19T00:00:00Z'))).toEqual({ year: 1402, month: 12, day: 29 });
  });

  it('is unaffected by the time of day', () => {
    expect(toJalali(new Date('2024-03-20T23:59:59Z'))).toEqual({ year: 1403, month: 1, day: 1 });
  });

  it('returns null for an invalid date', () => {
    expect(toJalali(new Date('nonsense'))).toBeNull();
  });
});

describe('toGregorian', () => {
  it('converts Nowruz back', () => {
    expect(iso(1403, 1, 1)).toBe('2024-03-20');
  });

  /** Nowruz 1373 fell on 21 March, a day later than 1403's — the window has to cover both. */
  it('handles a year whose Nowruz is on the 21st', () => {
    expect(iso(1373, 1, 1)).toBe('1994-03-21');
    expect(iso(1373, 2, 28)).toBe('1994-05-18');
  });

  it('converts a date in the 30-day half of the year', () => {
    expect(iso(1403, 7, 1)).toBe('2024-09-22');
  });

  it('round-trips every month boundary of a leap and a common year', () => {
    for (const year of [1403, 1404]) {
      for (let month = 1; month <= 12; month += 1) {
        const last = jalaliMonthLength(year, month);
        for (const day of [1, last]) {
          const date = toGregorian(year, month, day);
          expect(date, `${year}/${month}/${day}`).not.toBeNull();
          expect(toJalali(date as Date)).toEqual({ year, month, day });
        }
      }
    }
  });

  it('rejects days that do not exist', () => {
    // Esfand never has 31 days, and only has 30 in a leap year.
    expect(toGregorian(1403, 12, 31)).toBeNull();
    expect(toGregorian(1404, 12, 30)).toBeNull();
    // The 30-day half of the year has no 31st either.
    expect(toGregorian(1403, 7, 31)).toBeNull();
  });

  it('rejects out-of-range and non-integer input', () => {
    expect(toGregorian(1403, 0, 1)).toBeNull();
    expect(toGregorian(1403, 13, 1)).toBeNull();
    expect(toGregorian(1403, 1, 0)).toBeNull();
    expect(toGregorian(1403, 1, 1.5)).toBeNull();
  });
});

describe('jalaliMonthLength', () => {
  it('is 31 for the first six months and 30 for the next five', () => {
    for (let month = 1; month <= 6; month += 1) expect(jalaliMonthLength(1403, month)).toBe(31);
    for (let month = 7; month <= 11; month += 1) expect(jalaliMonthLength(1403, month)).toBe(30);
  });

  it('gives Esfand 30 days in a leap year and 29 otherwise', () => {
    expect(jalaliMonthLength(1403, 12)).toBe(30);
    expect(jalaliMonthLength(1404, 12)).toBe(29);
  });

  it('agrees with the calendar over a century', () => {
    for (let year = 1320; year <= 1420; year += 1) {
      const length = jalaliMonthLength(year, 12);
      expect(toGregorian(year, 12, length), `${year}`).not.toBeNull();
      expect(toGregorian(year, 12, length + 1), `${year}`).toBeNull();
    }
  });
});

describe('jalaliMonthStartColumn', () => {
  /** Saturday is column zero, so 1 Farvardin 1403 — a Wednesday — is column four. */
  it('counts from Saturday', () => {
    expect(jalaliMonthStartColumn(1403, 1)).toBe(4);
  });

  it('always lands inside the week', () => {
    for (let month = 1; month <= 12; month += 1) {
      const column = jalaliMonthStartColumn(1403, month);
      expect(column).toBeGreaterThanOrEqual(0);
      expect(column).toBeLessThanOrEqual(6);
    }
  });
});

describe('iso helpers', () => {
  it('round-trips', () => {
    const date = fromIsoDate('1994-05-18');
    expect(date).not.toBeNull();
    expect(toIsoDate(date as Date)).toBe('1994-05-18');
  });

  it('rejects anything that is not YYYY-MM-DD', () => {
    expect(fromIsoDate('1373/02/28')).toBeNull();
    expect(fromIsoDate('18-05-1994')).toBeNull();
    expect(fromIsoDate('')).toBeNull();
  });
});
