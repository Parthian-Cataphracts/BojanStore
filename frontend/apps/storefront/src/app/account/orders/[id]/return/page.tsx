import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ReturnRequestForm } from '@/components/account/ReturnRequestForm';
import { getOrder } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'درخواست مرجوعی',
  robots: { index: false },
};

/** Screen 35 — Return request. */
export default async function ReturnRequestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const order = await getOrder(id);
  if (!order) notFound();

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader
        title="درخواست مرجوعی"
        backHref={`${routes.orders}/${order.id}`}
        subtitle={`سفارش #${order.number}`}
      />
      <ReturnRequestForm order={order} />
    </Container>
  );
}
