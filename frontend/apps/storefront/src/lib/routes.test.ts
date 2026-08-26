import { describe, expect, it } from 'vitest';
import { routes, withReturnTo } from './routes';

describe('withReturnTo', () => {
  it('carries the destination a signed-out shopper was headed for', () => {
    // The case the whole helper exists for: filled a basket, pressed «ثبت
    // سفارش», was asked to sign in, and is now being sent to the profile step.
    expect(withReturnTo(routes.completeProfile, routes.checkout)).toBe(
      '/login/complete-profile?next=%2Fcheckout',
    );
  });

  it('leaves the path alone when there is nothing to carry', () => {
    // Somebody who opened the sign-in screen on purpose. A bare `?next=` would
    // be noise in the address bar and a destination of nowhere.
    expect(withReturnTo(routes.completeProfile, null)).toBe(routes.completeProfile);
    expect(withReturnTo(routes.completeProfile, undefined)).toBe(routes.completeProfile);
    expect(withReturnTo(routes.completeProfile, '')).toBe(routes.completeProfile);
  });

  it('keeps a query string on the destination', () => {
    expect(withReturnTo(routes.completeProfile, '/checkout?step=address')).toBe(
      '/login/complete-profile?next=%2Fcheckout%3Fstep%3Daddress',
    );
  });

  it('refuses to carry anything that leaves this site', () => {
    // Validated here as well as on arrival, so an open redirect is never even
    // written into the address bar. Each of these is a shape `safeNextPath`
    // documents: a scheme, protocol-relative, a backslash the browser
    // normalises, and a tab the URL parser strips before reading.
    for (const hostile of [
      'https://evil.example',
      '//evil.example',
      '/\\evil.example',
      '/\t/evil.example',
    ]) {
      expect(withReturnTo(routes.completeProfile, hostile)).toBe(routes.completeProfile);
    }
  });
});
