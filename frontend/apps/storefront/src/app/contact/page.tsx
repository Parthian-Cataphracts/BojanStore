import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Code, Icon, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ContactForm } from '@/components/content/ContactForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تماس با ما',
  description: 'راه‌های ارتباط با فروشگاه بوژان: تلفن، ایمیل، آدرس و فرم ارسال پیام.',
};

const channels: {
  icon: string;
  title: string;
  value: string;
  href?: string;
  /** Latin technical value — rendered in Inter, isolated LTR. */
  latin?: boolean;
}[] = [
  { icon: 'call', title: 'تلفن ثابت', value: '۰۲۱-۱۲۳۴۵۶۷۸', href: 'tel:+982112345678' },
  { icon: 'call', title: 'موبایل', value: '۰۹۱۲-۳۴۵-۶۷۸۹', href: 'tel:+989123456789' },
  {
    icon: 'mail',
    title: 'ایمیل',
    value: 'info@bojan.com',
    href: 'mailto:info@bojan.com',
    latin: true,
  },
  {
    icon: 'place',
    title: 'آدرس',
    value: 'تهران، خیابان ولیعصر، نرسیده به پارک‌وی، کوچه فرزان، پلاک ۱۲',
  },
];

/** Screen 18 — Contact us. */
export default function ContactPage() {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="تماس با ما"
        backHref={routes.home}
        subtitle="برای پیگیری سفارش، سوال درباره محصولات یا همکاری با بوژان با ما در ارتباط باشید."
      />

      <section className="grid gap-md md:grid-cols-2">
        {channels.map((channel) => {
          const inner = (
            <Card className="flex h-full items-start gap-md p-lg transition-shadow hover:shadow-soft">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                <Icon name={channel.icon} size={24} />
              </span>
              <div className="flex min-w-0 flex-col gap-xs">
                <h2 className="text-label-md font-label-md text-primary">{channel.title}</h2>
                {/* Phone numbers and e-mail are Latin technical values. */}
                {channel.latin ? (
                  <Code className="text-body-md text-on-surface-variant">{channel.value}</Code>
                ) : (
                  <p className="text-body-md leading-relaxed text-on-surface-variant">
                    {channel.value}
                  </p>
                )}
              </div>
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

      <Link
        href={routes.faq}
        className="paper-card flex items-center justify-center gap-sm rounded-lg p-lg text-label-md font-label-md text-primary transition-shadow hover:shadow-soft"
      >
        <Icon name="help" size={20} />
        پاسخ بسیاری از سوال‌ها در بخش سوالات متداول هست
      </Link>

      <p className="text-center text-caption text-outline">
        پاسخگویی: شنبه تا چهارشنبه، {toPersianDigits(9)} تا {toPersianDigits(18)}
      </p>
    </Container>
  );
}
