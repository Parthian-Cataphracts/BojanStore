import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { CompleteProfileForm } from '@/components/auth/CompleteProfileForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تکمیل پروفایل',
  robots: { index: false },
};

/**
 * Screen 52 — Complete profile, shown straight after first sign-in.
 *
 * The form reads `?next=` to know where to send the customer on afterwards, so
 * it needs a boundary for the page to stay static — the same arrangement the
 * sign-in screen makes for the same reason.
 */
export default function CompleteProfilePage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="تکمیل پروفایل" backHref={routes.login} />
      <Suspense
        fallback={<div className="h-[560px] w-full rounded-xl bg-surface-container" />}
      >
        <CompleteProfileForm />
      </Suspense>
    </Container>
  );
}
