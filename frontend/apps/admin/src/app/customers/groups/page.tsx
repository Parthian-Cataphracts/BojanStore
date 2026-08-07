import type { Metadata } from 'next';
import { Badge, Card, Icon, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { getCustomers } from '@/lib/api/customers';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'گروه‌بندی مشتریان' };

/** Rules that define each group, alongside the members they currently match. */
const groupRules = [
  {
    name: 'عادی',
    icon: 'person',
    rule: 'همه مشتریان حقیقی بدون شرط خاص',
    discount: '—',
  },
  {
    name: 'وفادار',
    icon: 'workspace_premium',
    rule: 'بیش از ۵ سفارش یا مجموع خرید بالای ۱۰ میلیون تومان',
    discount: '۵٪',
  },
  {
    name: 'سازمانی',
    icon: 'business_center',
    rule: 'دارای پروفایل سازمانی تاییدشده',
    discount: 'قیمت‌گذاری پلکانی',
  },
];

/** Screen 118 — Customer groups. */
export default async function CustomerGroupsPage() {
  await requireRole('owner', 'sales', 'support');
  const { items: customers } = await getCustomers({ pageSize: 500 });

  // Counts come from the customer list so a group can never claim a number the
  // members table cannot show.
  const counts = groupRules.map((group) => ({
    ...group,
    members: customers.filter((customer) => customer.group === group.name).length,
    revenue: customers
      .filter((customer) => customer.group === group.name)
      .reduce((sum, customer) => sum + customer.totalSpent, 0),
  }));

  return (
    <AdminPage
      title="گروه‌بندی مشتریان"
      description="گروه‌ها تعیین می‌کنند چه تخفیف و شرایطی به هر دسته از مشتریان تعلق بگیرد."
      breadcrumbs={[{ label: 'مشتریان', href: '/customers' }, { label: 'گروه‌بندی' }]}
    >
      <section className="grid gap-md sm:grid-cols-3">
        {counts.map((group) => (
          <Card key={group.name} className="flex flex-col gap-sm p-lg">
            <span className="flex items-center justify-between">
              <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                <Icon name={group.icon} size={22} />
              </span>
              <Badge tone="neutral">{group.discount}</Badge>
            </span>

            <h3 className="text-card-title text-primary">{group.name}</h3>
            <p className="text-caption leading-relaxed text-on-surface-variant">{group.rule}</p>

            <dl className="mt-md flex items-center justify-between border-t border-paper-border pt-md">
              <dt className="text-caption text-on-surface-variant">اعضا</dt>
              <dd className="tabular text-body-md font-semibold text-primary">
                {toPersianDigits(group.members)}
              </dd>
            </dl>
            <dl className="flex items-center justify-between">
              <dt className="text-caption text-on-surface-variant">مجموع خرید</dt>
              <dd className="tabular text-body-md text-on-surface">{formatPrice(group.revenue)}</dd>
            </dl>
          </Card>
        ))}
      </section>

      <section className="flex flex-col gap-md">
        <h3 className="font-headline text-card-title text-primary">اعضای گروه‌ها</h3>

        <DataTable
          rows={customers}
          rowKey={(row) => row.id}
          columns={[
            { key: 'name', header: 'مشتری', cell: (row) => row.name },
            {
              key: 'group',
              header: 'گروه',
              cell: (row) => (
                <Badge tone={row.group === 'سازمانی' ? 'teal' : row.group === 'وفادار' ? 'mint' : 'neutral'}>
                  {row.group}
                </Badge>
              ),
            },
            {
              key: 'orders',
              header: 'سفارش‌ها',
              cell: (row) => <span className="tabular">{toPersianDigits(row.orderCount)}</span>,
            },
            { key: 'spent', header: 'مجموع خرید', cell: (row) => formatPrice(row.totalSpent) },
          ]}
        />
      </section>
    </AdminPage>
  );
}
