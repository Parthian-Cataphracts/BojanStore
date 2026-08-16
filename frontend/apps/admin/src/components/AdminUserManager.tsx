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
} from '@bojan/ui';
import { DataTable, type Column } from '@/components/DataTable';
import { postJson } from '@/lib/submit';
import type { AdminUserDto } from '@/lib/api/types';

/**
 * Screen 145 — appointing operators and managing the ones there are.
 *
 * Appointing, not creating. An operator is somebody who already has an account
 * on the shop and has been granted the panel, so this screen takes an identity
 * — the phone or address they registered with — and never a password. The
 * password is theirs, it is the one they already use to buy things, and nobody
 * here has ever seen it.
 *
 * That is also what fixed the owner: the installer used to mint an operator row
 * with an e-mail and no phone number, and a shopper here *is* a phone number,
 * so the person who owned the shop could not sign in to it.
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
 * The ten sections an operator can be narrowed to.
 *
 * Ticking none is not «no access» — it is «whatever the role already allows»,
 * which is what an operator who has never been narrowed should get. The filter
 * on the API reads it the same way, and the sheet says so under the checklist
 * rather than leaving an owner to guess from an empty box.
 */
const sections = [
  { key: 'orders', label: 'سفارش‌ها' },
  { key: 'products', label: 'محصولات' },
  { key: 'inventory', label: 'موجودی' },
  { key: 'customers', label: 'مشتریان' },
  { key: 'business', label: 'درخواست‌های سازمانی' },
  { key: 'content', label: 'محتوا' },
  { key: 'campaigns', label: 'کمپین‌ها' },
  { key: 'support', label: 'پشتیبانی' },
  { key: 'reports', label: 'گزارش‌ها' },
  { key: 'settings', label: 'تنظیمات' },
] as const;

type Editing = { mode: 'create' } | { mode: 'edit'; user: AdminUserDto };

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
        // On create the whole identity is one field: whichever of their phone
        // or address the owner has to hand. The API looks the account up and
        // copies the name and contact from it, so none of that is asked here.
        ...(creating ? { identity: String(data.get('identity') ?? '').trim() } : null),
        ...(creating ? null : { name: String(data.get('name') ?? '').trim() }),
        role: String(data.get('role') ?? ''),
        // Every box, ticked or not, so clearing the last one is distinguishable
        // from not having sent the checklist at all.
        sections: sections
          .filter((section) => data.get(`section:${section.key}`) === 'on')
          .map((section) => section.key),
        // Absent on the caller's own row, where the switch is not drawn — so
        // the API keeps whatever is stored rather than being told `false` by a
        // control that was never there.
        ...(data.has('isActive') ? { isActive: data.get('isActive') === 'true' } : null),
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

  async function clearTwoFactor() {
    if (editing?.mode !== 'edit') return;

    setPending('two-factor');
    setSaved(null);
    setError(null);

    try {
      await postJson('/api/admin/admin-user-two-factor', { id: editing.user.id });
      setSaved(
        'ورود دو مرحله‌ای این اپراتور خاموش شد. با گذرواژه وارد می‌شود و می‌تواند دوباره فعالش کند.',
      );
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
        <span className="gap-xs flex flex-wrap items-center">
          {row.name}
          {row.id === currentUserId && <Badge tone="neutral">شما</Badge>}
        </span>
      ),
    },
    {
      key: 'email',
      header: 'ایمیل',
      cell: (row) => <Code className="text-caption">{row.email}</Code>,
    },
    {
      key: 'role',
      header: 'نقش',
      cell: (row) => <Badge tone="neutral">{roleLabel(row.role)}</Badge>,
    },
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
        row.status === 'active' ? (
          <Badge tone="mint">فعال</Badge>
        ) : (
          <Badge tone="error">معلق</Badge>
        ),
    },
  ];

  return (
    <>
      <div className="gap-md flex flex-wrap items-center justify-between">
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
          <div
            key={editing.mode === 'create' ? 'new' : editing.user.id}
            className="gap-lg flex flex-col"
          >
            <form onSubmit={saveDetails} className="gap-md flex flex-col">
              {editing.mode === 'create' ? (
                <Input
                  name="identity"
                  label="شماره موبایل یا ایمیل کاربر"
                  required
                  maxLength={200}
                  className="latin"
                  dir="ltr"
                  autoComplete="off"
                  hint="حساب باید از قبل در سایت ساخته شده باشد. نام و اطلاعات تماس از همان حساب برداشته می‌شود، و اپراتور با همان رمزی وارد پنل می‌شود که با آن خرید می‌کند."
                />
              ) : (
                <>
                  <Input
                    name="name"
                    label="نام نمایشی"
                    required
                    maxLength={200}
                    defaultValue={editing.user.name}
                    hint="فقط عنوانی است که در همین صفحه دیده می‌شود."
                  />

                  {/* Read-only: these belong to the shop account, and changing
                      them here would be editing somebody's own record from the
                      panel that merely grants them access to it. */}
                  <Card className="gap-xs p-md flex flex-col">
                    <span className="text-caption text-on-surface-variant">حساب کاربری</span>
                    <span className="latin text-body-md text-on-surface" dir="ltr">
                      {editing.user.phone ?? '—'}
                    </span>
                    <span className="latin text-caption text-on-surface-variant" dir="ltr">
                      {editing.user.email}
                    </span>
                    <span className="text-caption text-on-surface-variant leading-relaxed">
                      شماره، ایمیل و رمز از همین حساب خوانده می‌شوند و از سمت خودِ کاربر عوض
                      می‌شوند.
                    </span>
                  </Card>
                </>
              )}

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

              <SectionChecklist
                granted={editing.mode === 'edit' ? (editing.user.sections ?? []) : []}
              />

              {/* Not on the caller's own row: suspending yourself signs you out
                  of a panel only somebody else can let you back into. */}
              {!isSelf && (
                <ActiveSwitch
                  defaultActive={editing.mode === 'edit' ? editing.user.status === 'active' : true}
                />
              )}

              <Button
                type="submit"
                icon="save"
                loading={pending === 'details'}
                className="self-start"
              >
                {editing.mode === 'create' ? 'ساخت اپراتور' : 'ذخیره تغییرات'}
              </Button>
            </form>

            {editing.mode === 'edit' && !isSelf && (
              <>
                {/* No «set a password» form. An operator who is locked out
                    resets it from the storefront, where the mail goes to them
                    and to nobody else — an owner who could set it could then
                    sign in as them. */}
                <div className="gap-sm border-outline-variant pt-lg flex flex-col border-t">
                  <span className="gap-xs text-body-md text-on-surface flex items-center">
                    <Icon
                      name={editing.user.twoFactorEnabled ? 'verified_user' : 'lock_open'}
                      size={18}
                      className={
                        editing.user.twoFactorEnabled ? 'text-primary' : 'text-on-surface-variant'
                      }
                    />
                    {editing.user.twoFactorEnabled
                      ? 'ورود دو مرحله‌ای فعال است'
                      : 'ورود دو مرحله‌ای فعال نیست'}
                  </span>

                  {editing.user.twoFactorEnabled &&
                    (clearingTwoFactor ? (
                      <div className="gap-sm flex flex-col">
                        <p
                          role="status"
                          className="text-caption text-on-surface-variant leading-relaxed"
                        >
                          بعد از این، ورود این اپراتور فقط با گذرواژه انجام می‌شود تا خودش دوباره
                          ورود دو مرحله‌ای را راه بیندازد. نشست‌های بازش هم بسته می‌شود.
                        </p>
                        <div className="gap-sm flex items-center">
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
              <Card className="gap-sm p-md flex items-start">
                <Icon name="info" size={20} className="text-primary mt-px shrink-0" />
                <p className="text-caption text-on-surface-variant leading-relaxed">
                  گذرواژه و ورود دو مرحله‌ای حساب خودتان از «تنظیمات ← تغییر گذرواژه» و «تنظیمات ←
                  ورود دو مرحله‌ای» عوض می‌شود؛ آنجا گذرواژه‌ی فعلی و کد فعلی پرسیده می‌شود، و همین
                  است که نمی‌گذارد یک نشست دزدیده‌شده حساب پشتش را بردارد. همان گذرواژه در سایت هم
                  کار می‌کند، چون یک حساب بیشتر ندارید.
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

/**
 * The section checklist, as plain checkboxes the surrounding form reads back.
 *
 * Checkboxes rather than the role grid this replaces: a permission belongs to a
 * person now, so the question is «what may Nima open», not «what may the sales
 * role open» — and the grid could not tell one salesperson from another.
 */
function SectionChecklist({ granted }: { granted: readonly string[] }) {
  return (
    <fieldset className="gap-sm border-outline-variant p-md flex flex-col rounded-xl border">
      <legend className="px-xs text-label-md text-on-surface">دسترسی به بخش‌ها</legend>

      <div className="gap-xs grid sm:grid-cols-2">
        {sections.map((section) => (
          <label
            key={section.key}
            className="gap-sm px-xs hover:bg-surface-container-low flex cursor-pointer items-center rounded-lg py-2 transition-colors"
          >
            <input
              type="checkbox"
              name={`section:${section.key}`}
              defaultChecked={granted.includes(section.key)}
              className="border-outline-variant text-primary focus:ring-primary/30 h-5 w-5 shrink-0 rounded focus:ring-2"
            />
            <span className="text-body-md text-on-surface">{section.label}</span>
          </label>
        ))}
      </div>

      <p className="text-caption text-on-surface-variant leading-relaxed">
        اگر هیچ‌کدام تیک نخورد، اپراتور به همان چیزی دسترسی دارد که نقشش اجازه می‌دهد — یعنی محدود
        نشده، نه اینکه از همه‌جا بسته شده باشد. مالک هرگز محدود نمی‌شود.
      </p>
    </fieldset>
  );
}
