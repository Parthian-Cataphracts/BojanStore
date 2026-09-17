import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { HELP_GROUPS } from '@/lib/help-content';

export const metadata: Metadata = { title: 'راهنمای استفاده' };

/** The dedicated guide page: every store-panel section, each linked to its screen. */
export default function Page() {
  return (
    <AdminPage
      title="راهنمای استفاده"
      description="راهنمای ساده و کامل همهٔ بخش‌های پنل فروشگاه. روی «رفتن به این بخش» بزنید تا مستقیم بروید."
    >
      <div className="gap-lg flex flex-col">
        {HELP_GROUPS.map((group) => (
          <section key={group.group}>
            <h2 className="text-title-md text-primary border-outline-variant mb-3 border-b pb-2 font-bold">
              {group.group}
            </h2>
            <div className="gap-md grid grid-cols-1 lg:grid-cols-2">
              {group.entries.map((entry) => (
                <article
                  key={entry.href}
                  className="border-outline-variant bg-surface-container-low rounded-xl border p-4"
                >
                  <div className="flex items-center justify-between gap-3">
                    <h3 className="text-label-lg text-on-surface font-bold">{entry.title}</h3>
                    <Link
                      href={entry.href}
                      className="text-primary text-label-md inline-flex shrink-0 items-center gap-1 font-bold hover:underline"
                    >
                      رفتن به این بخش
                      <Icon name="arrow_back" className="text-[18px] rtl:-scale-x-100" />
                    </Link>
                  </div>
                  <p className="text-body-md text-on-surface-variant mt-2 leading-7">{entry.what}</p>
                  <ol className="text-body-md text-on-surface-variant mt-3 list-decimal space-y-1.5 ps-5 leading-7">
                    {entry.steps.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ol>
                  {entry.caution && (
                    <p className="bg-error-container text-on-error-container text-body-md mt-3 flex items-start gap-2 rounded-lg p-2.5 leading-7">
                      <Icon name="warning" className="mt-0.5 shrink-0 text-[18px]" />
                      <span>{entry.caution}</span>
                    </p>
                  )}
                </article>
              ))}
            </div>
          </section>
        ))}

        <section className="border-outline-variant bg-surface-container-low rounded-xl border p-4">
          <h2 className="text-title-md text-primary mb-2 font-bold">افزونه‌ها</h2>
          <p className="text-body-md text-on-surface-variant leading-7">
            راهنمای هر افزونهٔ نصب‌شده داخل خودِ صفحهٔ آن افزونه آمده است. برای دیدن
            فهرست، به{' '}
            <Link href="/extensions" className="text-primary font-bold hover:underline">
              «افزونه‌ها»
            </Link>{' '}
            بروید.
          </p>
        </section>
      </div>
    </AdminPage>
  );
}
