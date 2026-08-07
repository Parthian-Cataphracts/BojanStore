import { ApiError } from './client';

/**
 * The API's refusals, in words a shopper can act on.
 *
 * Every non-2xx from the backend is RFC 7807 with a machine key in `title` —
 * `out-of-stock`, `coupon-rejected`, `payment-unavailable` — because
 * `BACKEND.md` puts the Persian copy on this side deliberately. The proxies
 * then threw all of it away and answered "کمی بعد دوباره تلاش کنید" for
 * everything, which is not merely vague, it is wrong: waiting will not make a
 * sold-out product available, and it will not make a spent coupon work. The
 * shopper was told to do the one thing that could not help.
 *
 * `detail` narrows a few of them — the API sends `already-used` beside
 * `coupon-rejected`, and a field name beside `invalid-request` — so the pair is
 * matched first and the bare key is the fallback.
 */

const BY_KEY: Record<string, string> = {
  'out-of-stock': 'موجودی این کالا کافی نیست. تعداد را کم کنید یا آن را از سبد بردارید.',
  'coupon-rejected': 'این کد تخفیف قابل استفاده نیست.',
  'payment-unavailable':
    'سفارش شما ثبت شد ولی ارتباط با درگاه پرداخت برقرار نشد. از بخش سفارش‌ها دوباره برای پرداخت اقدام کنید.',
  conflict: 'این عملیات همین حالا انجام شده است.',
  'already-exists': 'این مورد از قبل ثبت شده است.',
  'not-found': 'موردی که خواستید پیدا نشد.',
  forbidden: 'برای این کار دسترسی ندارید.',
  unauthorized: 'برای ادامه وارد حساب کاربری خود شوید.',
  'not-cancellable': 'این سفارش در وضعیتی نیست که بتوان لغوش کرد.',
  'section-not-granted': 'برای این بخش دسترسی ندارید.',
};

const BY_KEY_AND_DETAIL: Record<string, string> = {
  'coupon-rejected:unknown': 'کد تخفیف وارد شده معتبر نیست.',
  'coupon-rejected:already-used': 'شما قبلاً از این کد تخفیف استفاده کرده‌اید.',
  'coupon-rejected:not-applicable': 'این کد تخفیف روی سبد خرید فعلی شما اعمال نمی‌شود.',
  'invalid-request:wallet-balance': 'موجودی کیف پول شما برای این سفارش کافی نیست.',
  'invalid-request:address': 'آدرس انتخاب‌شده معتبر نیست. آدرس دیگری انتخاب کنید.',
  'invalid-request:shipping-method': 'روش ارسال انتخاب‌شده در دسترس نیست.',
  'invalid-request:payment-method': 'روش پرداخت انتخاب‌شده در دسترس نیست.',
  'invalid-request:quantity': 'تعداد انتخاب‌شده مجاز نیست.',
  'invalid-request:lines': 'سبد خرید شما خالی است یا اقلام آن معتبر نیستند.',
};

interface ProblemDetails {
  title?: unknown;
  detail?: unknown;
}

/**
 * What the shopper should be told, or null when the API said nothing specific.
 *
 * Null rather than a generic sentence on purpose: the caller has a message
 * written for its own screen, and that is a better fallback than one written
 * here for none of them.
 */
export function problemMessage(cause: unknown): string | null {
  if (!(cause instanceof ApiError) || typeof cause.body !== 'object' || cause.body === null) {
    return null;
  }

  const { title, detail } = cause.body as ProblemDetails;
  if (typeof title !== 'string') return null;

  if (typeof detail === 'string' && BY_KEY_AND_DETAIL[`${title}:${detail}`]) {
    return BY_KEY_AND_DETAIL[`${title}:${detail}`]!;
  }

  return BY_KEY[title] ?? null;
}
