import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { AddressForm } from '@/components/account/AddressForm';
import { getAddress } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'ویرایش آدرس',
  robots: { index: false },
};

/** Screen 15 — Edit address. */
export default async function EditAddressPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const address = await getAddress(id);
  if (!address) notFound();

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="ویرایش آدرس" backHref={routes.addresses} subtitle={address.title} />
      <AddressForm address={address} />
    </Container>
  );
}
