import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { OrganizationForm } from '@/components/business/OrganizationForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'ثبت اطلاعات سازمان',
  robots: { index: false },
};

/** Screen 63 — Register organisation details. */
export default function OrganizationPage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader
        title="ثبت اطلاعات سازمان"
        backHref={routes.business}
        subtitle="اطلاعات سازمان یا شرکت خود را برای تکمیل پروفایل و صدور فاکتور رسمی وارد کنید."
      />
      <OrganizationForm />
    </Container>
  );
}
