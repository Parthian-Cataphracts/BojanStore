import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { KnightConnectionPanel } from '@/components/KnightConnectionPanel';
import { getKnightStatus } from '@/lib/api/knight';

export const metadata: Metadata = { title: 'اتصال به نایت' };

/**
 * اتصال به نایت — the platform this shop takes its Features from.
 *
 * Its own screen rather than a section of «API و وبهوک», because the two answer
 * different questions. That screen is about credentials this shop *issues* to
 * other systems; this one is about the single credential it *holds*, and about
 * a link that either works or does not — which is the first thing somebody
 * checks when a Feature they are paying for is not there.
 *
 * Not cached. A connection screen showing a state from a minute ago is a
 * connection screen that lies at exactly the moment somebody is looking at it.
 */
export const dynamic = 'force-dynamic';

export default async function Page() {
  const status = await getKnightStatus();

  return (
    <AdminPage
      title="اتصال به نایت"
      description="وضعیت اتصال این فروشگاه به نایت، و قابلیت‌هایی که از آن تحویل گرفته است."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'نایت' },
      ]}
    >
      <KnightConnectionPanel status={status} />
    </AdminPage>
  );
}
