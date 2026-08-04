'use client';

import { useState, type FormEvent } from 'react';
import { Badge, Button, Card, Checkbox, Icon, Input, Select, formatDateTime, toPersianDigits } from '@bojan/ui';
import { FormLayout, FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

const reports = [
  { value: 'sales', label: 'گزارش فروش' },
  { value: 'orders', label: 'گزارش سفارش‌ها' },
  { value: 'products', label: 'گزارش محصولات' },
  { value: 'customers', label: 'گزارش مشتریان' },
  { value: 'inventory', label: 'گزارش موجودی' },
  { value: 'finance', label: 'گزارش مالی' },
];

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
export function ReportExporter() {
  const [format, setFormat] = useState('xlsx');
  const [queued, setQueued] = useState(false);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);

    setWorking(true);
    setError(null);
    try {
      // The backend queues the job and mails a link; nothing downloads here.
      await postJson('/api/admin/report-exports', {
        report: String(data.get('report') ?? 'sales'),
        format,
        from: String(data.get('from') ?? ''),
        to: String(data.get('to') ?? ''),
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
            {error && (
              <span role="alert" className="flex items-center gap-xs text-caption text-error">
                <Icon name="error" size={16} />
                {error}
              </span>
            )}
            {queued && (
              <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
                <Icon name="check_circle" size={16} />
                درخواست در صف قرار گرفت؛ لینک دانلود ایمیل می‌شود.
              </span>
            )}
          </>
        }
      >
        <FormSection title="گزارش و بازه" icon="assessment">
          <Select name="report" label="نوع گزارش" defaultValue="sales">
            {reports.map((report) => (
              <option key={report.value} value={report.value}>
                {report.label}
              </option>
            ))}
          </Select>

          <div className="grid gap-md md:grid-cols-2">
            <Input name="from" label="از تاریخ" placeholder="۱۴۰۵/۰۵/۰۱" icon="calendar_today" />
            <Input name="to" label="تا تاریخ" placeholder="۱۴۰۵/۰۵/۳۱" icon="calendar_today" />
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

        <FormSection title="ستون‌ها" icon="view_column">
          <div className="grid gap-sm sm:grid-cols-2">
            {['شناسه', 'تاریخ', 'مشتری', 'مبلغ', 'وضعیت', 'روش پرداخت'].map((column) => (
              <Checkbox key={column} name={column} label={column} defaultChecked />
            ))}
          </div>
        </FormSection>

        <Card className="flex items-start gap-sm p-md">
          <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
          <p className="text-caption leading-relaxed text-on-surface-variant">
            خروجی‌های بزرگ در صف پردازش قرار می‌گیرند و لینک دانلود پس از آماده شدن ایمیل می‌شود.
          </p>
        </Card>
      </FormLayout>
    </form>
  );
}
