'use client';

import { useState } from 'react';
import { Badge, Button, Card, Icon, Select, formatDateTime, toPersianDigits } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

interface BackupRow {
  id: string;
  at: string;
  sizeMb: number;
  kind: string;
}

/**
 * Screen 156 — Backup and restore.
 *
 * `POST /backups` queues a job; there is no listing endpoint yet, and no
 * worker that turns a queued job into a downloadable file — see
 * `AdminOperationsService.QueueBackupAsync`. Showing invented rows here would
 * claim backups exist that do not, so the table stays empty until that lands.
 */
export function BackupPanel() {
  const [creating, setCreating] = useState(false);
  const [restoreTarget, setRestoreTarget] = useState<string | null>(null);
  const [confirmText, setConfirmText] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function createBackup() {
    setCreating(true);
    setError(null);
    try {
      await postJson('/api/admin/backups', { kind: 'full', confirm: true });
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
        <Button variant="outline" size="lg" icon="upload_file" className="px-xl">
          بارگذاری فایل پشتیبان
        </Button>
      </div>

      {error && (
        <p role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </p>
      )}


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
          rows={[] as BackupRow[]}
          rowKey={(row) => row.id}
          emptyIcon="backup"
          emptyTitle="نسخه پشتیبانی وجود ندارد"
          emptyDescription="اولین نسخه پشتیبان را بسازید."
          columns={[
            {
              key: 'at',
              header: 'تاریخ',
              cell: (row) => <span className="tabular">{formatDateTime(row.at)}</span>,
            },
            { key: 'kind', header: 'نوع', cell: (row) => <Badge tone="neutral">{row.kind}</Badge> },
            {
              key: 'size',
              header: 'حجم',
              cell: (row) => (
                <span className="tabular">{toPersianDigits(row.sizeMb)} مگابایت</span>
              ),
            },
          ]}
          actions={(row) => (
            <div className="flex items-center gap-xs">
              <button
                type="button"
                aria-label="دانلود نسخه پشتیبان"
                className="rounded p-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
              >
                <Icon name="download" size={18} />
              </button>
              <button
                type="button"
                aria-label="بازیابی این نسخه"
                onClick={() => {
                  setRestoreTarget(row.id);
                  setConfirmText('');
                }}
                className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
              >
                <Icon name="restore" size={18} />
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
            <Button variant="danger" disabled={confirmText.trim() !== 'بازیابی'} className="px-xl">
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
