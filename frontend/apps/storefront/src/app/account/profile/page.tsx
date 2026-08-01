import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ProfileForm } from '@/components/account/ProfileForm';
import { getCurrentUser } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'اطلاعات شخصی',
  robots: { index: false },
};

/** Screen 16 — Personal information. */
export default async function ProfilePage() {
  const user = await getCurrentUser();

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="اطلاعات شخصی" backHref={routes.account} />
      <ProfileForm user={user} />
    </Container>
  );
}
