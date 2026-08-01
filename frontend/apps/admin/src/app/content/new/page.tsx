import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';

export const metadata: Metadata = { title: 'افزودن محتوا' };

/**
 * Screen 121 — Content picker.
 *
 * "Content" covers four different entities with different fields, so this is a
 * chooser rather than one form pretending to fit all of them.
 */
const kinds = [
  {
    title: 'مقاله مجله',
    body: 'نوشته‌ای برای مجله بوژان با کاور، خلاصه و متن کامل.',
    icon: 'article',
    href: '/content/articles/new',
  },
  {
    title: 'بنر یا اسلایدر',
    body: 'تصویر تبلیغاتی برای صفحه اصلی یا بالای دسته‌بندی‌ها.',
    icon: 'image',
    href: '/content/banners/new',
  },
  {
    title: 'صفحه ثابت',
    body: 'صفحه‌ای مانند درباره ما، قوانین یا حریم خصوصی.',
    icon: 'description',
    href: '/content/pages/new',
  },
  {
    title: 'سوال متداول',
    body: 'یک پرسش و پاسخ در بخش سوالات متداول.',
    icon: 'help',
    href: '/content/faq/new',
  },
];

export default function NewContentPage() {
  return (
    <AdminPage
      title="افزودن محتوا"
      description="نوع محتوایی که می‌خواهید بسازید را انتخاب کنید."
      breadcrumbs={[{ label: 'محتوا', href: '/content' }, { label: 'افزودن محتوا' }]}
    >
      <div className="grid gap-md sm:grid-cols-2">
        {kinds.map((kind) => (
          <Link key={kind.href} href={kind.href} className="group block">
            <Card className="flex h-full items-start gap-md p-lg transition-shadow group-hover:shadow-soft">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                <Icon name={kind.icon} size={24} />
              </span>

              <div className="flex min-w-0 flex-1 flex-col gap-xs">
                <h3 className="text-card-title text-primary">{kind.title}</h3>
                <p className="text-body-md leading-relaxed text-on-surface-variant">{kind.body}</p>
              </div>

              <Icon name="chevron_left" size={20} className="mt-1 shrink-0 text-outline" />
            </Card>
          </Link>
        ))}
      </div>
    </AdminPage>
  );
}
