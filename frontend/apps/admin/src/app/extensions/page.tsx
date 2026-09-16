import type { Metadata } from 'next';
import Link from 'next/link';
import { AdminPage } from '@/components/AdminPage';
import { Badge, Card } from '@bojan/ui';
import { getKnightStatus } from '@/lib/api/knight';

export const metadata: Metadata = { title: 'افزونه‌ها' };

/**
 * افزونه‌ها — the Features KNIGHT has delivered to this shop, each with its own
 * screen.
 *
 * The panel's fixed navigation is the screens this shop was built with; this is
 * the screens it was *given*. A delivered Feature declares where its screen
 * hangs (`admin.sidebar`) and the store serves it behind the proxy — so the
 * only thing this page does is list what arrived and link through to it. A new
 * Feature appears here the moment it is delivered, with no change to the panel.
 */
export const dynamic = 'force-dynamic';

export default async function Page() {
  const status = await getKnightStatus();
  const features = status.features.filter(
    (feature) => feature.enabled && feature.mounts.some((mount) => mount.slot.startsWith('admin.')),
  );

  return (
    <AdminPage
      title="افزونه‌ها"
      description="قابلیت‌هایی که از نایت تحویل گرفته‌اید و صفحه‌ی خودشان را دارند."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'افزونه‌ها' }]}
    >
      {features.length === 0 ? (
        <Card>
          <p className="p-lg text-body-medium leading-relaxed text-on-surface-variant">
            هنوز افزونه‌ای با صفحه‌ی نمایش تحویل داده نشده است. هر قابلیتی که فروشگاه حق استفاده از آن
            را داشته باشد، خودکار از نایت می‌رسد و همین‌جا ظاهر می‌شود.
          </p>
        </Card>
      ) : (
        <div className="flex flex-col gap-md">
          {features.map((feature) => {
            const mount =
              feature.mounts.find((m) => m.slot === 'admin.sidebar') ?? feature.mounts[0];
            return (
              <Link key={feature.slug} href={`/extensions/${feature.slug}`}>
                <Card className="transition hover:border-primary">
                  <div className="flex flex-wrap items-center gap-sm p-lg">
                    <span className="text-body-large">{mount?.label ?? feature.slug}</span>
                    <Badge tone="neutral">{feature.version}</Badge>
                    <Badge tone="success">فعال</Badge>
                    {feature.architecture === 'external_service' ? (
                      <Badge tone="teal">سرویس بیرونی</Badge>
                    ) : null}
                  </div>
                </Card>
              </Link>
            );
          })}
        </div>
      )}
    </AdminPage>
  );
}
