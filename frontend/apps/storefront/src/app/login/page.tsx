import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Container } from '@/components/layout/Container';
import { LoginForm } from '@/components/auth/LoginForm';

export const metadata: Metadata = {
  // The description always said this screen does both; the title and the
  // visible copy did not, which is why the shop looked like it had no sign-up.
  title: 'ورود یا ثبت‌نام',
  description: 'ورود یا ثبت‌نام در فروشگاه بوژان با شماره موبایل.',
};

/**
 * Screen 09 — Sign in.
 *
 * The form reads `?next=` to know where to return the customer after
 * verification, so it needs a boundary for the page to stay static.
 */
export default function LoginPage() {
  return (
    <Container className="flex min-h-[70vh] items-center justify-center py-lg md:py-xl">
      <Suspense fallback={<div className="h-[420px] w-full max-w-md rounded-xl bg-surface-container" />}>
        <LoginForm />
      </Suspense>
    </Container>
  );
}
