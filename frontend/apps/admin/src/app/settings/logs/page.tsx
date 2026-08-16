import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import {
  Badge,
  Card,
  EmptyState,
  Icon,
  buttonClasses,
  cn,
  formatDate,
  toPersianDigits,
  type BadgeTone,
} from '@bojan/ui';
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
 * How many lines to ask for.
 *
 * The reader already took a `limit`; the screen simply never offered a way to
 * change it and asked for 300 every time. Zero means the ceiling rather than
 * literally everything — a fortnight of one file is not something to render at
 * once, and the API's own maximum is what decides where that stops.
 */
const TAIL_SIZES = [50, 100, 500, 0] as const;

function tailLabel(value: number): string {
  return value === 0 ? 'همه' : toPersianDigits(value);
}

/**
 * What a line is drawn as, by its level.
 *
 * The sink writes Serilog's three-letter codes, and the API hands them over
 * parsed rather than making the panel guess at the format. A line with no level
 * is a continuation of the one above it — usually a stack frame, and usually
 * the part somebody is actually reading — so it keeps the badge column empty
 * and the message at full weight instead of being labelled something it is not.
 */
const LEVELS: Record<string, { label: string; tone: BadgeTone; message: string }> = {
  FTL: { label: 'بحرانی', tone: 'error', message: 'font-semibold text-error' },
  ERR: { label: 'خطا', tone: 'error', message: 'text-error' },
  WRN: { label: 'هشدار', tone: 'warning', message: 'text-on-surface' },
  INF: { label: 'اطلاع', tone: 'teal', message: 'text-on-surface-variant' },
  DBG: { label: 'دیباگ', tone: 'neutral', message: 'text-outline' },
  VRB: { label: 'ورباز', tone: 'neutral', message: 'text-outline' },
};

/** The current view as a query string, with one key replaced. */
function linkTo(
  current: { file?: string | undefined; q: string; tail: number },
  patch: Partial<{ file: string; q: string; tail: number }>,
): string {
  const next = { ...current, ...patch };
  const params = new URLSearchParams();
  if (next.file) params.set('file', next.file);
  if (next.q) params.set('q', next.q);
  if (next.tail !== 100) params.set('tail', String(next.tail));
  const query = params.toString();
  return query ? `/settings/logs?${query}` : '/settings/logs';
}

/**
 * Screen — لاگ سرور.
 *
 * Owner only, and read only. There is no delete: an operator who could clear
 * the log could clear the record of what they did, which is the one thing a log
 * exists to prevent. Retention belongs to the sink, which keeps a fortnight.
 *
 * Every control writes to the URL rather than to component state, which is the
 * contract every list screen in this panel keeps: a filtered view is a link, it
 * survives a reload, and the back button walks it. That is the one thing done
 * differently from the screen this was modelled on, whose controls were React
 * state and whose view could not be sent to anybody.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');

  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const requested = first(params.file);
  const rawTail = Number(first(params.tail) ?? 100);
  const tailSize = (TAIL_SIZES as readonly number[]).includes(rawTail) ? rawTail : 100;

  const files = await getLogFiles();
  // Newest by default — the file being written to is the one somebody wants.
  const selected =
    requested && files.some((f) => f.name === requested) ? requested : files[0]?.name;
  // Zero travels straight through: `LogFileReader` reads a non-positive limit
  // as its own configured ceiling, so «همه» is the API's maximum rather than a
  // number picked here that would have to be kept in step with it.
  const tail = selected ? await getLogTail(selected, { q: query, limit: tailSize }) : null;

  const view = { ...(selected ? { file: selected } : null), q: query, tail: tailSize };

  return (
    <AdminPage
      title="لاگ سرور"
      description="آنچه برنامه درباره‌ی خودش نوشته است. برای دیدن دلیل یک خطا، دیگر لازم نیست به سرور وصل شوید."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'سیستم و دسترسی' },
        { label: 'لاگ سرور' },
      ]}
      actions={
        files.length > 0 ? (
          <>
            {/*
              Re-runs the server component, which re-reads the directory and the
              file. A link to the view it is already on, so it carries the
              chosen file, search and tail size with it.
            */}
            <Link
              href={linkTo(view, {})}
              prefetch={false}
              className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
            >
              <Icon name="sync" size={18} />
              بازخوانی
            </Link>

            {/*
              Everything retained, as one archive — what you want when the answer
              is not in today's file and you would rather read it somewhere else
              than page through a fortnight here.
              A route handler streaming a zip, not a page: `<Link>` would
              navigate the router at it and the download would never start.
              eslint-disable-next-line @next/next/no-html-link-for-pages
            */}
            {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
            <a
              href="/api/admin/logs/download"
              className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
            >
              <Icon name="download" size={18} />
              دانلود همه
            </a>
          </>
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
          <div className="hide-scrollbar gap-sm pb-sm flex overflow-x-auto">
            {files.map((file) => {
              const active = file.name === selected;
              return (
                <Link
                  key={file.name}
                  href={linkTo(view, { file: file.name })}
                  className={cn(
                    'gap-xs px-md py-sm flex shrink-0 flex-col rounded-xl border transition-colors',
                    active
                      ? 'border-primary bg-primary-fixed text-on-primary-fixed'
                      : 'border-outline-variant text-on-surface-variant hover:border-secondary-container',
                  )}
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

          <div className="gap-md flex flex-wrap items-center">
            <Suspense fallback={null}>
              <div className="min-w-[16rem] flex-1">
                <FilterBar searchPlaceholder="جستجو در متن لاگ، مثلاً error..." />
              </div>
            </Suspense>

            {/* How much of the file to pull back. Its own control rather than a
                filter chip, because it does not narrow what matched — it decides
                how much of what matched is drawn. */}
            <div className="gap-xs border-outline-variant/70 bg-surface-container-lowest flex shrink-0 items-center rounded-xl border p-1">
              <span className="px-sm text-caption text-on-surface-variant">تعداد خط:</span>
              {TAIL_SIZES.map((option) => (
                <Link
                  key={option}
                  href={linkTo(view, { tail: option })}
                  className={cn(
                    'px-md py-xs text-caption rounded-lg font-medium transition-colors',
                    tailSize === option
                      ? 'bg-primary-fixed text-on-primary-fixed'
                      : 'text-on-surface-variant hover:bg-surface-container',
                  )}
                >
                  {tailLabel(option)}
                </Link>
              ))}
            </div>
          </div>

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
            <Card surface="plain" className="overflow-hidden">
              <header className="gap-md border-outline-variant/40 px-lg py-md flex flex-wrap items-center justify-between border-b">
                <p className="gap-xs text-caption text-on-surface-variant flex items-center">
                  <Icon name="history" size={16} />
                  نمایش {toPersianDigits(tail.lines.length)} از {toPersianDigits(tail.matched)} خط
                  {query ? ` برای «${query}»` : ''} — جدیدترین بالاست.
                </p>

                <a
                  href={`/api/admin/logs/download?name=${encodeURIComponent(tail.name)}`}
                  className="gap-xs text-caption text-primary flex items-center hover:underline"
                >
                  <Icon name="download" size={16} />
                  دانلود همین فایل
                </a>
              </header>

              {/*
                A row per line rather than one `<pre>` of the whole tail: the
                level, the moment and the message are three different things and
                the file only looks like one string because that is how it is
                stored. The message keeps `dir="ltr"` and a monospace face — it
                is English, often with a path or a stack frame in it — while the
                page around it stays Persian.
              */}
              <div className="divide-outline-variant/30 max-h-[64vh] divide-y overflow-y-auto">
                {tail.lines.map((line, index) => {
                  const level = line.level ? LEVELS[line.level] : undefined;

                  return (
                    <div
                      key={index}
                      className="gap-xs px-lg py-sm hover:bg-surface-container-low flex flex-col transition-colors"
                    >
                      {(level || line.at) && (
                        <div className="gap-sm flex flex-wrap items-center">
                          {level && <Badge tone={level.tone}>{level.label}</Badge>}
                          {line.at && (
                            <span className="latin text-helper text-on-surface-variant" dir="ltr">
                              {line.at}
                            </span>
                          )}
                        </div>
                      )}

                      <p
                        dir="ltr"
                        className={cn(
                          'latin text-caption overflow-x-auto whitespace-pre-wrap break-words text-start leading-relaxed',
                          level?.message ?? 'text-outline',
                        )}
                      >
                        {line.message || line.raw}
                      </p>
                    </div>
                  );
                })}
              </div>
            </Card>
          )}
        </>
      )}
    </AdminPage>
  );
}
