'use client';

import { useState, type FormEvent } from 'react';
import {
  Badge,
  Button,
  Card,
  FormStatus,
  Icon,
  JalaliDateInput,
  Select,
  buttonClasses,
} from '@bojan/ui';
import { FormLayout, FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

/**
 * Tehran's offset, fixed.
 *
 * The API takes an instant and the operator picks a day, so somebody has to
 * decide which instant a day starts at. Iran has not observed daylight saving
 * since 1401, so a constant is the whole of it — the same fallback
 * `PersianFormat.ToTehran` uses on the server.
 */
const TEHRAN_OFFSET = '+03:30';

/**
 * A picked day as the moment the API means by it.
 *
 * Both date fields were plain text inputs posting whatever was typed —
 * `۱۴۰۵/۰۵/۰۱`, or an empty string when the operator left them alone — straight
 * into a `DateTimeOffset?`. System.Text.Json parses neither, so the request was
 * refused during binding, before any handler ran, and the panel had nothing to
 * report but "ذخیره اطلاعات انجام نشد". Leaving the dates blank failed exactly
 * the same way, which is why this screen has never produced an export.
 *
 * `end` pushes to the last second of the day: a range that stops at midnight
 * excludes the day the operator asked for, and a report missing its final day
 * is worse than one that refuses to run.
 */
function instantFor(isoDate: string, edge: 'start' | 'end'): string | undefined {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(isoDate)) return undefined;
  return `${isoDate}T${edge === 'start' ? '00:00:00' : '23:59:59'}${TEHRAN_OFFSET}`;
}

/**
 * The six keys `ReportCatalogue` knows, spelled its way.
 *
 * This list said `products` and `finance`. Neither is a report: the catalogue
 * has `inventory` and `financial`, and it refuses a name it does not know at
 * the door — so two of the six entries in this dropdown could only ever answer
 * «این گزارش شناخته نشد», and `campaigns`, which does exist, was not offered at
 * all.
 */
const reports = [
  { value: 'sales', label: 'گزارش فروش', ownerOnly: false },
  { value: 'orders', label: 'گزارش سفارش‌ها', ownerOnly: false },
  { value: 'inventory', label: 'گزارش موجودی', ownerOnly: false },
  { value: 'customers', label: 'گزارش مشتریان', ownerOnly: false },
  { value: 'campaigns', label: 'گزارش کمپین‌ها', ownerOnly: false },
  // Mirrors the owner-only gate on GET /admin/reports/financial. The API
  // refuses it for anyone else whatever this offers; hiding it is what stops
  // the panel presenting a choice that ends in a refusal.
  { value: 'financial', label: 'گزارش مالی', ownerOnly: true },
] as const;

/**
 * What the export worker can actually build.
 *
 * It builds CSV and nothing else — `ReportExportWorker` throws
 * `NotSupportedException` on the other two, marks the job failed and stores the
 * reason where no screen reads it. This picker offered all three and started on
 * Excel, so the plain use of this screen queued a job that could only fail and
 * reported it as queued. The two that do not exist are drawn as unavailable
 * rather than removed: an operator looking for Excel should find out why it is
 * not there instead of wondering whether they misremembered.
 */
const formats = [
  { id: 'xlsx', label: 'Excel', icon: 'table_view', note: 'برای تحلیل و فیلتر کردن', ready: true },
  {
    id: 'csv',
    label: 'CSV',
    icon: 'description',
    note: 'برای ورود به سامانه‌های دیگر',
    ready: true,
  },
  {
    id: 'pdf',
    label: 'PDF',
    icon: 'picture_as_pdf',
    note: 'برای چاپ و بایگانی — فارسی، راست‌چین، با فونت جاسازی‌شده',
    ready: true,
  },
];

/** Screen 140 — Report exporter. */
export function ReportExporter({ role }: { role: string }) {
  const available = reports.filter((report) => !report.ownerOnly || role === 'owner');
  const [format, setFormat] = useState('xlsx');
  /** The id of the export just queued, which is also its download address. */
  const [queued, setQueued] = useState<string | null>(null);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);

    const from = instantFor(String(data.get('from') ?? ''), 'start');
    const to = instantFor(String(data.get('to') ?? ''), 'end');

    setWorking(true);
    setError(null);
    try {
      // An omitted date is omitted from the body rather than sent empty — the
      // field is optional on the API and "" is not a value it can bind.
      const created = await postJson<{ id?: string }>('/api/admin/report-exports', {
        report: String(data.get('report') ?? 'sales'),
        format,
        ...(from ? { from } : null),
        ...(to ? { to } : null),
      });

      // The id is what makes the file reachable. This screen used to throw it
      // away and tell the operator to wait for an email that nothing sends.
      setQueued(created?.id ?? null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت درخواست خروجی انجام نشد.');
    } finally {
      setWorking(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <FormLayout
        aside={
          <FormSection title="خروجی این جلسه" icon="history">
            {/*
              What was queued from this screen, not a history: the API has no
              endpoint that lists past exports, and a panel that invented one
              would be claiming files it cannot produce. The id comes back from
              the queue call and is the download address, which is all this
              needs to be useful.
            */}
            <div className="gap-md flex flex-col">
              {queued === null ? (
                <p className="text-caption text-on-surface-variant">هنوز خروجی‌ای نساخته‌اید.</p>
              ) : (
                <div className="gap-sm flex flex-col">
                  <Badge tone="mint" className="self-start">
                    در صف ساخت
                  </Badge>
                  <p className="text-caption text-on-surface-variant leading-relaxed">
                    ساخت فایل چند لحظه طول می‌کشد. اگر دکمه‌ی زیر گفت آماده نیست، کمی بعد دوباره
                    بزنید.
                  </p>
                  {/* A plain anchor: the route streams a file and attaches the
                      operator's credential on the way, which a router
                      navigation would not do. */}
                  <a
                    href={`/api/admin/report-exports/${queued}/download`}
                    className={buttonClasses({
                      variant: 'outline',
                      size: 'sm',
                      className: 'gap-xs self-start',
                    })}
                  >
                    <Icon name="download" size={18} />
                    دانلود خروجی
                  </a>
                </div>
              )}
            </div>
          </FormSection>
        }
        actions={
          <>
            <Button type="submit" size="lg" loading={working} icon="download" className="px-xl">
              ساخت خروجی
            </Button>
            <FormStatus error={error} />
            {/*
              It said the link would be emailed. Nothing in the export worker
              sends mail, so on every installation that sentence was false —
              and on this one, where no mailbox is configured at all, it sent
              the operator to wait by an inbox for a file sitting on the server.
            */}
            <FormStatus
              ok={queued ? 'خروجی در صف ساخت قرار گرفت. از کادر کناری دانلودش کنید.' : null}
            />
          </>
        }
      >
        {/*
          `assessment` is not a ligature this font carries and never has been —
          not in any version of the subset — so this heading drew the word
          "assessment" beside «گزارش و بازه» instead of an icon. `list_alt` is
          in the subset, means the same thing here, and is not the `table_view`
          the Excel tile below already uses.
        */}
        <FormSection title="گزارش و بازه" icon="list_alt">
          <Select name="report" label="نوع گزارش" defaultValue="sales">
            {available.map((report) => (
              <option key={report.value} value={report.value}>
                {report.label}
              </option>
            ))}
          </Select>

          <div className="gap-md grid md:grid-cols-2">
            {/* The panel's own date control, which shows a Persian calendar and
                hands the form ISO. The plain text boxes it replaces let an
                operator type a Jalali date the API could not read. */}
            <JalaliDateInput
              name="from"
              label="از تاریخ"
              hint="خالی بگذارید تا از ابتدا حساب شود."
            />
            <JalaliDateInput name="to" label="تا تاریخ" hint="خالی بگذارید تا تا امروز حساب شود." />
          </div>
        </FormSection>

        <FormSection title="قالب خروجی" icon="download">
          <div className="gap-md grid sm:grid-cols-3">
            {formats.map((item) => (
              <button
                key={item.id}
                type="button"
                aria-pressed={format === item.id}
                disabled={!item.ready}
                title={item.ready ? undefined : 'این قالب هنوز روی سرور ساخته نمی‌شود.'}
                onClick={() => setFormat(item.id)}
                className={`gap-xs p-md flex flex-col items-start rounded-lg border text-start transition-colors ${
                  format === item.id
                    ? 'border-primary bg-soft-mint/30'
                    : 'border-outline-variant enabled:hover:bg-surface-container-low'
                } disabled:cursor-not-allowed disabled:opacity-50`}
              >
                <Icon
                  name={item.icon}
                  size={22}
                  className={item.ready ? 'text-primary' : 'text-outline'}
                />
                <span className="latin text-body-md text-on-surface font-medium">{item.label}</span>
                <span className="text-caption text-on-surface-variant">{item.note}</span>
              </button>
            ))}
          </div>
        </FormSection>

        {/*
          A "ستون‌ها" section used to sit here: six ticked checkboxes an
          operator could untick, posted nowhere. The API's export request has no
          column field and each report's writer builds a fixed set, so every
          combination produced the same file. A control that cannot change the
          result is worse than a missing one — it invites somebody to untick
          "مبلغ" and hand the sheet to an accountant.
        */}
        <Card className="gap-sm p-md flex items-start">
          <Icon name="info" size={20} className="text-primary mt-px shrink-0" />
          <p className="text-caption text-on-surface-variant leading-relaxed">
            خروجی‌های بزرگ در صف پردازش قرار می‌گیرند و لینک دانلود پس از آماده شدن ایمیل می‌شود —
            پس تا وقتی صندوق پستی فروشگاه در «تنظیمات ← صندوق پستی» تنظیم نشده باشد، لینک جایی
            نمی‌رود.
          </p>
        </Card>
      </FormLayout>
    </form>
  );
}
