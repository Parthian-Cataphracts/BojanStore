import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Code, Icon } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ContactForm } from '@/components/content/ContactForm';
import { getStoreSettings } from '@/lib/api/store';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تماس با ما',
  description: 'راه‌های ارتباط با فروشگاه: تلفن، ایمیل، آدرس و فرم ارسال پیام.',
};

interface Channel {
  icon: string;
  title: string;
  value: string;
  href?: string;
  /** Latin technical value — rendered in Inter, isolated LTR. */
  latin?: boolean;
}

/**
 * Screen 18 — Contact us.
 *
 * Every detail here comes from the settings screen. It used to be four
 * hardcoded cards, which meant a shop that changed its number told nobody: the
 * owner edited the settings screen, saw it save, and this page went on showing
 * the number from the design mock-up.
 *
 * A shop that has filled in nothing gets the form and no cards, rather than
 * four cards of placeholder contact details a customer might actually try.
 */
export default async function ContactPage() {
  const { contact, identity } = await getStoreSettings();

  const channels: (Channel | null)[] = [
    contact.phone
      ? { icon: 'call', title: 'تلفن', value: contact.phone, href: `tel:${contact.phone}` }
      : null,
    contact.email
      ? {
          icon: 'mail',
          title: 'ایمیل',
          value: contact.email,
          href: `mailto:${contact.email}`,
          latin: true,
        }
      : null,
    contact.address ? { icon: 'place', title: 'آدرس', value: contact.address } : null,
    contact.postalCode
      ? { icon: 'markunread_mailbox', title: 'کد پستی', value: contact.postalCode }
      : null,
  ];

  const visible = channels.filter((channel): channel is Channel => channel !== null);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="تماس با ما"
        backHref={routes.home}
        subtitle={`برای پیگیری سفارش، سوال درباره محصولات یا همکاری با ${identity.name} با ما در ارتباط باشید.`}
      />

      <section className="grid gap-md md:grid-cols-2">
        {visible.map((channel) => {
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

      {contact.workingHours && (
        <p className="text-center text-caption text-outline">پاسخگویی: {contact.workingHours}</p>
      )}
    </Container>
  );
}
