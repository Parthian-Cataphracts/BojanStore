/**
 * What a failed write tells the person who made it.
 *
 * These are pinned because the failure they were written for was invisible: the
 * panel's live-chat reply returned 404 from a tab left open across a deploy, and
 * the operator was shown "ارسال پاسخ انجام نشد" — true, useless, and giving no
 * hint that reloading would fix it. Every message here has to answer "what do I
 * do now?", and a test is the only thing that keeps that true.
 */

import { describe, expect, it } from 'vitest';

import { retryAfterSeconds, submitErrorMessage } from './submit-errors.js';

describe('submitErrorMessage', () => {
  it('tells a stale tab to reload', () => {
    expect(submitErrorMessage(404)).toContain('تازه کنید');
  });

  it('tells an expired session to sign in again', () => {
    expect(submitErrorMessage(401)).toContain('وارد شوید');
  });

  it('names the owner as the way to get a missing permission', () => {
    expect(submitErrorMessage(403)).toContain('مالک');
  });

  it('says a server fault is not the reader’s fault', () => {
    const message = submitErrorMessage(500);

    expect(message).toContain('از سمت سرور');
    expect(message).toContain('نیست');
  });

  it('treats every 5xx the same way', () => {
    for (const status of [500, 502, 503, 504]) {
      expect(submitErrorMessage(status)).toBe(submitErrorMessage(500));
    }
  });

  it('quotes the wait when the server said how long', () => {
    expect(submitErrorMessage(429, { retryAfterSeconds: 30 })).toContain('30 ثانیه');
  });

  it('still asks for patience when it did not', () => {
    expect(submitErrorMessage(429)).toContain('کمی صبر کنید');
  });

  /** Zero is this codebase's "the request never left the browser". */
  it('points a failed connection at the connection', () => {
    expect(submitErrorMessage(0)).toContain('اتصال اینترنت');
  });

  it('tells a conflicting edit to reload rather than retry blindly', () => {
    expect(submitErrorMessage(409)).toContain('تازه کنید');
  });

  /** Anything unmapped still gets a sentence rather than an empty banner. */
  it('falls back to something readable', () => {
    expect(submitErrorMessage(418)).toBe('درخواست انجام نشد. دوباره تلاش کنید.');
  });

  it('never answers with an empty string', () => {
    for (const status of [0, 400, 401, 403, 404, 409, 413, 418, 429, 500, 503]) {
      expect(submitErrorMessage(status).length).toBeGreaterThan(10);
    }
  });
});

describe('retryAfterSeconds', () => {
  it('reads a plain count', () => {
    expect(retryAfterSeconds(new Headers({ 'Retry-After': '45' }))).toBe(45);
  });

  it('is undefined when the header is absent', () => {
    expect(retryAfterSeconds(new Headers())).toBeUndefined();
  });

  /** An HTTP-date is legal and not worth parsing — every refusal here sends a count. */
  it('is undefined when the header is not a count', () => {
    expect(retryAfterSeconds(new Headers({ 'Retry-After': 'Wed, 21 Oct 2026 07:28:00 GMT' })))
      .toBeUndefined();
  });

  it('ignores a zero or negative wait', () => {
    expect(retryAfterSeconds(new Headers({ 'Retry-After': '0' }))).toBeUndefined();
    expect(retryAfterSeconds(new Headers({ 'Retry-After': '-5' }))).toBeUndefined();
  });
});
