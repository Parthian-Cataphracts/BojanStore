/**
 * Jalali ↔ Gregorian, for the one place the shop has to accept a Persian date
 * rather than merely print one.
 *
 * Everything the customer *reads* goes through `formatDate`, which hands the
 * job to `Intl` and its Persian calendar. A date they have to *pick* needs the
 * conversion to run the other way as well, and `Intl` only formats — so this
 * is the inverse, built from the same source of truth rather than from a
 * second implementation of the calendar.
 *
 * There is no arithmetic here that encodes when a Jalali year is a leap year,
 * where Nowruz falls, or how long Esfand is. Every one of those is asked of
 * `Intl` and confirmed by converting back: a candidate is only accepted when
 * the platform agrees it is the date it was asked for. That is what keeps this
 * correct across the whole range without a table of leap-year breaks that
 * would have to be right in perpetuity, and it is why an impossible date like
 * 31 Esfand is rejected without a rule ever being written for it.
 *
 * Everything is computed in UTC, so a browser east or west of Tehran cannot
 * shift a birth date by a day.
 */

const MS_PER_DAY = 86_400_000;

/** Month names as the picker's header prints them. */
export const JALALI_MONTHS = [
  'فروردین',
  'اردیبهشت',
  'خرداد',
  'تیر',
  'مرداد',
  'شهریور',
  'مهر',
  'آبان',
  'آذر',
  'دی',
  'بهمن',
  'اسفند',
] as const;

/** Saturday first, the way a Persian calendar is drawn. */
export const JALALI_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'] as const;

export interface JalaliDate {
  year: number;
  /** 1–12. */
  month: number;
  /** 1–31. */
  day: number;
}

/*
 * `nu-latn` so the parts come back as ASCII digits regardless of the browser's
 * own numbering default, and `timeZone: 'UTC'` so the calendar day is the one
 * the Date actually holds rather than the one it happens to be locally.
 */
const jalaliParts = new Intl.DateTimeFormat('en-US-u-ca-persian-nu-latn', {
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
  timeZone: 'UTC',
});

/** The Jalali date a Gregorian instant falls on, or null if the input is not a date. */
export function toJalali(date: Date): JalaliDate | null {
  if (Number.isNaN(date.getTime())) return null;

  const parts = new Map<string, string>();
  for (const part of jalaliParts.formatToParts(date)) parts.set(part.type, part.value);

  // `relatedYear` stands in for `year` on ICU builds that treat the calendar
  // as non-solar; `year` is what the Persian calendar actually emits, so it
  // wins where both are present.
  const year = Number(parts.get('year') ?? parts.get('relatedYear'));
  const month = Number(parts.get('month'));
  const day = Number(parts.get('day'));

  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) return null;
  return { year, month, day };
}

/**
 * The Gregorian date a Jalali one names, or null when no such day exists —
 * 31 Esfand, or 30 Esfand outside a leap year.
 */
export function toGregorian(year: number, month: number, day: number): Date | null {
  if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) return null;
  if (month < 1 || month > 12 || day < 1 || day > 31) return null;

  /*
   * Day of the Jalali year: the first six months are 31 days and the next five
   * are 30, without exception. Only Esfand varies, and a day-of-year never has
   * to know its length — the round-trip below is what rejects a day past its
   * end.
   */
  const dayOfYear = month <= 6 ? (month - 1) * 31 + day : 186 + (month - 7) * 30 + day;

  // Nowruz lands on 20 or 21 March in the modern range and has drifted by a
  // day either side historically, so the window is asked rather than assumed.
  for (let march = 19; march <= 22; march += 1) {
    const nowruz = new Date(Date.UTC(year + 621, 2, march));
    const asJalali = toJalali(nowruz);
    if (!asJalali || asJalali.year !== year || asJalali.month !== 1 || asJalali.day !== 1) continue;

    const candidate = new Date(nowruz.getTime() + (dayOfYear - 1) * MS_PER_DAY);
    const back = toJalali(candidate);

    return back && back.year === year && back.month === month && back.day === day
      ? candidate
      : null;
  }

  return null;
}

/** How many days a Jalali month has — Esfand answered by asking, not by a rule. */
export function jalaliMonthLength(year: number, month: number): number {
  if (month <= 6) return 31;
  if (month <= 11) return 30;
  return toGregorian(year, 12, 30) ? 30 : 29;
}

/**
 * Which column the first of a Jalali month falls in, with Saturday as column
 * zero — `getUTCDay()` counts from Sunday, which is one place further on.
 */
export function jalaliMonthStartColumn(year: number, month: number): number {
  const first = toGregorian(year, month, 1);
  return first ? (first.getUTCDay() + 1) % 7 : 0;
}

/** `YYYY-MM-DD` in UTC — the wire format the API parses. */
export function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** Reads a `YYYY-MM-DD` back, rejecting anything else. */
export function fromIsoDate(value: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const date = new Date(`${value}T00:00:00.000Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}
