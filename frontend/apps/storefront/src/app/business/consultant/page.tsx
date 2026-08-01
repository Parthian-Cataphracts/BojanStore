import type { Metadata } from 'next';
import { Card, Code, Icon, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ContactForm } from '@/components/content/ContactForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تماس با مشاور فروش سازمانی',
  description: 'مشاوران فروش سازمانی بوژان آماده‌اند نیاز سازمان شما را بررسی کنند.',
};

const channels: { icon: string; title: string; value: string; href?: string; latin?: boolean }[] = [
  {
    icon: 'call',
    title: 'خط مستقیم فروش سازمانی',
    value: '۰۲۱-۱۲۳۴۵۶۷۹',
    href: 'tel:+982112345679',
  },
  {
    icon: 'mail',
    title: 'ایمیل واحد سازمانی',
    value: 'b2b@bojan.com',
    href: 'mailto:b2b@bojan.com',
    latin: true,
  },
  { icon: 'schedule', title: 'ساعات پاسخگویی', value: 'شنبه تا چهارشنبه، ۹ تا ۱۷' },
];

/** Screen 68 — Contact the B2B sales consultant. */
export default function ConsultantPage() {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="تماس با مشاور فروش سازمانی"
        backHref={routes.business}
        subtitle="مشاوران ما آماده‌اند بهترین راهکار تأمین را متناسب با نیاز و بودجه سازمان شما ارائه دهند."
      />

      <section className="grid gap-md md:grid-cols-3">
        {channels.map((channel) => {
          const inner = (
            <Card className="flex h-full flex-col gap-sm p-lg transition-shadow hover:shadow-soft">
              <span className="flex h-12 w-12 items-center justify-center rounded-full bg-soft-mint text-primary">
                <Icon name={channel.icon} size={24} />
              </span>
              <h2 className="text-label-md font-semibold text-primary">{channel.title}</h2>
              {channel.latin ? (
                <Code className="text-body-md text-on-surface-variant">{channel.value}</Code>
              ) : (
                <p className="tabular text-body-md text-on-surface-variant">{channel.value}</p>
              )}
            </Card>
          );

          return channel.href ? (
            <a key={channel.title} href={channel.href} className="block">
              {inner}
            </a>
          ) : (
            <div key={channel.title}>{inner}</div>
          );
        })}
      </section>

      <ContactForm />

      <p className="flex items-center justify-center gap-xs text-caption text-outline">
        <Icon name="info" size={16} />
        میانگین زمان پاسخگویی: کمتر از {toPersianDigits(24)} ساعت کاری
      </p>
    </Container>
  );
}
