import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { CompleteProfileForm } from '@/components/auth/CompleteProfileForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تکمیل پروفایل',
  robots: { index: false },
};

/** Screen 52 — Complete profile, shown straight after first sign-in. */
export default function CompleteProfilePage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="تکمیل پروفایل" backHref={routes.login} />
      <CompleteProfileForm />
    </Container>
  );
}
