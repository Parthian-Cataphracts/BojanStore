import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { AddressForm } from '@/components/account/AddressForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'افزودن آدرس جدید',
  robots: { index: false },
};

/** Screen 15 — Add address. */
export default function NewAddressPage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="افزودن آدرس جدید" backHref={routes.addresses} />
      <AddressForm />
    </Container>
  );
}
