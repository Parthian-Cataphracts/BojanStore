import { describe, expect, it } from 'vitest';
import { ApiError } from './client';
import { problemMessage } from './problem';

const problem = (title: string, detail?: string) =>
  new ApiError('request failed', 409, { title, ...(detail ? { detail } : null) });

describe('problemMessage', () => {
  it('names the actual reason instead of asking the shopper to wait', () => {
    // The two that mattered most: waiting does not restock a product, and it
    // does not un-spend a coupon.
    expect(problemMessage(problem('out-of-stock'))).toContain('موجودی');
    expect(problemMessage(problem('coupon-rejected', 'already-used'))).toContain('قبلاً');
  });

  it('prefers the narrower message when the API sent a detail', () => {
    expect(problemMessage(problem('coupon-rejected', 'unknown'))).not.toBe(
      problemMessage(problem('coupon-rejected', 'already-used')),
    );

    // An unrecognised detail still gets the general answer for its key rather
    // than falling through to nothing.
    expect(problemMessage(problem('coupon-rejected', 'something-new'))).toBe(
      problemMessage(problem('coupon-rejected')),
    );
  });

  it('tells the shopper the order exists when the gateway is the thing that failed', () => {
    const message = problemMessage(problem('payment-unavailable', 'BJ-000123'));

    expect(message).toContain('ثبت شد');
    expect(message).toContain('سفارش‌ها');
  });

  it('returns null when there is nothing specific to say', () => {
    // The caller's own copy is a better fallback than a sentence written here
    // for no particular screen.
    expect(problemMessage(problem('some-key-we-do-not-know'))).toBeNull();
    expect(problemMessage(new ApiError('boom', 500, undefined))).toBeNull();
    expect(problemMessage(new Error('boom'))).toBeNull();
    expect(problemMessage(null)).toBeNull();
  });
});
