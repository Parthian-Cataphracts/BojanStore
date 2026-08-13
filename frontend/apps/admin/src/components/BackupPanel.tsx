'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Badge, Button, Card, FormStatus, Icon, Select, formatDateTime, toPersianDigits } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';
import type { BackupJobDto } from '@/lib/api/types';

const STATUS_TONE: Record<BackupJobDto['status'], 'mint' | 'warning' | 'neutral' | 'error'> = {
  completed: 'mint',
  running: 'warning',
  queued: 'neutral',
  failed: 'error',
};

const STATUS_LABEL: Record<BackupJobDto['status'], string> = {
  completed: 'کامل شد',
  running: 'در حال اجرا',
  queued: 'در صف',
  failed: 'ناموفق',
};

/**
 * Screen 156 — Backup and restore.
 *
 * `POST /backups` queues the job and returns; the API's backup worker dumps the
 * database with `pg_dump`, archives the uploads tree beside it and marks the row
 * completed — or failed, with the reason, which is what the panel then shows.
 * `GET /backups` lists what actually happened, so this table is real rows.
 *
 * Because a real backup takes minutes rather than milliseconds, the list polls
 * while anything on it is still moving. Without that the operator pressed the
 * button, saw "در صف", and had no way to learn how it ended short of reloading
 * the page on a hunch.
 */
export function BackupPanel({ backups }: { backups: BackupJobDto[] }) {
  const router = useRouter();
  const [creating, setCreating] = useState(false);
  const [restoreTarget, setRestoreTarget] = useState<string | null>(null);
  const [confirmText, setConfirmText] = useState('');
  const [error, setError] = useState<string | null>(null);

  // Nothing pushes from the server, so the list asks again while a job is
  // still in flight and stops as soon as none is.
  const settling = backups.some((job) => job.status === 'queued' || job.status === 'running');

  useEffect(() => {
    if (!settling) return;

    const timer = window.setInterval(() => router.refresh(), 4000);
    return () => window.clearInterval(timer);
  }, [settling, router]);

  async function createBackup() {
    setCreating(true);
    setError(null);
    try {
      await postJson('/api/admin/backups', { kind: 'full', confirm: true });
      router.refresh();
      setError(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ساخت نسخه پشتیبان انجام نشد.');
    } finally {
      setCreating(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <div className="flex flex-wrap gap-md">
        <Button size="lg" loading={creating} icon="backup" onClick={createBackup} className="px-xl">
          ساخت نسخه پشتیبان
        </Button>
        {/*
          Still disabled: the API writes archives but nothing accepts one handed
          back to it. Restoring is a different endpoint with a different threat
          model — a stranger's bytes rather than this API's own output — and is
          separate work from making the backup real.
        */}
        <Button
          variant="outline"
          size="lg"
          icon="upload_file"
          disabled
          hint="بارگذاری فایل پشتیبان هنوز در سرور پیاده‌سازی نشده است."
          className="px-xl"
        >
          بارگذاری فایل پشتیبان
        </Button>
      </div>

      <FormStatus error={error} />


      <FormSection title="پشتیبان‌گیری خودکار" icon="schedule">
        <Select label="تناوب" defaultValue="daily">
          <option value="daily">روزانه (ساعت ۲ بامداد)</option>
          <option value="weekly">هفتگی</option>
          <option value="off">غیرفعال</option>
        </Select>

        <Select label="مدت نگهداری" defaultValue="30">
          <option value="7">۷ روز</option>
          <option value="30">۳۰ روز</option>
          <option value="90">۹۰ روز</option>
        </Select>
      </FormSection>

      <section className="flex flex-col gap-md">
        <h3 className="font-headline text-card-title text-primary">نسخه‌های موجود</h3>

        <DataTable
          rows={backups}
          rowKey={(row) => row.id}
          emptyIcon="backup"
          emptyTitle="نسخه پشتیبانی وجود ندارد"
          emptyDescription="اولین نسخه پشتیبان را بسازید."
          columns={[
            {
              key: 'at',
              header: 'تاریخ',
              cell: (row) => <span className="tabular">{formatDateTime(row.requestedAt)}</span>,
            },
            { key: 'kind', header: 'نوع', cell: (row) => <Badge tone="neutral">{row.kind}</Badge> },
            {
              key: 'status',
              header: 'وضعیت',
              /*
                The reason travels beside the badge, and always has: the worker
                stores why a backup failed and the API has been returning it in
                `error` this whole time. Nothing drew it. An owner saw «ناموفق»
                against a row with no size and had no way to find out whether
                pg_dump was missing, the disk was full or the dump timed out —
                three faults with three different answers, presented as one
                word. It is exactly the case a status column exists to open.
              */
              cell: (row) => (
                <div className="flex flex-col gap-2xs">
                  <Badge tone={STATUS_TONE[row.status]} className="self-start">
                    {STATUS_LABEL[row.status]}
                  </Badge>
                  {row.error && (
                    <span className="max-w-sm text-caption leading-relaxed text-error">
                      {row.error}
                    </span>
                  )}
                </div>
              ),
            },
            {
              key: 'size',
              header: 'حجم',
              cell: (row) => (
                <span className="tabular">
                  {row.sizeBytes ? `${toPersianDigits(Math.ceil(row.sizeBytes / 1024))} کیلوبایت` : '—'}
                </span>
              ),
            },
          ]}
          actions={(row) => (
            <div className="flex items-center gap-xs">
              <a
                href={row.downloadable ? `/api/admin/backups/${row.id}/download` : undefined}
                aria-label="دانلود نسخه پشتیبان"
                aria-disabled={!row.downloadable}
                title={row.downloadable ? undefined : 'این نسخه هنوز فایلی ندارد.'}
                className="rounded p-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary aria-disabled:pointer-events-none aria-disabled:opacity-40"
              >
                <Icon name="download" size={18} />
              </a>
              <button
                type="button"
                aria-label="بازیابی این نسخه"
                onClick={() => {
                  setRestoreTarget(row.id);
                  setConfirmText('');
                }}
                className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
              >
                <Icon name="settings_backup_restore" size={18} />
              </button>
            </div>
          )}
        />
      </section>

      {/*
        Restore overwrites live data, so it asks for the word to be typed rather
        than relying on a single click.
      */}
      {restoreTarget && (
        <Card className="flex flex-col gap-md border-error/40 p-lg">
          <h3 className="flex items-center gap-sm font-headline text-card-title text-error">
            <Icon name="warning" size={22} />
            بازیابی نسخه پشتیبان
          </h3>

          <p className="text-body-md leading-loose text-on-surface-variant">
            با بازیابی، همه داده‌های فعلی فروشگاه با محتوای این نسخه جایگزین می‌شود. این کار قابل
            بازگشت نیست. برای تایید، عبارت <strong className="text-error">بازیابی</strong> را وارد
            کنید.
          </p>

          <input
            value={confirmText}
            onChange={(event) => setConfirmText(event.target.value)}
            placeholder="بازیابی"
            className="h-12 max-w-xs rounded-lg border border-error/50 bg-surface-container-lowest px-md text-body-md text-on-surface placeholder:text-outline focus:border-error focus:outline-none focus:ring-2 focus:ring-error/30"
          />

          <div className="flex flex-wrap gap-md">
            {/*
              Still no restore endpoint, and deliberately not built alongside
              list/download/create: those are additive (a real archive now
              exists to list and fetch), but restore overwrites every table in
              the database from that archive — a destructive, whole-system
              operation with no worker or job-safety story yet. The
              confirmation gate stays as the right shape for when it is.
            */}
            <Button
              variant="danger"
              disabled
              hint="بازیابی نسخه پشتیبان هنوز در سرور پیاده‌سازی نشده است."
              className="px-xl"
            >
              بازیابی نسخه انتخاب‌شده
            </Button>
            <Button variant="ghost" onClick={() => setRestoreTarget(null)} className="px-lg">
              انصراف
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}
