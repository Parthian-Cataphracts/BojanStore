import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { getAdminUsers } from '@/lib/api/settings';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'نقش‌ها و دسترسی‌ها' };

/** What each role may ever reach, before any narrowing. */
const roles = [
  {
    id: 'owner',
    label: 'مالک',
    reach: 'همه‌چیز، شامل تنظیمات، پشتیبان‌گیری، کلیدهای API و همین صفحه',
  },
  { id: 'product', label: 'مدیر محصول', reach: 'کاتالوگ، موجودی، محتوا و کمپین' },
  {
    id: 'sales',
    label: 'فروش سازمانی',
    reach: 'درخواست‌های سازمانی، پیش‌فاکتور، کوپن، اعلان انبوه و سفارش',
  },
  { id: 'support', label: 'پشتیبانی', reach: 'تیکت، گفتگو، صندوق پستی، وضعیت سفارش و مرجوعی' },
] as const;

const SECTIONS: Record<string, string> = {
  orders: 'سفارش‌ها',
  products: 'محصولات',
  inventory: 'موجودی',
  customers: 'مشتریان',
  business: 'درخواست‌های سازمانی',
  content: 'محتوا',
  campaigns: 'کمپین‌ها',
  support: 'پشتیبانی',
  reports: 'گزارش‌ها',
  settings: 'تنظیمات',
};

const roleLabel = (role: string) => roles.find((entry) => entry.id === role)?.label ?? role;

/**
 * Screen 146 — who may open what.
 *
 * It was a grid of role against section, and it is now a list of people. The
 * grid could not express the thing an owner actually wants: granting one
 * salesperson the returns queue granted it to every salesperson, because the
 * permission belonged to the job title rather than to the person doing the job.
 *
 * So permissions moved onto the operator, and this screen moved with them. It
 * reads rather than writes — the checklist that sets these lives beside the
 * operator it belongs to, on «کاربران ادمین», which is where somebody is
 * already standing when they think about what that person should see.
 */
export default async function RolesPage() {
  await requireRole('owner');

  const { items: operators } = await getAdminUsers({ pageSize: 100 });

  return (
    <AdminPage
      title="نقش‌ها و دسترسی‌ها"
      description="نقش تعیین می‌کند یک اپراتور حداکثر به چه چیزی می‌تواند برسد، و دسترسی‌های هر نفر آن را باریک‌تر می‌کند."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'سیستم و دسترسی' },
        { label: 'نقش‌ها و دسترسی‌ها' },
      ]}
      actions={
        <Link
          href="/settings/users"
          className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
        >
          <Icon name="manage_accounts" size={18} />
          مدیریت اپراتورها
        </Link>
      }
    >
      <Card className="gap-md p-lg flex flex-col">
        <h3 className="font-headline text-card-title text-primary">چهار نقش</h3>
        <dl className="gap-sm flex flex-col">
          {roles.map((role) => (
            <div
              key={role.id}
              className="gap-xs border-outline-variant/30 pb-sm sm:gap-md flex flex-col border-b last:border-0 last:pb-0 sm:flex-row sm:items-baseline"
            >
              <dt className="w-32 shrink-0">
                <Badge tone={role.id === 'owner' ? 'teal' : 'neutral'}>{role.label}</Badge>
              </dt>
              <dd className="text-caption text-on-surface-variant leading-relaxed">{role.reach}</dd>
            </div>
          ))}
        </dl>
      </Card>

      <Card className="gap-md p-lg flex flex-col">
        <div className="gap-xs flex flex-col">
          <h3 className="font-headline text-card-title text-primary">دسترسی هر اپراتور</h3>
          <p className="text-caption text-on-surface-variant leading-relaxed">
            بخش‌های تیک‌خورده، دسترسی آن نفر را به همان‌ها محدود می‌کنند. اپراتوری که هیچ بخشی برایش
            انتخاب نشده محدود نشده است و به هرچه نقشش اجازه می‌دهد دسترسی دارد. مالک هرگز محدود
            نمی‌شود.
          </p>
        </div>

        {operators.length === 0 ? (
          <p className="text-body-md text-on-surface-variant">هنوز اپراتوری تعیین نشده است.</p>
        ) : (
          <ul className="gap-sm flex flex-col">
            {operators.map((operator) => {
              const granted = operator.sections ?? [];

              return (
                <li
                  key={operator.id}
                  className="gap-sm border-outline-variant/30 pb-md flex flex-col border-b last:border-0 last:pb-0"
                >
                  <div className="gap-sm flex flex-wrap items-center">
                    <span className="text-body-md text-on-surface font-medium">
                      {operator.name}
                    </span>
                    <Badge tone={operator.role === 'owner' ? 'teal' : 'neutral'}>
                      {roleLabel(operator.role)}
                    </Badge>
                    {operator.status !== 'active' && <Badge tone="warning">معلق</Badge>}
                  </div>

                  {operator.role === 'owner' ? (
                    <span className="text-caption text-on-surface-variant">
                      دسترسی کامل — نقش مالک محدودشدنی نیست.
                    </span>
                  ) : granted.length === 0 ? (
                    <span className="text-caption text-on-surface-variant">
                      محدود نشده — به تمام چیزی که نقشش اجازه می‌دهد دسترسی دارد.
                    </span>
                  ) : (
                    <div className="gap-xs flex flex-wrap">
                      {granted.map((section) => (
                        <Badge key={section} tone="mint">
                          {SECTIONS[section] ?? section}
                        </Badge>
                      ))}
                      <span className="text-caption text-on-surface-variant">
                        ({toPersianDigits(granted.length)} بخش)
                      </span>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </AdminPage>
  );
}
