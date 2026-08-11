import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { LoyaltyForm } from '@/components/LoyaltyForm';
import { getLoyaltyProgramme } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'باشگاه مشتریان' };

/**
 * The loyalty club.
 *
 * There was no screen for this because there was no club: `/loyalty` on the
 * storefront advertised three tiers, a permanent discount and unlimited free
 * delivery, and nothing in the system applied any of it — the one call to
 * `AddLoyaltyPoints` in the whole codebase was in the seeder. Every figure on
 * this screen is now read by the checkout.
 */
export default async function Page() {
  const programme = await getLoyaltyProgramme();

  return (
    <AdminPage
      title="باشگاه مشتریان"
      description="پله‌های عضویت، تخفیف دائمی هر پله، و اینکه هر چند تومان خرید یک امتیاز بدهد."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'باشگاه مشتریان' },
      ]}
    >
      <LoyaltyForm programme={programme} />
    </AdminPage>
  );
}
