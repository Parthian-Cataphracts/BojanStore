import type { Metadata } from 'next';
import { Card, Code, Icon } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ContactForm } from '@/components/content/ContactForm';
import { getStoreSettings } from '@/lib/api/store';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تماس با مشاور فروش سازمانی',
  description: 'مشاوران فروش سازمانی آماده‌اند نیاز سازمان شما را بررسی کنند.',
};

interface Channel {
  icon: string;
  title: string;
  value: string;
  href?: string;
  latin?: boolean;
}

/**
 * Screen 68 — Contact the B2B sales consultant.
 *
 * The organisational line and address come from the settings screen, falling
 * back to the shop's main ones — most shops answer B2B enquiries on the same
 * number, and a card showing a direct line that nobody answers is worse than a
 * card showing the one that is.
 */
export default async function ConsultantPage() {
  const { contact, promises } = await getStoreSettings();
  const supportPromise = promises.supportPromise;

  const phone = contact.businessPhone || contact.phone;
  const email = contact.businessEmail || contact.email;

  const channels: (Channel | null)[] = [
    phone
      ? { icon: 'call', title: 'خط فروش سازمانی', value: phone, href: `tel:${phone}` }
      : null,
    email
      ? {
          icon: 'mail',
          title: 'ایمیل واحد سازمانی',
          value: email,
          href: `mailto:${email}`,
          latin: true,
        }
      : null,
    contact.workingHours
      ? { icon: 'schedule', title: 'ساعات پاسخگویی', value: contact.workingHours }
      : null,
  ];

  const visible = channels.filter((channel): channel is Channel => channel !== null);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="تماس با مشاور فروش سازمانی"
        backHref={routes.business}
        subtitle="مشاوران ما آماده‌اند بهترین راهکار تأمین را متناسب با نیاز و بودجه سازمان شما ارائه دهند."
      />

      <section className="grid gap-md md:grid-cols-3">
        {visible.map((channel) => {
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

      {/* The shop's own promise rather than a figure written here — a page
          guaranteeing a reply within a day is a page the shop has to honour. */}
      {supportPromise && (
        <p className="flex items-center justify-center gap-xs text-caption text-outline">
          <Icon name="info" size={16} />
          {supportPromise}
        </p>
      )}
    </Container>
  );
}
