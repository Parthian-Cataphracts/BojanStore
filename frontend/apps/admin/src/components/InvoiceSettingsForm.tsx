'use client';

import { useRef, useState, type FormEvent } from 'react';
import { Button, FormStatus, Input, Textarea } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
// From `lib/invoice-fields`, not `lib/api/invoice-settings`: that module
// imports the API client, which reaches `next/headers` — server-only, and a
// build error the moment a `'use client'` file pulls it in.
import { INVOICE_KEYS, INVOICE_SECTION } from '@/lib/invoice-fields';
import type { InvoiceSettingsDto } from '@/lib/api/types';

/**
 * The invoice's configurable content.
 *
 * Not `SettingsForm`: that one renders a list of text fields and posts them,
 * which the seller block and the closing note fit but the stamp does not — a
 * file has to be uploaded before there is a value to save, and the owner has to
 * see what they uploaded before saving it.
 */
export function InvoiceSettingsForm({ settings }: { settings: InvoiceSettingsDto }) {
  // The stamp is the one field held in state: it is set by an upload rather
  // than typed, and it is submitted as whatever the upload last returned.
  const [stampUrl, setStampUrl] = useState<string | null>(settings.stampUrl ?? null);
  const [uploading, setUploading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const filePicker = useRef<HTMLInputElement>(null);

  async function upload(file: File | undefined) {
    if (!file) return;

    setError(null);
    setUploading(true);
    try {
      const body = new FormData();
      body.append('file', file);

      const response = await fetch('/api/upload/invoices', { method: 'POST', body });
      const result = (await response.json().catch(() => null)) as
        | { url?: string; error?: string }
        | null;

      if (!response.ok || !result?.url) {
        throw new Error(result?.error ?? 'بارگذاری مهر انجام نشد.');
      }

      setStampUrl(result.url);
      // Uploading is not saving. The file is stored, but the invoice keeps
      // printing the old stamp until this form is submitted — so the button
      // below stays the thing that changes the document.
      setSaved(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'بارگذاری مهر انجام نشد.');
    } finally {
      setUploading(false);
      // Cleared so choosing the same file twice fires `change` again.
      if (filePicker.current) filePicker.current.value = '';
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);

    const values: Record<string, string> = {};
    for (const [key, value] of data.entries()) {
      if (typeof value === 'string') values[key] = value.trim();
    }
    // Empty is meaningful here rather than absent: it is how an owner removes a
    // stamp they no longer want printed.
    values[INVOICE_KEYS.stampUrl] = stampUrl ?? '';

    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/settings', { section: INVOICE_SECTION, values });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-lg">
      <FormSection title="مشخصات فروشنده" icon="storefront">
        <p className="text-caption text-on-surface-variant">
          این بخش در کادر «مشخصات فروشنده» فاکتور چاپ می‌شود. فیلدهای خالی روی فاکتور نمایش داده
          نمی‌شوند.
        </p>
        <Input
          name={INVOICE_KEYS.sellerName}
          label="نام فروشنده"
          defaultValue={settings.seller.name}
          required
        />
        <Input
          name={INVOICE_KEYS.sellerWebsite}
          label="وب‌سایت"
          defaultValue={settings.seller.website}
          className="latin"
        />
        <Input
          name={INVOICE_KEYS.sellerEmail}
          label="ایمیل پشتیبانی"
          type="email"
          defaultValue={settings.seller.email}
          className="latin"
        />
        <Input
          name={INVOICE_KEYS.sellerPhone}
          label="تلفن"
          defaultValue={settings.seller.phone}
          className="latin"
        />
        <Textarea
          name={INVOICE_KEYS.sellerAddress}
          label="نشانی"
          defaultValue={settings.seller.address}
          rows={2}
        />
        <Input
          name={INVOICE_KEYS.sellerNationalId}
          label="شناسه ملی"
          defaultValue={settings.seller.nationalId}
          className="latin"
        />
        <Input
          name={INVOICE_KEYS.sellerEconomicCode}
          label="کد اقتصادی"
          defaultValue={settings.seller.economicCode}
          className="latin"
        />
      </FormSection>

      <FormSection title="متن‌های فاکتور" icon="edit_note">
        <Input
          name={INVOICE_KEYS.thanksNote}
          label="جمله‌ی تشکر"
          defaultValue={settings.thanksNote}
        />
        <Textarea
          name={INVOICE_KEYS.terms}
          label="شرایط و مقررات"
          defaultValue={settings.terms}
          rows={4}
        />
        <Textarea
          name={INVOICE_KEYS.footerNote}
          label="یادداشت پاورقی"
          defaultValue={settings.footerNote}
          rows={2}
        />
      </FormSection>

      <FormSection title="مهر الکترونیک" icon="approval">
        <p className="text-caption leading-relaxed text-on-surface-variant">
          تصویر مهر روی فاکتور چاپ می‌شود و جای کادر خالی «محل مهر و امضای فروشگاه» را می‌گیرد. برای
          چاپ تمیز، تصویر PNG با پس‌زمینه‌ی شفاف بهترین نتیجه را می‌دهد. تا وقتی مهری بارگذاری نشده،
          همان کادر خالی چاپ می‌شود تا روی کاغذ دستی مهر بخورد.
        </p>

        <div className="flex flex-wrap items-center gap-md">
          <div className="grid h-32 w-44 place-items-center border border-outline-variant bg-surface-container-lowest p-sm">
            {stampUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={stampUrl}
                alt="پیش‌نمایش مهر فروشگاه"
                className="max-h-full max-w-full object-contain"
              />
            ) : (
              <span className="text-caption text-outline">مهری بارگذاری نشده</span>
            )}
          </div>

          <div className="flex flex-col gap-sm">
            <input
              ref={filePicker}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
              onChange={(event) => upload(event.target.files?.[0])}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              icon="upload"
              disabled={uploading}
              onClick={() => filePicker.current?.click()}
            >
              {uploading ? 'در حال بارگذاری…' : stampUrl ? 'تغییر مهر' : 'بارگذاری مهر'}
            </Button>

            {stampUrl && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                icon="delete"
                onClick={() => {
                  setStampUrl(null);
                  setSaved(false);
                }}
              >
                حذف مهر
              </Button>
            )}
          </div>
        </div>
      </FormSection>

      <div className="flex flex-wrap items-center gap-md">
        <Button type="submit" disabled={saving || uploading}>
          {saving ? 'در حال ذخیره…' : 'ذخیره تنظیمات'}
        </Button>

        {/* Was its own pair, a size larger than every other screen's and with
            the failure announced politely rather than as an alert — so a save
            that did not happen waited its turn behind whatever a screen reader
            was already saying. */}
        <FormStatus
          ok={saved ? 'تنظیمات ذخیره شد. فاکتورهای بعدی با همین اطلاعات چاپ می‌شوند.' : null}
          error={error}
        />
      </div>
    </form>
  );
}
