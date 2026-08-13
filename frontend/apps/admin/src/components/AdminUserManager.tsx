'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import {
  Badge,
  Button,
  Card,
  Code,
  FormStatus,
  Icon,
  Input,
  Select,
  Sheet,
  Switch,
  formatDate,
  toPersianDigits,
} from '@bojan/ui';
import { DataTable, type Column } from '@/components/DataTable';
import { postJson } from '@/lib/submit';
import type { AdminUserDto } from '@/lib/api/types';

/**
 * Screen 145 — appointing operators and managing the ones there are.
 *
 * The screen listed them and nothing more. There was no endpoint that created
 * an operator, so the "افزودن کاربر" button was disabled and said so, and a shop
 * that needed a second person in the panel had exactly one way to get one:
 * hand over the owner's own password. Everything below exists to replace that.
 *
 * Every rule here is enforced on the API too, and has to be — this component
 * decides what is offered, not what is allowed, and a request that never came
 * through the panel gets none of it. What the two halves are for is different:
 * the API refuses, and this explains, so an owner reads why a control is off
 * instead of pressing it and being told no.
 */

/** The four the API accepts, in the order they appear on the permission grid. */
const roles = [
  { value: 'owner', label: 'مالک', hint: 'دسترسی کامل، شامل تنظیمات و همین صفحه' },
  { value: 'product', label: 'مدیر محصول', hint: 'کاتالوگ، موجودی و محتوا' },
  { value: 'sales', label: 'فروش سازمانی', hint: 'درخواست‌های سازمانی، پیش‌فاکتور و کوپن' },
  { value: 'support', label: 'پشتیبانی', hint: 'تیکت‌ها، گفت‌وگو و وضعیت سفارش' },
] as const;

const roleLabel = (role: string) => roles.find((entry) => entry.value === role)?.label ?? role;

/**
 * The floor the API applies to an operator's password.
 *
 * Twelve, not the customer's eight: these accounts open the shop's books. Stated
 * here so the field says the rule before the server does — the number itself is
 * `AdminOperationsService.MinimumPasswordLength`, and the two are checked
 * against each other by nothing but this comment, which is why the field only
 * hints and never refuses on its own.
 */
const MIN_PASSWORD_LENGTH = 12;

type Editing =
  | { mode: 'create' }
  | { mode: 'edit'; user: AdminUserDto };

export function AdminUserManager({
  users,
  currentUserId,
  pagination,
}: {
  users: AdminUserDto[];
  /** The signed-in operator, so their own row can say why half of it is off. */
  currentUserId: string;
  pagination: {
    page: number;
    pageSize: number;
    total: number;
    params: Record<string, string | string[] | undefined>;
    basePath: string;
  };
}) {
  const router = useRouter();

  const [editing, setEditing] = useState<Editing | null>(null);
  const [pending, setPending] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [clearingTwoFactor, setClearingTwoFactor] = useState(false);

  function open(next: Editing) {
    setEditing(next);
    setSaved(null);
    setError(null);
    setClearingTwoFactor(false);
  }

  const isSelf = editing?.mode === 'edit' && editing.user.id === currentUserId;

  async function saveDetails(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing) return;

    const form = event.currentTarget;
    const data = new FormData(form);
    const creating = editing.mode === 'create';

    setPending('details');
    setSaved(null);
    setError(null);

    try {
      await postJson('/api/admin/admin-users', {
        ...(creating ? null : { id: editing.user.id }),
        name: String(data.get('name') ?? '').trim(),
        email: String(data.get('email') ?? '').trim(),
        // Empty means "no number", which is a value: an operator who signs in
        // by email alone is ordinary, and clearing a number that was typed
        // wrongly has to be possible.
        phone: String(data.get('phone') ?? '').trim(),
        role: String(data.get('role') ?? ''),
        // Absent on the caller's own row, where the switch is not drawn — so
        // the API keeps whatever is stored rather than being told `false` by a
        // control that was never there.
        ...(data.has('isActive') ? { isActive: data.get('isActive') === 'true' } : null),
        ...(creating ? { password: String(data.get('password') ?? '') } : null),
      });

      setEditing(null);
      // The table is rendered on the server and paged there, so the page is
      // refetched rather than patched here — one answer about who the operators
      // are, not two that can drift apart.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره اپراتور انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  async function setPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (editing?.mode !== 'edit') return;

    const form = event.currentTarget;
    const password = String(new FormData(form).get('password') ?? '');

    setPending('password');
    setSaved(null);
    setError(null);

    try {
      await postJson('/api/admin/admin-user-password', { id: editing.user.id, password });
      setSaved('گذرواژه ثبت شد. نشست‌های باز این اپراتور بسته شد و در اولین ورود باید گذرواژه را عوض کند.');
      form.reset();
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت گذرواژه انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  async function clearTwoFactor() {
    if (editing?.mode !== 'edit') return;

    setPending('two-factor');
    setSaved(null);
    setError(null);

    try {
      await postJson('/api/admin/admin-user-two-factor', { id: editing.user.id });
      setSaved('ورود دو مرحله‌ای این اپراتور خاموش شد. با گذرواژه وارد می‌شود و می‌تواند دوباره فعالش کند.');
      setClearingTwoFactor(false);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'خاموش کردن ورود دو مرحله‌ای انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  const columns: Column<AdminUserDto>[] = [
    {
      key: 'name',
      header: 'نام',
      cell: (row) => (
        <span className="flex flex-wrap items-center gap-xs">
          {row.name}
          {row.id === currentUserId && <Badge tone="neutral">شما</Badge>}
          {row.mustChangePassword && <Badge tone="warning">در انتظار تغییر گذرواژه</Badge>}
        </span>
      ),
    },
    {
      key: 'email',
      header: 'ایمیل',
      cell: (row) => <Code className="text-caption">{row.email}</Code>,
    },
    { key: 'role', header: 'نقش', cell: (row) => <Badge tone="neutral">{roleLabel(row.role)}</Badge> },
    {
      key: 'active',
      header: 'آخرین ورود',
      cell: (row) => (
        <span className="tabular">{row.lastActiveAt ? formatDate(row.lastActiveAt) : 'هرگز'}</span>
      ),
    },
    {
      key: 'status',
      header: 'وضعیت',
      cell: (row) =>
        row.status === 'active' ? <Badge tone="mint">فعال</Badge> : <Badge tone="error">معلق</Badge>,
    },
  ];

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-md">
        <p className="text-caption text-on-surface-variant">
          اپراتور حذف نمی‌شود؛ تعلیق می‌شود — نام او روی ردیف‌های ممیزی و سفارش‌هایی که رسیدگی کرده
          باقی می‌ماند.
        </p>
        <Button size="sm" icon="person_add" onClick={() => open({ mode: 'create' })}>
          افزودن اپراتور
        </Button>
      </div>

      <DataTable
        columns={columns}
        rows={users}
        rowKey={(row) => row.id}
        pagination={pagination}
        emptyTitle="کاربری یافت نشد"
        emptyIcon="admin_panel_settings"
        actions={(row) => (
          <Button
            variant="outline"
            size="sm"
            icon="manage_accounts"
            onClick={() => open({ mode: 'edit', user: row })}
          >
            مدیریت
          </Button>
        )}
      />

      <Sheet
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing?.mode === 'create' ? 'افزودن اپراتور' : 'مدیریت اپراتور'}
      >
        {editing && (
          /*
            Keyed on whose row this is, so React rebuilds the fields instead of
            reusing them. Every input below is uncontrolled — `defaultValue`
            applies at mount and never again — and the sheet renders the same
            shape for everybody, so without this the tree was reused verbatim:
            managing an operator and then pressing "افزودن اپراتور" opened a
            create form already carrying that operator's name, address and
            number, and an owner who filled in only a password would have
            appointed a second account under somebody else's identity.
          */
          <div key={editing.mode === 'create' ? 'new' : editing.user.id} className="flex flex-col gap-lg">
            <form onSubmit={saveDetails} className="flex flex-col gap-md">
              <Input
                name="name"
                label="نام"
                required
                maxLength={200}
                defaultValue={editing.mode === 'edit' ? editing.user.name : ''}
              />

              <Input
                name="email"
                type="email"
                label="ایمیل"
                required
                maxLength={200}
                className="latin"
                dir="ltr"
                autoComplete="off"
                hint="اپراتور با همین ایمیل وارد پنل می‌شود."
                defaultValue={editing.mode === 'edit' ? editing.user.email : ''}
              />

              <Input
                name="phone"
                label="شماره موبایل (اختیاری)"
                inputMode="numeric"
                maxLength={11}
                className="latin"
                dir="ltr"
                autoComplete="off"
                hint="اگر پر شود، اپراتور می‌تواند به‌جای ایمیل با این شماره هم وارد شود."
                defaultValue={editing.mode === 'edit' ? (editing.user.phone ?? '') : ''}
              />

              <Select
                name="role"
                label="نقش"
                required
                defaultValue={editing.mode === 'edit' ? editing.user.role : 'support'}
                hint={
                  isSelf
                    ? // Stepping down is allowed once a successor exists — what the
                      // API refuses is the panel ending up with no owner at all.
                      'اگر نقش خودتان را پایین بیاورید، دیگر به همین صفحه دسترسی ندارید. تا وقتی مالک فعال دیگری نباشد، این تغییر پذیرفته نمی‌شود.'
                    : roles.find(
                        (entry) =>
                          entry.value === (editing.mode === 'edit' ? editing.user.role : 'support'),
                      )?.hint
                }
              >
                {roles.map((role) => (
                  <option key={role.value} value={role.value}>
                    {role.label}
                  </option>
                ))}
              </Select>

              {editing.mode === 'create' && (
                <Input
                  name="password"
                  type="password"
                  label="گذرواژه اولیه"
                  required
                  minLength={MIN_PASSWORD_LENGTH}
                  className="latin"
                  dir="ltr"
                  autoComplete="new-password"
                  hint={`دست‌کم ${toPersianDigits(MIN_PASSWORD_LENGTH)} نویسه. خودتان آن را به اپراتور بدهید؛ در اولین ورود مجبور است عوضش کند و بعد از آن هیچ‌جا نمایش داده نمی‌شود.`}
                />
              )}

              {/* Not on the caller's own row: suspending yourself signs you out
                  of a panel only somebody else can let you back into. */}
              {!isSelf && (
                <ActiveSwitch
                  defaultActive={editing.mode === 'edit' ? editing.user.status === 'active' : true}
                />
              )}

              <Button type="submit" icon="save" loading={pending === 'details'} className="self-start">
                {editing.mode === 'create' ? 'ساخت اپراتور' : 'ذخیره تغییرات'}
              </Button>
            </form>

            {editing.mode === 'edit' && !isSelf && (
              <>
                <form
                  onSubmit={setPassword}
                  className="flex flex-col gap-sm border-t border-outline-variant pt-lg"
                >
                  <Input
                    name="password"
                    type="password"
                    label="تعیین گذرواژه"
                    required
                    minLength={MIN_PASSWORD_LENGTH}
                    className="latin"
                    dir="ltr"
                    autoComplete="new-password"
                    hint="برای اپراتوری که گذرواژه‌اش را فراموش کرده. همه‌ی نشست‌های بازش بسته می‌شود و در اولین ورود باید گذرواژه‌ی خودش را انتخاب کند."
                  />
                  <Button
                    type="submit"
                    variant="outline"
                    icon="key"
                    loading={pending === 'password'}
                    className="self-start"
                  >
                    ثبت گذرواژه
                  </Button>
                </form>

                <div className="flex flex-col gap-sm border-t border-outline-variant pt-lg">
                  <span className="flex items-center gap-xs text-body-md text-on-surface">
                    <Icon
                      name={editing.user.twoFactorEnabled ? 'verified_user' : 'lock_open'}
                      size={18}
                      className={editing.user.twoFactorEnabled ? 'text-primary' : 'text-on-surface-variant'}
                    />
                    {editing.user.twoFactorEnabled
                      ? 'ورود دو مرحله‌ای فعال است'
                      : 'ورود دو مرحله‌ای فعال نیست'}
                  </span>

                  {editing.user.twoFactorEnabled &&
                    (clearingTwoFactor ? (
                      <div className="flex flex-col gap-sm">
                        <p role="status" className="text-caption leading-relaxed text-on-surface-variant">
                          بعد از این، ورود این اپراتور فقط با گذرواژه انجام می‌شود تا خودش دوباره
                          ورود دو مرحله‌ای را راه بیندازد. نشست‌های بازش هم بسته می‌شود.
                        </p>
                        <div className="flex items-center gap-sm">
                          <Button
                            type="button"
                            variant="danger"
                            size="sm"
                            loading={pending === 'two-factor'}
                            onClick={clearTwoFactor}
                          >
                            بله، خاموش کن
                          </Button>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() => setClearingTwoFactor(false)}
                          >
                            انصراف
                          </Button>
                        </div>
                      </div>
                    ) : (
                      // Asked twice: this removes the factor standing between a
                      // stolen password and the panel, for an operator who
                      // cannot be reached to confirm it was them who asked.
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        icon="lock_reset"
                        className="self-start"
                        onClick={() => setClearingTwoFactor(true)}
                      >
                        خاموش کردن ورود دو مرحله‌ای
                      </Button>
                    ))}
                </div>
              </>
            )}

            {editing.mode === 'edit' && isSelf && (
              <Card className="flex items-start gap-sm p-md">
                <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
                <p className="text-caption leading-relaxed text-on-surface-variant">
                  گذرواژه و ورود دو مرحله‌ای حساب خودتان از «تنظیمات ← تغییر گذرواژه» و «تنظیمات ←
                  ورود دو مرحله‌ای» عوض می‌شود؛ آنجا گذرواژه‌ی فعلی و کد فعلی پرسیده می‌شود، و
                  همین است که نمی‌گذارد یک نشست دزدیده‌شده حساب پشتش را بردارد.
                </p>
              </Card>
            )}

            <FormStatus ok={saved} error={error} />
          </div>
        )}
      </Sheet>
    </>
  );
}

/**
 * The suspension control, as a switch the surrounding form reads back.
 *
 * Its own component only because `Switch` is controlled and the form around it
 * is not — the hidden input it emits is what makes the value readable at
 * submit, and the state has to live somewhere for the track to move.
 */
function ActiveSwitch({ defaultActive }: { defaultActive: boolean }) {
  const [active, setActive] = useState(defaultActive);

  return (
    <Switch
      name="isActive"
      label="حساب فعال است"
      checked={active}
      onChange={setActive}
      onValue="true"
      offValue="false"
      hint="تعلیق، اپراتور را همان لحظه از پنل بیرون می‌کند و ورودش را می‌بندد — بدون اینکه چیزی از کارهایی که کرده پاک شود."
    />
  );
}
