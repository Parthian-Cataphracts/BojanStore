import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Container } from '@/components/layout/Container';
import { EmailVerifyConfirm } from '@/components/account/EmailVerifyConfirm';

export const metadata: Metadata = {
  title: 'تایید ایمیل',
  robots: { index: false },
};

/**
 * The other end of the link `EmailLinks.VerifyEmail` puts in the verification
 * e-mail — see `ResetPasswordForm`'s page for the same `?token=` boundary.
 */
export default function EmailVerifyPage() {
  return (
    <Container className="flex min-h-[70vh] items-center justify-center py-lg md:py-xl">
      <Suspense
        fallback={<div className="h-[280px] w-full max-w-md rounded-xl bg-surface-container" />}
      >
        <EmailVerifyConfirm />
      </Suspense>
    </Container>
  );
}
