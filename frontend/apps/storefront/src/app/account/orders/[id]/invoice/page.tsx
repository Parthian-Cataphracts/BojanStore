import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Icon, InvoiceDocument } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { InvoiceActions } from '@/components/account/InvoiceActions';
import { getInvoice } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'فاکتور سفارش',
  robots: { index: false },
};

/**
 * Screen 34 — Order invoice.
 *
 * The document is `InvoiceDocument` from `@bojan/ui`, the same component the
 * panel renders, so the copy the buyer downloads and the copy an operator
 * opens are one thing. Everything on it is computed server-side by
 * `InvoiceBuilder`: the number issued at delivery, the lines net of anything
 * returned and refunded, and totals footed against the money that moved.
 *
 * 404 before delivery. There is no invoice to show until then — the screen
 * used to render one for any order at all, numbered `INV-{order number}` and
 * billing everything ordered whether or not it came back.
 */
export default async function InvoicePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const invoice = await getInvoice(id);
  if (!invoice) notFound();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <div className="print-hidden">
        <PageHeader title="فاکتور سفارش" backHref={`${routes.orders}/${invoice.orderId}`} />
      </div>

      <InvoiceDocument
        invoice={invoice}
        actions={<InvoiceActions orderNumber={invoice.orderNumber} />}
      />

      <p className="print-hidden flex items-center justify-center gap-xs text-caption text-outline">
        <Icon name="info" size={16} />
        این فاکتور به‌صورت الکترونیکی صادر شده و نیازی به مهر و امضا ندارد.
      </p>
    </Container>
  );
}
