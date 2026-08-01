import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { b2bProcessSteps } from '@/lib/mock/business';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'راهنمای خرید سازمانی',
  description: 'فرآیند خرید و ثبت سفارش برای سازمان‌ها و شرکت‌ها در بوژان.',
};

const faqs = [
  {
    question: 'حداقل مبلغ سفارش سازمانی چقدر است؟',
    answer: 'برای خرید سازمانی حداقل مبلغ سفارش ۲۰ میلیون تومان است. برای بسته‌های هدیه، حداقل تعداد در هر بسته مشخص شده است.',
  },
  {
    question: 'امکان تسویه اعتباری وجود دارد؟',
    answer: 'بله. برای سازمان‌هایی که پروفایل سازمانی تکمیل و تایید شده دارند، تسویه اعتباری تا ۳۰ روز امکان‌پذیر است.',
  },
  {
    question: 'حکاکی لوگو روی محصولات ممکن است؟',
    answer: 'روی بیشتر محصولات چوبی، فلزی و چرمی امکان حکاکی لیزری وجود دارد. زمان تولید حدود ۷ تا ۱۰ روز کاری اضافه می‌شود.',
  },
];

/** Screen 67 — Business buying guide. */
export default function BusinessGuidePage() {
  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <PageHeader
        title="راهنمای خرید سازمانی"
        backHref={routes.business}
        subtitle="فرآیند خرید و ثبت سفارش برای سازمان‌ها و شرکت‌ها"
      />

      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-section-title text-primary">فرآیند ثبت سفارش</h2>

        <ol className="grid gap-gutter sm:grid-cols-2 lg:grid-cols-3">
          {b2bProcessSteps.map((step, index) => (
            <li key={step.title}>
              <Card className="flex h-full flex-col gap-sm p-lg">
                <div className="flex items-center gap-sm">
                  <span className="tabular flex h-9 w-9 items-center justify-center rounded-full bg-primary-fixed-dim/25 text-label-md font-semibold text-primary-container">
                    {toPersianDigits(index + 1)}
                  </span>
                  <Icon name={step.icon} size={22} className="text-primary" />
                </div>
                <h3 className="text-card-title text-primary">{step.title}</h3>
                <p className="text-body-md leading-relaxed text-on-surface-variant">{step.body}</p>
              </Card>
            </li>
          ))}
        </ol>
      </section>

      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-section-title text-primary">پرسش‌های پرتکرار</h2>

        {faqs.map((faq) => (
          <Card key={faq.question} className="flex flex-col gap-sm p-lg">
            <h3 className="flex items-start gap-sm text-label-md font-semibold text-primary">
              <Icon name="help" size={20} className="mt-px shrink-0" />
              {faq.question}
            </h3>
            <p className="ps-7 text-body-md leading-loose text-on-surface-variant">{faq.answer}</p>
          </Card>
        ))}
      </section>

      <div className="flex flex-col gap-md sm:flex-row">
        <Link
          href={routes.businessQuote}
          className={buttonClasses({ size: 'lg', fullWidth: true })}
        >
          درخواست پیش‌فاکتور
        </Link>
        <Link
          href={routes.businessConsultant}
          className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
        >
          تماس با مشاور فروش
        </Link>
      </div>
    </Container>
  );
}
