import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { Card } from '@bojan/ui';
import { getKnightStatus } from '@/lib/api/knight';

export const dynamic = 'force-dynamic';

/**
 * A single delivered Feature's screen, mounted in an iframe.
 *
 * The screen is the Feature's own HTML, served by its service and reached
 * through `/api/features/...` on this origin — where the route handler adds the
 * staff identity the proxy needs (a shopper reaching the same URL is refused).
 * We frame it rather than re-render it: the shop decides where the screen hangs,
 * and the Feature decides what is on it, which is the one division that survives
 * either side changing independently.
 */
export default async function Page({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const status = await getKnightStatus();
  const feature = status.features.find((f) => f.slug === slug && f.enabled);
  if (!feature) notFound();

  // Prefer the admin route; fall back to the first route the Feature serves.
  const route =
    feature.routes.find((r) => r.includes('/admin/')) ?? feature.routes[0];
  const mount = feature.mounts.find((m) => m.slot === 'admin.sidebar') ?? feature.mounts[0];
  const label = mount?.label ?? slug;

  return (
    <AdminPage
      title={label}
      description="صفحه‌ای که این افزونه از طریق نایت ارائه می‌دهد."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'افزونه‌ها', href: '/extensions' },
        { label },
      ]}
    >
      {route ? (
        <Card>
          <iframe
            src={route}
            title={label}
            className="w-full rounded-lg border-0"
            style={{ height: 'calc(100vh - 220px)', minHeight: '480px' }}
          />
        </Card>
      ) : (
        <Card>
          <p className="p-lg text-body-medium text-on-surface-variant">
            این افزونه صفحه‌ی نمایشی ارائه نمی‌دهد.
          </p>
        </Card>
      )}
    </AdminPage>
  );
}
