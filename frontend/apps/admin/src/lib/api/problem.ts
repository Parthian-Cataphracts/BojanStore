import { ApiError } from './client';

/**
 * The API's refusals, in words an operator can act on.
 *
 * Every non-2xx from the backend is RFC 7807 with a machine key in `title`.
 * The write proxy read `detail?.error`, which the API has never sent, so every
 * refusal fell through to "ذخیره اطلاعات انجام نشد" — a permission the owner
 * has not granted, a coupon code already taken and a genuine server fault all
 * read identically, and none of them said what to do.
 *
 * `detail` narrows a few of them — the API sends a field name beside
 * `invalid-request` — so the pair is matched first and the bare key is the
 * fallback.
 */

const BY_KEY: Record<string, string> = {
  'out-of-stock': 'موجودی کافی نیست.',
  conflict: 'این مقدار از قبل ثبت شده است.',
  'already-exists': 'مقداری که وارد کردید تکراری است.',
  'not-found': 'موردی که خواستید پیدا نشد.',
  forbidden: 'برای این کار دسترسی ندارید.',
  unauthorized: 'نشست شما معتبر نیست. دوباره وارد شوید.',
  'not-cancellable': 'این سفارش در وضعیتی نیست که بتوان لغوش کرد.',
  'section-not-granted': 'این بخش برای نقش شما فعال نشده است. از مالک بخواهید در «نقش‌ها و دسترسی‌ها» آن را بدهد.',
};

const BY_KEY_AND_DETAIL: Record<string, string> = {
  'invalid-request:slug': 'این نامک قبلاً برای مورد دیگری ثبت شده است.',
  'invalid-request:grants': 'دسترسی‌های ارسال‌شده معتبر نیستند.',
  'invalid-request:kind': 'نوع انتخاب‌شده معتبر نیست.',
  'invalid-request:confirm': 'برای این عملیات باید تأیید نهایی را بزنید.',
  'invalid-request:new-password': 'گذرواژه جدید کوتاه‌تر از حد مجاز است.',
  'forbidden:current-password': 'گذرواژه فعلی درست وارد نشده است.',
  'forbidden:code': 'کد تأیید دو مرحله‌ای درست نیست.',
  'conflict:slug': 'این نامک قبلاً برای مورد دیگری ثبت شده است.',
  'invalid-request:file-too-large': 'حجم فایل بیشتر از ۸ مگابایت است.',
  'invalid-request:file-type': 'این فایل تصویر نیست یا فرمتش پشتیبانی نمی‌شود. JPG، PNG، WebP یا GIF بفرستید.',
};

/** The shape the API answers every refusal with. */
export interface ProblemDetails {
  title?: unknown;
  detail?: unknown;
}

/**
 * What the operator should be told, or null when the API said nothing specific.
 *
 * Null rather than a generic sentence on purpose: the caller has a message
 * written for its own screen, and that is a better fallback than one written
 * here for none of them.
 */
export function problemMessage(cause: unknown): string | null {
  // Two callers, two shapes: the typed client throws an `ApiError` carrying the
  // parsed body, while the write proxy has the body itself because it forwards
  // the upstream status verbatim.
  const problem = cause instanceof ApiError ? cause.body : cause;

  if (typeof problem !== 'object' || problem === null) {
    return null;
  }

  const { title, detail } = problem as ProblemDetails;
  if (typeof title !== 'string') return null;

  if (typeof detail === 'string' && BY_KEY_AND_DETAIL[`${title}:${detail}`]) {
    return BY_KEY_AND_DETAIL[`${title}:${detail}`]!;
  }

  return BY_KEY[title] ?? null;
}
