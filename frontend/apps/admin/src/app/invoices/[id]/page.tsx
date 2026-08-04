import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Icon, InvoiceDocument, buttonClasses } from '@bojan/ui';
import { PrintButton } from '@/components/PrintButton';
import { getInvoice } from '@/lib/api/invoices';

export const metadata: Metadata = { title: 'فاکتور' };

/**
 * One invoice, as it prints.
 *
 * No `AdminPage` shell around the document: this screen exists to be printed,
 * and the print rules would strip a breadcrumb bar and page header anyway. The
 * two controls above the sheet carry `print-hidden` through
 * `InvoiceDocument`'s `actions` slot.
 */
export default async function AdminInvoicePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const invoice = await getInvoice(id);

  // 404 covers "no such order" and "delivered? not yet" alike — the API does
  // not distinguish them, and neither has an invoice to show.
  if (!invoice) notFound();

  return (
    <div className="p-md md:p-lg">
      <InvoiceDocument
        invoice={invoice}
        actions={
          <div className="mx-auto flex w-full max-w-[820px] items-center justify-between gap-sm">
            <Link
              href="/invoices"
              className={buttonClasses({ variant: 'ghost', size: 'sm', className: 'gap-xs' })}
            >
              <Icon name="chevron_right" size={18} />
              بازگشت به فاکتورها
            </Link>
            <div className="flex items-center gap-sm">
              <Link
                href={`/orders/${invoice.orderId}`}
                className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
              >
                <Icon name="shopping_cart" size={18} />
                سفارش
              </Link>
              <PrintButton />
            </div>
          </div>
        }
      />
    </div>
  );
}
