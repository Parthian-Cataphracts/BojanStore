import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Container } from '@/components/layout/Container';
import { ResetPasswordForm } from '@/components/auth/ResetPasswordForm';

export const metadata: Metadata = {
  title: 'رمز عبور تازه',
  robots: { index: false },
};

/**
 * The other end of the emailed link. The form reads `?token=`, so it needs a
 * boundary for the page to stay static.
 */
export default function ResetPasswordPage() {
  return (
    <Container className="flex min-h-[70vh] items-center justify-center py-xl">
      <Suspense
        fallback={<div className="h-[420px] w-full max-w-md rounded-xl bg-surface-container" />}
      >
        <ResetPasswordForm />
      </Suspense>
    </Container>
  );
}
