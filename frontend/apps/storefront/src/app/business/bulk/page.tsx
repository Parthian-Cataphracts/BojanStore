import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { BulkOrderForm } from '@/components/business/BulkOrderForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'فرم خرید عمده',
  description: 'اقلام و تعداد مورد نیاز خود را برای خرید عمده از بوژان ثبت کنید.',
};

/** Screen 62 — Bulk order form. */
export default function BulkOrderPage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader
        title="فرم خرید عمده"
        backHref={routes.business}
        subtitle="لطفاً اطلاعات محصولات درخواستی و اطلاعات تماس خود را با دقت وارد کنید."
      />
      <BulkOrderForm />
    </Container>
  );
}
