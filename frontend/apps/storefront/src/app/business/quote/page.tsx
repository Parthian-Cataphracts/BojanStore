import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { QuoteRequestForm } from '@/components/business/QuoteRequestForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'درخواست پیش‌فاکتور',
  description: 'برای خریدهای سازمانی، هدایای تبلیغاتی و سفارش‌های عمده درخواست پیش‌فاکتور ثبت کنید.',
};

/** Screen 61 — Quote request. */
export default function BusinessQuotePage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader
        title="درخواست پیش‌فاکتور"
        backHref={routes.business}
        subtitle="فرم زیر را تکمیل کنید؛ کارشناسان ما در اسرع وقت با شما تماس خواهند گرفت."
      />
      <QuoteRequestForm />
    </Container>
  );
}
