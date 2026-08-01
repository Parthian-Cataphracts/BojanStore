import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses, serializeJsonLd } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { FaqList } from '@/components/content/FaqList';
import { faqs } from '@/lib/content/pages';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'سوالات متداول',
  description: 'پاسخ پرتکرارترین سوال‌ها درباره سفارش، ارسال، پرداخت و مرجوعی در فروشگاه بوژان.',
};

/** Screen 19 — FAQ. */
export default function FaqPage() {
  // Structured data so the answers can surface directly in search results.
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    mainEntity: faqs.map((faq) => ({
      '@type': 'Question',
      name: faq.question,
      acceptedAnswer: { '@type': 'Answer', text: faq.answer },
    })),
  };

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: serializeJsonLd(jsonLd) }}
      />

      <PageHeader title="سوالات متداول" backHref={routes.home} />

      <FaqList />

      <Card className="flex flex-col items-center gap-md p-xl text-center">
        <span className="flex h-14 w-14 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="headset_mic" size={26} />
        </span>
        <h2 className="font-headline text-display-md text-primary">پاسخ خود را پیدا نکردید؟</h2>
        <p className="max-w-md text-body-md leading-loose text-on-surface-variant">
          تیم پشتیبانی ما آماده پاسخگویی به سوالات شماست.
        </p>
        <Link href={routes.contact} className={buttonClasses({ className: 'mt-sm' })}>
          ارتباط با پشتیبانی
        </Link>
      </Card>
    </Container>
  );
}
