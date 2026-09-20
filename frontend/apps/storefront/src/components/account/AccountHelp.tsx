import Link from 'next/link';
import { Card, Icon } from '@bojan/ui';
import { routes } from '@/lib/routes';

/**
 * In-account help. The shopper already has FAQ and buying guides elsewhere on
 * the site, but nothing that explains the account panel itself — this card does
 * that inline (no navigation needed, one open at a time via native <details>)
 * and points to the fuller help pages for anything beyond it.
 */

const topics: { q: string; a: string }[] = [
  {
    q: 'سفارش‌هایم را کجا ببینم و چطور پیگیری کنم؟',
    a: 'از «سفارش‌های من» همهٔ سفارش‌ها با وضعیت‌شان دیده می‌شود. روی هر سفارش بزنید تا جزئیات، رسید و مراحل ارسال را ببینید. برای رهگیری با کد رهگیری هم «پیگیری سفارش» را باز کنید.',
  },
  {
    q: 'چطور کالایی را مرجوع کنم؟',
    a: 'وارد سفارش موردنظر شوید و «درخواست مرجوعی» را بزنید، دلیل را انتخاب کنید و ثبت کنید. وضعیت درخواست در «مرجوعی‌های من» دنبال می‌شود.',
  },
  {
    q: 'کیف پول و امتیاز باشگاه مشتریان چطور کار می‌کند؟',
    a: 'موجودی کیف پول برای پرداخت سریع هنگام خرید استفاده می‌شود و از «کیف پول و اعتبار» قابل شارژ است. امتیاز باشگاه مشتریان با هر خرید جمع می‌شود و در تخفیف‌ها به کار می‌آید.',
  },
  {
    q: 'کد تخفیف را کجا وارد کنم؟',
    a: 'کدهای فعال شما در «کدهای تخفیف» فهرست شده‌اند. هنگام تسویه‌حساب، کد را در فیلد «کد تخفیف» وارد و اعمال کنید.',
  },
  {
    q: 'اطلاعات شخصی و آدرس‌هایم را چطور تغییر بدهم؟',
    a: 'نام، ایمیل و شماره از «اطلاعات شخصی» ویرایش می‌شود. آدرس‌های ارسال را هم در «آدرس‌های من» اضافه، ویرایش یا حذف کنید تا هنگام خرید سریع انتخاب شوند.',
  },
];

const quickLinks: { label: string; icon: string; href: string }[] = [
  { label: 'سوالات متداول', icon: 'quiz', href: routes.faq },
  { label: 'راهنمای خرید', icon: 'menu_book', href: routes.buyingGuide },
  { label: 'پیام به پشتیبانی', icon: 'support_agent', href: routes.support },
];

export function AccountHelp() {
  return (
    <section className="flex flex-col gap-md">
      <h2 className="flex items-center gap-xs font-headline text-display-md text-primary">
        <Icon name="help" size={22} />
        راهنما و پشتیبانی
      </h2>

      <Card className="flex flex-col divide-y divide-outline-variant p-0">
        {topics.map((topic) => (
          <details key={topic.q} className="group">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-md p-lg text-label-md font-label-md text-primary">
              {topic.q}
              <Icon
                name="expand_more"
                size={20}
                className="shrink-0 text-on-surface-variant transition-transform group-open:rotate-180"
              />
            </summary>
            <p className="px-lg pb-lg text-body-md leading-loose text-on-surface-variant">
              {topic.a}
            </p>
          </details>
        ))}
      </Card>

      <div className="grid grid-cols-3 gap-md">
        {quickLinks.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className="paper-card flex flex-col items-center gap-sm rounded-lg p-lg text-center transition-shadow hover:shadow-soft"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-full bg-soft-mint text-primary">
              <Icon name={link.icon} size={22} />
            </span>
            <span className="text-label-md font-label-md text-primary-container">{link.label}</span>
          </Link>
        ))}
      </div>
    </section>
  );
}
