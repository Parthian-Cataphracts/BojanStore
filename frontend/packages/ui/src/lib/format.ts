/**
 * Persian formatting helpers.
 *
 * The design renders numbers with Persian digits and an ASCII thousands
 * separator, which is *not* what
 * `Intl.NumberFormat('fa-IR')` produces — it uses U+066C as the group
 * separator. So we group in en-US and transliterate the digits ourselves.
 */

const PERSIAN_DIGITS = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'] as const;

/** Convert every ASCII digit in a string to its Persian counterpart. */
export function toPersianDigits(value: string | number): string {
  return String(value).replace(/\d/g, (d) => PERSIAN_DIGITS[Number(d)] ?? d);
}

/** Convert Persian (and Arabic-Indic) digits back to ASCII — for form input. */
export function toLatinDigits(value: string): string {
  return value
    .replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x06f0))
    .replace(/[٠-٩]/g, (d) => String(d.charCodeAt(0) - 0x0660));
}

/**
 * Whether to leave the digits in ASCII instead of transliterating them.
 *
 * For documents rather than for the interface. The invoice is the only place
 * that asks for it: it is read as a financial record, filed and typed back into
 * an accounting system, so its figures are set in Latin throughout — a printed
 * amount someone has to re-key is not the place to make them transliterate.
 * Everything a shopper reads on screen stays Persian, which is what the design
 * specifies.
 */
export interface DigitOptions {
  latin?: boolean;
}

/** Group in thousands, then render the digits in Persian — or leave them Latin. */
export function formatNumber(value: number, options: DigitOptions = {}): string {
  const grouped = new Intl.NumberFormat('en-US').format(value);
  return options.latin ? grouped : toPersianDigits(grouped);
}

/** Same as formatNumber, with the Toman unit appended by default. */
export function formatPrice(
  value: number,
  options: { withUnit?: boolean } & DigitOptions = {},
): string {
  const { withUnit = true, latin = false } = options;
  const amount = formatNumber(value, { latin });
  return withUnit ? `${amount} تومان` : amount;
}

/** Percentage in Persian digits — the discount badge format used on product cards. */
export function formatPercent(value: number): string {
  return `${toPersianDigits(value)}٪`;
}

/**
 * Jalali date in yyyy/MM/dd form. Uses the platform's Persian calendar so we
 * do not ship a conversion table.
 */
export function formatDate(
  input: Date | string | number,
  style: 'short' | 'long' = 'short',
  options: DigitOptions = {},
): string {
  const date = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(date.getTime())) return '';

  // `nu-latn` keeps the Jalali calendar and swaps only the numbering system,
  // so a Latin-digit invoice still carries the Persian date the rest of the
  // shop shows — not a Gregorian one.
  return new Intl.DateTimeFormat(
    options.latin ? 'fa-IR-u-ca-persian-nu-latn' : 'fa-IR-u-ca-persian',
    style === 'long'
      ? { year: 'numeric', month: 'long', day: 'numeric' }
      : { year: 'numeric', month: '2-digit', day: '2-digit' },
  ).format(date);
}

/** Jalali date plus 24-hour time — the timestamp format used in orders and admin tables. */
export function formatDateTime(input: Date | string | number): string {
  const date = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(date.getTime())) return '';

  const time = new Intl.DateTimeFormat('fa-IR', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);

  return `${formatDate(date)} - ${toPersianDigits(time)}`;
}

/** Strip everything but digits, normalising Persian input first. */
export function normalizeDigitsInput(value: string): string {
  return toLatinDigits(value).replace(/\D/g, '');
}

/** Group an 11-digit mobile number as 4-3-4 for display. */
export function formatPhone(value: string): string {
  const digits = normalizeDigitsInput(value);
  if (digits.length !== 11) return toPersianDigits(digits);
  return toPersianDigits(`${digits.slice(0, 4)} ${digits.slice(4, 7)} ${digits.slice(7)}`);
}
