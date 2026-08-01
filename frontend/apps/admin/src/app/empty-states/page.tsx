import type { Metadata } from 'next';
import { Card, EmptyState } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';

export const metadata: Metadata = { title: 'حالت‌های خالی پنل ادمین' };

/** The catalogue of empty states the panel uses, so they stay consistent. */
const states = [
  {
    context: 'فهرست سفارش‌ها',
    icon: 'receipt_long',
    title: 'سفارشی ثبت نشده',
    description: 'با ثبت اولین سفارش، این فهرست پر می‌شود.',
  },
  {
    context: 'فهرست محصولات',
    icon: 'inventory_2',
    title: 'محصولی اضافه نشده',
    description: 'اولین محصول خود را اضافه کنید تا فروشگاه راه بیفتد.',
  },
  {
    context: 'نتیجه فیلتر',
    icon: 'filter_alt_off',
    title: 'موردی با این فیلترها نیست',
    description: 'فیلترها را تغییر دهید یا همه را پاک کنید.',
  },
  {
    context: 'جستجو',
    icon: 'search_off',
    title: 'نتیجه‌ای پیدا نشد',
    description: 'املای عبارت را بررسی کنید یا عبارت کوتاه‌تری امتحان کنید.',
  },
  {
    context: 'پشتیبانی',
    icon: 'forum',
    title: 'گفتگوی بازی نیست',
    description: 'همه تیکت‌ها پاسخ داده شده‌اند.',
  },
  {
    context: 'خطای بارگذاری',
    icon: 'cloud_off',
    title: 'داده‌ها بارگذاری نشد',
    description: 'اتصال را بررسی کنید و دوباره تلاش کنید.',
  },
];

/** Screen 159 — Empty-state patterns. */
export default function EmptyStatesPage() {
  return (
    <AdminPage
      title="حالت‌های خالی پنل ادمین"
      description="این صفحه مرجع است، نه یک بخش عملیاتی: الگوی حالت خالی هر بخش را یک‌جا نشان می‌دهد تا پیام‌ها در کل پنل یکدست بمانند."
      breadcrumbs={[{ label: 'تنظیمات', href: '/settings' }, { label: 'حالت‌های خالی' }]}
    >
      <div className="grid gap-md md:grid-cols-2">
        {states.map((state) => (
          <Card key={state.context} surface="plain" className="flex flex-col">
            <span className="border-b border-outline-variant/40 px-lg py-sm text-caption text-on-surface-variant">
              {state.context}
            </span>
            <EmptyState
              icon={state.icon}
              title={state.title}
              description={state.description}
              className="flex-1"
            />
          </Card>
        ))}
      </div>
    </AdminPage>
  );
}
