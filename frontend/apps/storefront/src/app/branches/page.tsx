import type { Metadata } from 'next';
import { EmptyState, Icon, SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getBranches } from '@/lib/api/features';

// Reads the catalogue-adjacent feature API at request time, not build time.
export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'شعبه‌های ما',
  description: 'نشانی، ساعت کاری و امکان تحویل حضوری شعبه‌های فروشگاه.',
};

const DAY_LABELS: Record<string, string> = {
  sat: 'شنبه',
  sun: 'یک‌شنبه',
  mon: 'دوشنبه',
  tue: 'سه‌شنبه',
  wed: 'چهارشنبه',
  thu: 'پنج‌شنبه',
  fri: 'جمعه',
};

/** Storefront "our branches" page, backed by the multi-location feature. */
export default async function BranchesPage() {
  const branches = await getBranches();

  return (
    <Container className="py-8">
      <SectionHeader title="شعبه‌های ما" subtitle="ما را از نزدیک ببینید یا سفارش‌تان را حضوری تحویل بگیرید." />

      {branches.length === 0 ? (
        <EmptyState
          icon="storefront"
          title="هنوز شعبه‌ای ثبت نشده"
          description="به‌زودی نشانی و ساعت کاری شعبه‌ها این‌جا نمایش داده می‌شود."
        />
      ) : (
        <div className="gap-lg mt-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3">
          {branches.map((b) => {
            const hours = Object.entries(b.hours ?? {});
            return (
              <article
                key={b.id}
                className="border-outline-variant bg-surface gap-sm flex flex-col rounded-2xl border p-5"
              >
                <div className="flex items-center justify-between gap-2">
                  <h2 className="text-title-md text-on-surface font-bold">{b.name}</h2>
                  {b.pickup ? (
                    <span className="bg-soft-mint text-primary text-label-sm rounded-full px-2.5 py-1 font-bold">
                      تحویل حضوری
                    </span>
                  ) : null}
                </div>

                {b.city ? <p className="text-body-md text-on-surface-variant">{b.city}</p> : null}
                {b.address ? (
                  <p className="text-body-md text-on-surface flex items-start gap-2">
                    <Icon name="location_on" className="text-primary mt-0.5 shrink-0 text-[20px]" />
                    <span>{b.address}</span>
                  </p>
                ) : null}
                {b.phone ? (
                  <p className="text-body-md text-on-surface flex items-center gap-2">
                    <Icon name="call" className="text-primary shrink-0 text-[20px]" />
                    <a href={`tel:${b.phone}`} dir="ltr" className="hover:text-primary">
                      {b.phone}
                    </a>
                  </p>
                ) : null}

                {hours.length > 0 ? (
                  <div className="mt-1">
                    <p className="text-label-md text-on-surface-variant mb-1 font-bold">ساعت کاری</p>
                    <ul className="text-body-sm text-on-surface-variant space-y-0.5">
                      {hours.map(([day, value]) => (
                        <li key={day} className="flex justify-between gap-3">
                          <span>{DAY_LABELS[day] ?? day}</span>
                          <span dir="ltr">{value}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}
              </article>
            );
          })}
        </div>
      )}
    </Container>
  );
}
