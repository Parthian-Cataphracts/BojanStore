import { NextResponse } from 'next/server';
import { OTP_COOKIE, SESSION_COOKIE, cookieOptions } from '@/lib/auth/session';

/**
 * Sign out. POST-only on purpose: a GET would let a third-party page log the
 * customer out with an `<img>` tag, and `sameSite: lax` does not cover GET.
 */
export async function POST() {
  const response = NextResponse.json({ ok: true });
  response.cookies.set(SESSION_COOKIE, '', { ...cookieOptions, maxAge: 0 });
  response.cookies.set(OTP_COOKIE, '', { ...cookieOptions, maxAge: 0 });
  return response;
}
