import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Container } from '@/components/layout/Container';
import { RegisterForm } from '@/components/auth/RegisterForm';

export const metadata: Metadata = {
  title: 'ثبت‌نام',
  description: 'ساخت حساب کاربری در فروشگاه بوژان با شماره موبایل، ایمیل و رمز عبور.',
};

/**
 * Screen 10 — register.
 *
 * Its own address rather than a tab inside sign-in, so it can be linked to,
 * bookmarked, and reached with a `?next=` that survives the trip. The form
 * reads that parameter, so it needs a boundary for the page to stay static.
 */
export default function RegisterPage() {
  return (
    <Container className="flex min-h-[70vh] items-center justify-center py-lg md:py-xl">
      <Suspense
        fallback={<div className="h-[520px] w-full max-w-md rounded-xl bg-surface-container" />}
      >
        <RegisterForm />
      </Suspense>
    </Container>
  );
}
