import type { ReactNode } from 'react';
import { cn } from '../lib/cn';
import { formatDate, formatPrice, toPersianDigits } from '../lib/format';
import { Code } from './Code';

export interface InvoiceDocumentLine {
  productId: string;
  title: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

/**
 * Structurally the backend's `InvoiceDto`.
 *
 * Declared here rather than imported from either app because this package sits
 * below both of them — and because the two apps render the *same* document, so
 * a shape that only one of them satisfies would be the bug this component
 * exists to prevent.
 */
export interface InvoiceDocumentData {
  invoiceNumber: string;
  orderNumber: string;
  placedAt: string;
  issuedAt: string;
  customerName: string;
  customerPhone: string;
  paymentMethod: string;
  shippingMethod: string;
  address: string;
  lines: InvoiceDocumentLine[];
  subtotal: number;
  couponCode?: string | null;
  discount: number;
  shipping: number;
  total: number;
  returnedCount: number;
  returnedRefund: number;
}

export interface InvoiceDocumentProps {
  invoice: InvoiceDocumentData;
  /** Print controls and anything else that must not reach paper. */
  actions?: ReactNode;
  className?: string;
}

function Row({ label, value }: { label: ReactNode; value: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-md border-b border-outline-variant/40 py-sm last:border-b-0">
      <dt className="text-caption text-on-surface-variant">{label}</dt>
      <dd className="text-body-md font-medium text-on-surface">{value}</dd>
    </div>
  );
}

function Party({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="border border-outline-variant">
      <h2 className="print-ink border-b border-outline-variant bg-surface-container-low px-md py-sm text-caption font-semibold text-on-surface-variant">
        {title}
      </h2>
      <dl className="px-md pb-sm pt-xs">{children}</dl>
    </section>
  );
}

/**
 * The customer invoice, laid out as a commercial document.
 *
 * Rendered identically by the storefront (screen 34) and the panel's invoice
 * section — same component, same data — so the copy the buyer holds and the
 * copy the shop keeps cannot drift apart.
 *
 * It bills what the buyer **kept**. Units returned and refunded are left off
 * the lines and reported once at the foot, because a buyer who was paid back
 * for something should not find it itemised on their bill. All the arithmetic
 * arrives from the server (`InvoiceBuilder`), so nothing here can disagree with
 * the money that actually moved.
 *
 * `print-sheet` is what makes this the only thing on the page when printed —
 * see the print rules in `@bojan/config`'s stylesheet.
 */
export function InvoiceDocument({ invoice, actions, className }: InvoiceDocumentProps) {
  return (
    <div className={cn('flex flex-col gap-md', className)}>
      {actions && <div className="print-hidden">{actions}</div>}

      <article
        dir="rtl"
        className="print-sheet mx-auto w-full max-w-[820px] border-t-4 border-primary bg-surface-container-lowest p-lg shadow-sm md:p-xl"
      >
        {/* Letterhead. In print it replaces the browser's own header, which the
            zero page margin removes. */}
        <header className="print-ink mb-lg flex flex-wrap items-center justify-between gap-md border border-outline-variant bg-surface-container-low px-md py-sm">
          <div className="flex flex-col">
            <span className="font-headline text-display-md text-primary">بوژان</span>
            <Code className="text-caption text-on-surface-variant">Bojan Store</Code>
          </div>
          <div className="flex flex-col text-start">
            <span className="text-caption text-on-surface-variant">تاریخ صدور</span>
            <span className="tabular text-body-md font-medium text-on-surface">
              {formatDate(invoice.issuedAt, 'long')}
            </span>
          </div>
        </header>

        <div className="flex flex-wrap items-baseline justify-between gap-md">
          <h1 className="font-headline text-display-md text-on-surface">فاکتور فروش</h1>
          <Code className="text-label-md font-semibold text-primary">{invoice.invoiceNumber}</Code>
        </div>
        <div className="mt-sm h-px bg-on-surface" />
        <p className="mt-sm text-caption text-on-surface-variant">
          بوژان · لوازم‌التحریر، دفتر و برنامه‌ریز، ابزار هنری و مهندسی
        </p>

        <dl className="mt-lg grid gap-x-xl sm:grid-cols-2">
          <Row label="شماره فاکتور" value={<Code>{invoice.invoiceNumber}</Code>} />
          <Row label="شماره سفارش" value={<Code>{invoice.orderNumber}</Code>} />
          <Row
            label="تاریخ سفارش"
            value={<span className="tabular">{formatDate(invoice.placedAt, 'long')}</span>}
          />
          <Row label="روش پرداخت" value={invoice.paymentMethod} />
          <Row label="روش ارسال" value={invoice.shippingMethod} />
          <Row label="وضعیت" value="تحویل‌شده" />
        </dl>

        <div className="mt-lg grid gap-md sm:grid-cols-2">
          <Party title="مشخصات فروشنده">
            <Row label="نام" value="فروشگاه بوژان" />
            <Row label="وب‌سایت" value={<Code>bojan.store</Code>} />
            <Row label="پشتیبانی" value={<Code>support@bojan.store</Code>} />
          </Party>
          <Party title="مشخصات خریدار">
            <Row label="نام" value={invoice.customerName} />
            <Row label="شماره تماس" value={<Code>{invoice.customerPhone}</Code>} />
            <Row label="نشانی" value={<span className="text-body-sm">{invoice.address}</span>} />
          </Party>
        </div>

        <div className="mt-lg overflow-x-auto">
          <table className="w-full min-w-[480px] border-collapse text-body-md">
            <thead>
              <tr className="print-ink bg-on-surface text-surface-container-lowest">
                <th scope="col" className="w-12 px-sm py-sm text-center text-caption font-semibold">
                  ردیف
                </th>
                <th scope="col" className="px-md py-sm text-start text-caption font-semibold">
                  شرح کالا
                </th>
                <th scope="col" className="w-20 px-sm py-sm text-center text-caption font-semibold">
                  تعداد
                </th>
                <th scope="col" className="w-32 px-md py-sm text-start text-caption font-semibold">
                  مبلغ واحد
                </th>
                <th scope="col" className="w-36 px-md py-sm text-start text-caption font-semibold">
                  مبلغ کل
                </th>
              </tr>
            </thead>
            <tbody>
              {invoice.lines.map((line, index) => (
                <tr key={line.productId} className="border-b border-outline-variant/40">
                  <td className="tabular px-sm py-md text-center text-on-surface-variant">
                    {toPersianDigits(index + 1)}
                  </td>
                  <td className="px-md py-md text-on-surface">{line.title}</td>
                  <td className="tabular px-sm py-md text-center text-on-surface-variant">
                    {toPersianDigits(line.quantity)}
                  </td>
                  <td className="tabular px-md py-md text-on-surface-variant">
                    {formatPrice(line.unitPrice)}
                  </td>
                  <td className="tabular px-md py-md font-medium text-on-surface">
                    {formatPrice(line.lineTotal)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Returned units are never itemised — the buyer was refunded, so they
            are not part of this bill. Stated once so the document explains why
            it does not match the original order. */}
        {invoice.returnedCount > 0 && (
          <p className="print-ink mt-md border border-outline-variant bg-surface-container-low px-md py-sm text-body-sm leading-loose text-on-surface-variant">
            این فاکتور فقط شامل اقلام نگه‌داشته‌شده است. {toPersianDigits(invoice.returnedCount)} قلم
            از این سفارش مرجوع شد
            {invoice.returnedRefund > 0 && <> و مبلغ {formatPrice(invoice.returnedRefund)} بازگردانده شد</>}.
          </p>
        )}

        <div className="print-keep mt-lg flex flex-col gap-lg md:flex-row md:items-stretch md:justify-between">
          <div className="w-full md:max-w-[340px]">
            <dl>
              <Row
                label="جمع اقلام"
                value={<span className="tabular">{formatPrice(invoice.subtotal)}</span>}
              />
              {invoice.discount > 0 && (
                <Row
                  label={
                    <>
                      تخفیف
                      {invoice.couponCode && <> (<Code>{invoice.couponCode}</Code>)</>}
                    </>
                  }
                  value={<span className="tabular text-secondary">−{formatPrice(invoice.discount)}</span>}
                />
              )}
              <Row
                label="هزینه ارسال"
                value={<span className="tabular">{formatPrice(invoice.shipping)}</span>}
              />
            </dl>
            <div className="print-ink mt-sm flex items-baseline justify-between gap-md bg-on-surface px-md py-md text-surface-container-lowest">
              <span className="text-body-md font-medium">مبلغ قابل پرداخت</span>
              <span className="tabular text-body-lg font-semibold">{formatPrice(invoice.total)}</span>
            </div>
          </div>

          <div className="flex flex-1 flex-col justify-between gap-md md:max-w-[46ch]">
            <div>
              <p className="text-body-lg font-medium text-on-surface">
                از اعتماد و خرید شما سپاسگزاریم.
              </p>
              <p className="mt-sm text-body-sm leading-loose text-on-surface-variant">
                خریدار با ثبت این سفارش، قوانین و مقررات فروشگاه بوژان را مطالعه کرده و پذیرفته است.
                مهلت مرجوعی کالا و شرایط گارانتی، مطابق شرایط اعلام‌شده در زمان خرید است.
              </p>
            </div>
          </div>
        </div>

        <footer className="mt-lg flex flex-wrap items-center justify-between gap-sm border-t border-outline-variant/40 pt-sm text-caption text-on-surface-variant">
          <span>این فاکتور به‌صورت الکترونیکی صادر شده و بدون مهر و امضای فیزیکی معتبر است.</span>
          <Code>bojan.store</Code>
        </footer>
      </article>
    </div>
  );
}
