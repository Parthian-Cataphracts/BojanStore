'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Card, FormStatus, Icon, Input, JalaliDateInput, Select } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { AdminCustomerDto } from '@/lib/api/types';

/**
 * Everything on a customer's record, editable — and the account removable.
 *
 * The panel could suspend a customer and set their password, and nothing else.
 * A name typed wrongly at registration stayed wrong for good; an account created
 * by mistake stayed for good too. Both are ordinary things a shop needs to do,
 * and neither had a control anywhere.
 *
 * The wallet balance and the loyalty points are not here. They are ledgers —
 * they move by a transaction that records why — and a field that set them
 * directly would be a way to credit an account with no such record.
 */
export function CustomerEditPanel({
  customer,
  groups,
}: {
  customer: AdminCustomerDto;
  /** The groups already in use, so an operator picks rather than retypes. */
  groups: string[];
}) {
  const router = useRouter();

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);

    setSaving(true);
    setSaved(null);
    setError(null);
    try {
      await postJson('/api/admin/customer-save', {
        id: customer.id,
        code: String(data.get('code') ?? '').trim(),
        firstName: String(data.get('firstName') ?? ''),
        lastName: String(data.get('lastName') ?? ''),
        phone: String(data.get('phone') ?? '').trim(),
        email: String(data.get('email') ?? ''),
        city: String(data.get('city') ?? ''),
        nationalId: String(data.get('nationalId') ?? ''),
        group: String(data.get('group') ?? ''),
        birthDate: String(data.get('birthDate') ?? ''),
      });
      setSaved('اطلاعات مشتری ذخیره شد.');
      // The header above this card is rendered on the server, so the page is
      // refetched rather than the state patched here.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره اطلاعات مشتری انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  async function remove() {
    setDeleting(true);
    setError(null);
    try {
      await postJson('/api/admin/customer-delete', { customerId: customer.id });
      router.push('/customers');
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'حذف مشتری انجام نشد.');
      setDeleting(false);
    }
  }

  return (
    <Card className="flex flex-col gap-lg p-lg">
      <div className="flex items-start gap-sm">
        <Icon name="badge" size={22} className="mt-2xs shrink-0 text-primary" />
        <div className="flex min-w-0 flex-col gap-2xs">
          <h2 className="font-headline text-card-title text-on-surface">اطلاعات مشتری</h2>
          <p className="text-caption leading-relaxed text-on-surface-variant">
            شناسه‌ی مشتری همان چیزی است که روی سفارش و در تماس تلفنی به آن ارجاع می‌دهید — کوتاه و
            بی‌خطر برای گفتن، برخلاف شماره‌ی موبایل.
          </p>
        </div>
      </div>

      <form onSubmit={save} className="flex flex-col gap-md">
        <div className="grid gap-md md:grid-cols-2">
          <Input
            name="code"
            label="شناسه مشتری"
            defaultValue={customer.code ?? ''}
            className="latin"
            dir="ltr"
            maxLength={32}
            hint="اگر از سامانه‌ی قبلی شناسه دارید، همان را بگذارید."
          />

          <Select name="group" label="گروه مشتری" defaultValue={customer.group}>
            {/* The group the customer is in, plus every group in use — free
                text on the API, so this is a shortlist and not a fixed set. */}
            {[...new Set([customer.group, ...groups])].filter(Boolean).map((group) => (
              <option key={group} value={group}>
                {group}
              </option>
            ))}
          </Select>

          <Input name="firstName" label="نام" defaultValue={customer.firstName ?? ''} maxLength={100} />
          <Input name="lastName" label="نام خانوادگی" defaultValue={customer.lastName ?? ''} maxLength={100} />

          <Input
            name="phone"
            label="شماره موبایل"
            defaultValue={customer.phone}
            className="latin"
            dir="ltr"
            inputMode="numeric"
            maxLength={11}
            hint="شماره‌ی ورود مشتری. با تغییرش، نشست‌های باز او بسته می‌شود."
          />

          <Input
            name="email"
            type="email"
            label="ایمیل"
            defaultValue={customer.email ?? ''}
            className="latin"
            dir="ltr"
            maxLength={200}
          />

          <Input name="city" label="شهر" defaultValue={customer.city ?? ''} maxLength={100} />

          <Input
            name="nationalId"
            label="کد ملی"
            defaultValue={customer.nationalId ?? ''}
            className="latin"
            dir="ltr"
            inputMode="numeric"
            maxLength={10}
          />

          <JalaliDateInput
            name="birthDate"
            label="تاریخ تولد"
            defaultValue={customer.birthDate ?? ''}
            yearsBack={100}
            yearsAhead={0}
          />
        </div>

        <Button type="submit" icon="save" loading={saving} className="self-start">
          ذخیره اطلاعات
        </Button>
      </form>

      <div className="flex flex-col gap-sm border-t border-outline-variant pt-lg">
        {customer.deletable === false ? (
          /*
            Said rather than hidden. This account has orders, a wallet movement
            or a support ticket behind it, and those name their customer with a
            restricting key — an invoice whose customer has been deleted is not
            a tidier invoice. Suspension is what this account gets, and it is on
            the card below.
          */
          <p className="flex items-start gap-xs text-caption leading-relaxed text-on-surface-variant">
            <Icon name="info" size={18} className="mt-px shrink-0 text-primary" />
            این حساب سابقه‌ی خرید یا پشتیبانی دارد و حذف نمی‌شود — سفارش‌ها و فاکتورها باید نام
            مشتری‌شان را نگه دارند. برای بستن دسترسی از کارت «دسترسی حساب» استفاده کنید.
          </p>
        ) : confirmingDelete ? (
          <div className="flex flex-col gap-sm">
            <p role="status" className="text-caption leading-relaxed text-error">
              این حساب و اطلاعات شخصی‌اش — آدرس‌ها، علاقه‌مندی‌ها، اعلان‌ها و تاریخچه‌ی جستجو —
              کامل پاک می‌شوند و برنمی‌گردند.
            </p>
            <div className="flex items-center gap-sm">
              <Button type="button" variant="danger" size="sm" loading={deleting} onClick={remove}>
                بله، حذف کن
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => setConfirmingDelete(false)}>
                انصراف
              </Button>
            </div>
          </div>
        ) : (
          <Button
            type="button"
            variant="outline"
            size="sm"
            icon="delete"
            className="self-start"
            onClick={() => setConfirmingDelete(true)}
          >
            حذف کامل حساب
          </Button>
        )}
      </div>

      <FormStatus ok={saved} error={error} />
    </Card>
  );
}
