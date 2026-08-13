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
  formatDateTime,
  toPersianDigits,
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

const formats = [
  { id: 'xlsx', label: 'Excel', icon: 'table_view', note: 'برای تحلیل و فیلتر کردن' },
  { id: 'csv', label: 'CSV', icon: 'description', note: 'برای ورود به سامانه‌های دیگر' },
  { id: 'pdf', label: 'PDF', icon: 'picture_as_pdf', note: 'برای بایگانی و چاپ' },
];

/**
 * Recent export jobs. `POST /report-exports` queues a job and mails a link —
 * there is no listing endpoint yet to show its progress here, so this stays
 * empty rather than inventing rows that would claim past exports exist.
 */
const history: { id: string; report: string; at: string; rows: number; ready: boolean }[] = [];

/** Screen 140 — Report exporter. */
export function ReportExporter({ role }: { role: string }) {
  const available = reports.filter((report) => !report.ownerOnly || role === 'owner');
  const [format, setFormat] = useState('xlsx');
  const [queued, setQueued] = useState(false);
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
      // The backend queues the job and mails a link; nothing downloads here.
      // An omitted date is omitted from the body rather than sent empty — the
      // field is optional on the API and "" is not a value it can bind.
      await postJson('/api/admin/report-exports', {
        report: String(data.get('report') ?? 'sales'),
        format,
        ...(from ? { from } : null),
        ...(to ? { to } : null),
      });
      setQueued(true);
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
          <FormSection title="خروجی‌های اخیر" icon="history">
            <div className="flex flex-col gap-md">
              {history.length === 0 && (
                <p className="text-caption text-on-surface-variant">خروجی‌ای ثبت نشده است.</p>
              )}
              {history.map((item) => (
                <div
                  key={item.id}
                  className="flex flex-col gap-xs border-b border-paper-border pb-md last:border-0 last:pb-0"
                >
                  <div className="flex items-center justify-between gap-sm">
                    <span className="text-body-md text-on-surface">{item.report}</span>
                    {item.ready ? (
                      <Badge tone="mint">آماده</Badge>
                    ) : (
                      <Badge tone="warning">در حال ساخت</Badge>
                    )}
                  </div>
                  <span className="tabular text-caption text-outline">
                    {toPersianDigits(item.rows)} ردیف · {formatDateTime(item.at)}
                  </span>
                </div>
              ))}
            </div>
          </FormSection>
        }
        actions={
          <>
            <Button type="submit" size="lg" loading={working} icon="download" className="px-xl">
              ساخت خروجی
            </Button>
            <FormStatus error={error} />
            <FormStatus ok={queued ? 'درخواست در صف قرار گرفت؛ لینک دانلود ایمیل می‌شود.' : null} />
          </>
        }
      >
        <FormSection title="گزارش و بازه" icon="assessment">
          <Select name="report" label="نوع گزارش" defaultValue="sales">
            {available.map((report) => (
              <option key={report.value} value={report.value}>
                {report.label}
              </option>
            ))}
          </Select>

          <div className="grid gap-md md:grid-cols-2">
            {/* The panel's own date control, which shows a Persian calendar and
                hands the form ISO. The plain text boxes it replaces let an
                operator type a Jalali date the API could not read. */}
            <JalaliDateInput name="from" label="از تاریخ" hint="خالی بگذارید تا از ابتدا حساب شود." />
            <JalaliDateInput name="to" label="تا تاریخ" hint="خالی بگذارید تا تا امروز حساب شود." />
          </div>
        </FormSection>

        <FormSection title="قالب خروجی" icon="download">
          <div className="grid gap-md sm:grid-cols-3">
            {formats.map((item) => (
              <button
                key={item.id}
                type="button"
                aria-pressed={format === item.id}
                onClick={() => setFormat(item.id)}
                className={`flex flex-col items-start gap-xs rounded-lg border p-md text-start transition-colors ${
                  format === item.id
                    ? 'border-primary bg-soft-mint/30'
                    : 'border-outline-variant hover:bg-surface-container-low'
                }`}
              >
                <Icon name={item.icon} size={22} className="text-primary" />
                <span className="latin text-body-md font-medium text-on-surface">{item.label}</span>
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
        <Card className="flex items-start gap-sm p-md">
          <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
          <p className="text-caption leading-relaxed text-on-surface-variant">
            خروجی‌های بزرگ در صف پردازش قرار می‌گیرند و لینک دانلود پس از آماده شدن ایمیل می‌شود —
            پس تا وقتی صندوق پستی فروشگاه در «تنظیمات ← صندوق پستی» تنظیم نشده باشد، لینک جایی
            نمی‌رود.
          </p>
        </Card>
      </FormLayout>
    </form>
  );
}
