/**
 * What to tell someone when a write fails.
 *
 * Shared by both apps, because a failure means the same thing on either side of
 * the shop and because two copies of a rule this small are two copies that come
 * to disagree.
 *
 * Every message here answers "what do I do now?". That is the whole point: a
 * failure the reader cannot act on is a failure they will report as "it doesn't
 * work", and the person they report it to will not know either.
 *
 * The server's own message always wins when it sends one — it knows which field
 * was wrong and these do not. These are for the cases where nothing readable
 * comes back at all, which is exactly when the reader is most stuck.
 */

/**
 * @param {number} status
 * @param {{ retryAfterSeconds?: number }} [context]
 * @returns {string}
 */
export function submitErrorMessage(status, context = {}) {
  if (status === 0) {
    return 'ارتباط با سرور برقرار نشد. اتصال اینترنت خود را بررسی کنید و دوباره تلاش کنید.';
  }

  if (status === 401) {
    return 'نشست شما منقضی شده است. دوباره وارد شوید و همین کار را تکرار کنید.';
  }

  if (status === 403) {
    return 'برای این کار دسترسی ندارید. اگر فکر می‌کنید باید داشته باشید، از مالک فروشگاه بخواهید دسترسی این بخش را بدهد.';
  }

  /*
   * A 404 on a write is almost never "this record is gone" — the form was on a
   * page that listed it a moment ago. It is a browser tab left open across a
   * deploy, asking for an address the new build no longer serves.
   *
   * That is not a guess: the panel's live-chat reply and stamp upload both
   * returned exactly this, from a tab opened seventy-five seconds before the
   * containers were replaced, and the operator was told "ارسال پاسخ انجام نشد"
   * with nothing to act on. Reloading fixed both.
   */
  if (status === 404) {
    return 'این صفحه با نسخه‌ی فعلی سایت هماهنگ نیست. صفحه را تازه کنید (F5) و دوباره تلاش کنید.';
  }

  if (status === 409) {
    return 'این مورد هم‌زمان جای دیگری تغییر کرده است. صفحه را تازه کنید تا آخرین وضعیت را ببینید.';
  }

  if (status === 413) {
    return 'حجم فایلی که فرستادید بیش از حد مجاز است.';
  }

  if (status === 429) {
    const seconds = context.retryAfterSeconds;

    return seconds && seconds > 0
      ? `درخواست‌های پیاپی بیش از حد بوده است. ${seconds} ثانیه صبر کنید و دوباره تلاش کنید.`
      : 'درخواست‌های پیاپی بیش از حد بوده است. کمی صبر کنید و دوباره تلاش کنید.';
  }

  // Said plainly that it is not the reader's fault, because the alternative is
  // someone retyping a correct form half a dozen times looking for their
  // mistake.
  if (status >= 500) {
    return 'خطایی از سمت سرور رخ داد. مشکل از اطلاعاتی که وارد کرده‌اید نیست — کمی بعد دوباره تلاش کنید و اگر تکرار شد به پشتیبانی اطلاع دهید.';
  }

  return 'درخواست انجام نشد. دوباره تلاش کنید.';
}

/**
 * Seconds from a `Retry-After` header, when it carries a plain count.
 *
 * The header may also hold an HTTP date, which is not worth parsing here: every
 * refusal this project issues sends a count.
 *
 * @param {Headers} headers
 * @returns {number | undefined}
 */
export function retryAfterSeconds(headers) {
  const raw = headers.get('Retry-After');
  if (!raw) return undefined;

  const seconds = Number.parseInt(raw, 10);
  return Number.isFinite(seconds) && seconds > 0 ? seconds : undefined;
}
