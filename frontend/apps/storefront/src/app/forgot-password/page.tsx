import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { ForgotPasswordForm } from '@/components/auth/ForgotPasswordForm';

export const metadata: Metadata = {
  title: 'بازیابی رمز عبور',
  // Nothing here should be indexed or followed into.
  robots: { index: false },
};

/** Asking for a reset link. */
export default function ForgotPasswordPage() {
  return (
    <Container className="flex min-h-[70vh] items-center justify-center py-lg md:py-xl">
      <ForgotPasswordForm />
    </Container>
  );
}
