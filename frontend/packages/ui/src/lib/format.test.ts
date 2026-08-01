import { describe, expect, it } from 'vitest';
import {
  formatDate,
  formatDateTime,
  formatNumber,
  formatPercent,
  formatPhone,
  formatPrice,
  normalizeDigitsInput,
  toLatinDigits,
  toPersianDigits,
} from './format';

describe('toPersianDigits', () => {
  it('converts every ASCII digit', () => {
    expect(toPersianDigits('0123456789')).toBe('۰۱۲۳۴۵۶۷۸۹');
  });

  it('leaves non-digits untouched', () => {
    expect(toPersianDigits('BZ-1024')).toBe('BZ-۱۰۲۴');
  });

  it('accepts numbers as well as strings', () => {
    expect(toPersianDigits(42)).toBe('۴۲');
  });
});

describe('toLatinDigits', () => {
  it('converts Persian digits back', () => {
    expect(toLatinDigits('۰۹۱۲۳۴۵۶۷۸۹')).toBe('09123456789');
  });

  it('also handles Arabic-Indic digits, which Arabic keyboards produce', () => {
    expect(toLatinDigits('٠١٢٣٤٥٦٧٨٩')).toBe('0123456789');
  });

  it('round-trips with toPersianDigits', () => {
    expect(toLatinDigits(toPersianDigits('1405/05/07'))).toBe('1405/05/07');
  });
});

describe('formatNumber', () => {
  it('groups thousands with an ASCII comma, matching the design', () => {
    // Intl.NumberFormat('fa-IR') would use U+066C here; the design does not.
    expect(formatNumber(1_200_000)).toBe('۱,۲۰۰,۰۰۰');
    expect(formatNumber(1_200_000)).not.toContain('٬');
  });

  it('leaves values under a thousand ungrouped', () => {
    expect(formatNumber(999)).toBe('۹۹۹');
  });

  it('handles zero', () => {
    expect(formatNumber(0)).toBe('۰');
  });
});

describe('formatPrice', () => {
  it('appends the currency unit by default', () => {
    expect(formatPrice(350_000)).toBe('۳۵۰,۰۰۰ تومان');
  });

  it('omits the unit when asked, for use beside a struck-through price', () => {
    expect(formatPrice(350_000, { withUnit: false })).toBe('۳۵۰,۰۰۰');
  });
});

describe('formatPercent', () => {
  it('renders a Persian percent sign', () => {
    expect(formatPercent(25)).toBe('۲۵٪');
  });
});

describe('formatDate', () => {
  it('renders a Jalali date, not a Gregorian one', () => {
    // 2026-07-29 falls in Mordad 1405.
    expect(formatDate('2026-07-29')).toBe('۱۴۰۵/۰۵/۰۷');
  });

  it('renders the long form with a Persian month name', () => {
    expect(formatDate('2026-07-29', 'long')).toContain('مرداد');
  });

  it('accepts Date objects and timestamps as well as strings', () => {
    const iso = '2026-07-29T00:00:00Z';
    expect(formatDate(new Date(iso))).toBe(formatDate(iso));
    expect(formatDate(new Date(iso).getTime())).toBe(formatDate(iso));
  });

  it('returns an empty string for an unparseable value rather than "Invalid Date"', () => {
    expect(formatDate('not a date')).toBe('');
  });
});

describe('formatDateTime', () => {
  it('combines the Jalali date with a 24-hour time', () => {
    const formatted = formatDateTime('2026-07-29T14:30:00Z');
    expect(formatted).toMatch(/^۱۴۰۵\/۰۵\/۰۷ - ۱?[۰-۹]:[۰-۹]{2}$/);
  });

  it('returns an empty string for an unparseable value', () => {
    expect(formatDateTime('nope')).toBe('');
  });
});

describe('normalizeDigitsInput', () => {
  it('strips separators so a pasted phone number still validates', () => {
    expect(normalizeDigitsInput('۰۹۱۲ ۳۴۵ ۶۷۸۹')).toBe('09123456789');
    expect(normalizeDigitsInput('021-8888-8888')).toBe('02188888888');
  });

  it('returns an empty string when there is nothing numeric', () => {
    expect(normalizeDigitsInput('سلام')).toBe('');
  });
});

describe('formatPhone', () => {
  it('groups an 11-digit mobile as 4-3-4', () => {
    expect(formatPhone('09123456789')).toBe('۰۹۱۲ ۳۴۵ ۶۷۸۹');
  });

  it('normalises Persian input before grouping', () => {
    expect(formatPhone('۰۹۱۲۳۴۵۶۷۸۹')).toBe('۰۹۱۲ ۳۴۵ ۶۷۸۹');
  });

  it('falls back to plain digits when the length is not 11', () => {
    expect(formatPhone('123')).toBe('۱۲۳');
  });
});
