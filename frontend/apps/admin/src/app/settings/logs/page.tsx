import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Card, Code, EmptyState, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { FilterBar } from '@/components/FilterBar';
import { getLogFiles, getLogTail } from '@/lib/api/logs';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'لاگ سرور' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

/** Bytes as something a person reads, in Persian digits. */
function size(bytes: number): string {
  if (bytes < 1024) return `${toPersianDigits(bytes)} بایت`;
  if (bytes < 1024 * 1024) return `${toPersianDigits(Math.round(bytes / 1024))} کیلوبایت`;
  return `${toPersianDigits((bytes / (1024 * 1024)).toFixed(1))} مگابایت`;
}

/**
 * What a line is drawn in, by its level.
 *
 * The level is parsed by the API rather than guessed at here — the format
 * belongs to the sink, and the panel should not have to know it. A line with no
 * level is a continuation of the one above it, usually a stack frame, and is
 * drawn quietly so the line that names the failure stands out from its trace.
 */
function toneOf(level: string | null | undefined): string {
  if (level === 'ERR' || level === 'FTL') return 'text-error';
  if (level === 'WRN') return 'text-tertiary';
  if (!level) return 'text-outline';
  return 'text-on-surface-variant';
}

/**
 * Screen — لاگ سرور.
 *
 * Owner only, and read only. There is no delete: an operator who could clear
 * the log could clear the record of what they did, which is the one thing a log
 * exists to prevent. Retention belongs to the sink, which keeps a fortnight.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');

  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const requested = first(params.file);

  const files = await getLogFiles();
  // Newest by default — the file being written to is the one somebody wants.
  const selected = requested && files.some((f) => f.name === requested) ? requested : files[0]?.name;
  const tail = selected ? await getLogTail(selected, { q: query }) : null;

  return (
    <AdminPage
      title="لاگ سرور"
      description="آنچه برنامه درباره‌ی خودش نوشته است. برای دیدن دلیل یک خطا، دیگر لازم نیست به سرور وصل شوید."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'لاگ سرور' },
      ]}
      actions={
        files.length > 0 ? (
          // Everything retained, as one archive — what you want when the answer
          // is not in today's file and you would rather read it somewhere else
          // than page through a fortnight here.
          // A route handler streaming a zip, not a page: `<Link>` would
          // navigate the router at it and the download would never start. The
          // per-file link below escapes this rule only because its href is
          // built from a variable, which is luck rather than a distinction.
          // eslint-disable-next-line @next/next/no-html-link-for-pages
          <a
            href="/api/admin/logs/download"
            className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
          >
            <Icon name="download" size={18} />
            دانلود همه
          </a>
        ) : undefined
      }
    >
      {files.length === 0 ? (
        <EmptyState
          icon="description"
          title="فایلی برای خواندن نیست"
          description="یا برنامه تازه بالا آمده و هنوز چیزی ننوشته، یا مسیر لاگ (Logs__Directory) روی این نصب تنظیم نشده است."
        />
      ) : (
        <>
          {/* One chip per file. The sink rolls daily, so this is a fortnight of
              days and picking one is picking a day. */}
          <div className="hide-scrollbar flex gap-sm overflow-x-auto pb-sm">
            {files.map((file) => {
              const active = file.name === selected;
              return (
                <Link
                  key={file.name}
                  href={`/settings/logs?file=${encodeURIComponent(file.name)}${query ? `&q=${encodeURIComponent(query)}` : ''}`}
                  className={`flex shrink-0 flex-col gap-2xs rounded-xl border px-md py-sm transition-colors ${
                    active
                      ? 'border-primary bg-primary-fixed text-on-primary-fixed'
                      : 'border-outline-variant text-on-surface-variant hover:border-secondary-container'
                  }`}
                >
                  <span className="latin text-label-md" dir="ltr">
                    {file.name}
                  </span>
                  <span className="text-caption opacity-80">
                    {size(file.sizeBytes)} — {formatDate(file.modifiedAtUtc, 'long')}
                  </span>
                </Link>
              );
            })}
          </div>

          <Suspense fallback={null}>
            <FilterBar searchPlaceholder="جستجو در متن لاگ، مثلاً error..." />
          </Suspense>

          {tail === null ? (
            <EmptyState
              icon="error"
              title="خواندن این فایل انجام نشد"
              description="ممکن است همین حالا چرخانده شده باشد. فهرست بالا را دوباره بارگذاری کنید."
            />
          ) : tail.lines.length === 0 ? (
            <EmptyState
              icon="search_off"
              title={query ? 'چیزی با این عبارت پیدا نشد' : 'این فایل خالی است'}
              description={query ? 'عبارت دیگری را امتحان کنید.' : undefined}
            />
          ) : (
            <Card className="flex flex-col gap-sm p-lg">
              <div className="flex flex-wrap items-center justify-between gap-md">
                <p className="flex items-center gap-xs text-caption text-on-surface-variant">
                  <Icon name="history" size={16} />
                  {tail.truncated
                    ? `آخرین ${toPersianDigits(tail.lines.length)} خط از ${toPersianDigits(tail.matched)}. جدیدترین بالاست.`
                    : `${toPersianDigits(tail.lines.length)} خط. جدیدترین بالاست.`}
                </p>

                <a
                  href={`/api/admin/logs/download?name=${encodeURIComponent(tail.name)}`}
                  className="flex items-center gap-2xs text-caption text-primary hover:underline"
                >
                  <Icon name="download" size={16} />
                  دانلود همین فایل
                </a>
              </div>

              {/* `overflow-x-auto` on its own box: a stack trace is one long
                  line, and letting it stretch the page is how the six order
                  screens ended up 225px wide on a phone. */}
              <div className="overflow-x-auto">
                <pre className="min-w-0 text-caption leading-relaxed" dir="ltr">
                  {tail.lines.map((line, index) => (
                    <Code
                      key={index}
                      className={`block whitespace-pre bg-transparent px-0 ${toneOf(line.level)}`}
                    >
                      {line.raw}
                    </Code>
                  ))}
                </pre>
              </div>
            </Card>
          )}
        </>
      )}
    </AdminPage>
  );
}
