'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, FormStatus, Icon, Input, Select, Textarea, cn, toPersianDigits } from '@bojan/ui';
import { FormLayout, FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

/**
 * The ids are what `NotificationChannel` parses, not free labels.
 *
 * `in-app` used to be sent as `push`. Both parse — `Push` is a real member of
 * that enum — so nothing failed: the campaign was stored as a push campaign,
 * the in-app branch that writes the customer rows never ran, and the dispatcher
 * logged "no provider configured" for a channel the operator never chose. The
 * screen said the notification had been sent and no one ever received one.
 *
 * Push proper is not offered because nothing delivers it. A channel with no
 * provider behind it belongs in the roadmap, not in a picker.
 */
const channels = [
  { id: 'in-app', label: 'اعلان درون‌برنامه‌ای', icon: 'notifications', ready: true },
  { id: 'sms', label: 'پیامک', icon: 'sms', ready: true },
  // Live now that the dispatcher has an email branch and the shop has a mail
  // account to send it from. It reaches only the part of the audience that has
  // an address — the shop's sign-up path is a phone number, so many customers
  // have none — which the note under the tiles says plainly.
  { id: 'email', label: 'ایمیل', icon: 'mail', ready: true },
];

const audiences = [
  { value: 'all', label: 'همه مشتریان' },
  { value: 'عادی', label: 'گروه عادی' },
  { value: 'وفادار', label: 'گروه وفادار' },
  { value: 'سازمانی', label: 'گروه سازمانی' },
];

/** SMS parts are 70 chars in Persian (UCS-2), not 160. */
const SMS_PART = 70;

/** Screen 129 — Notification and SMS composer. */
export function NotificationComposer({ customerGroups }: { customerGroups: string[] }) {
  const [channel, setChannel] = useState('in-app');
  const [audience, setAudience] = useState('all');
  const [body, setBody] = useState('');
  const [confirming, setConfirming] = useState(false);
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const recipients =
    audience === 'all'
      ? customerGroups.length
      : customerGroups.filter((group) => group === audience).length;

  const parts = channel === 'sms' ? Math.max(1, Math.ceil(body.length / SMS_PART)) : 0;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const title = String(new FormData(event.currentTarget).get('title') ?? '');

    // Bulk sends cannot be recalled, so the second click is the real one.
    if (!confirming) {
      setConfirming(true);
      return;
    }

    setSending(true);
    setError(null);
    try {
      await postJson('/api/admin/notifications', { channel, audience, title, body });
      setSent(true);
      setConfirming(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال پیام انجام نشد.');
      setConfirming(false);
    } finally {
      setSending(false);
    }
  }

  if (sent) {
    return (
      <Card className="flex flex-col items-center gap-md p-xl text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="send" size={30} />
        </span>
        <h3 className="font-headline text-card-title text-primary">پیام در صف ارسال قرار گرفت</h3>
        <p className="tabular max-w-md text-body-md leading-loose text-on-surface-variant">
          برای {toPersianDigits(recipients)} مخاطب ارسال می‌شود. گزارش تحویل در بخش کمپین‌ها
          در دسترس خواهد بود.
        </p>
        <Button variant="outline" onClick={() => setSent(false)}>
          ارسال پیام دیگر
        </Button>
      </Card>
    );
  }

  return (
    <form onSubmit={submit} noValidate>
      <FormLayout
        aside={
          <FormSection title="خلاصه ارسال" icon="summarize">
            <dl className="flex flex-col gap-sm text-body-md">
              <div className="flex items-center justify-between">
                <dt className="text-on-surface-variant">مخاطبان</dt>
                <dd className="tabular font-semibold text-primary">
                  {toPersianDigits(recipients)} نفر
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-on-surface-variant">کانال</dt>
                <dd className="text-on-surface">
                  {channels.find((item) => item.id === channel)?.label}
                </dd>
              </div>
              {channel === 'sms' && (
                <div className="flex items-center justify-between">
                  <dt className="text-on-surface-variant">تعداد بخش پیامک</dt>
                  <dd className="tabular text-on-surface">{toPersianDigits(parts)}</dd>
                </div>
              )}
            </dl>

            {channel === 'sms' && (
              <p className="flex items-start gap-xs rounded-lg bg-secondary-fixed/40 px-md py-sm text-caption leading-relaxed text-on-secondary-fixed-variant">
                <Icon name="info" size={16} className="mt-px shrink-0" />
                هر بخش پیامک فارسی {toPersianDigits(SMS_PART)} نویسه است و جداگانه محاسبه می‌شود.
              </p>
            )}
          </FormSection>
        }
        actions={
          <>
            <FormStatus error={error} />

            <Button
              type="submit"
              size="lg"
              loading={sending}
              variant={confirming ? 'danger' : 'primary'}
              className="px-xl"
            >
              {confirming ? `تایید ارسال به ${toPersianDigits(recipients)} مخاطب` : 'ارسال پیام'}
            </Button>

            {confirming && (
              <Button
                type="button"
                variant="ghost"
                size="lg"
                onClick={() => setConfirming(false)}
                className="px-xl"
              >
                انصراف
              </Button>
            )}
          </>
        }
      >
        <FormSection title="کانال ارسال" icon="campaign">
          <div className="grid gap-md sm:grid-cols-3">
            {channels.map((item) => (
              <button
                key={item.id}
                type="button"
                disabled={!item.ready}
                aria-pressed={channel === item.id}
                title={item.ready ? undefined : 'هنوز سرویس‌دهنده‌ای برای ارسال ایمیل تنظیم نشده است.'}
                onClick={() => {
                  setChannel(item.id);
                  setConfirming(false);
                }}
                className={cn(
                  'flex items-center gap-sm rounded-lg border p-md text-start transition-colors',
                  !item.ready && 'cursor-not-allowed opacity-50',
                  channel === item.id
                    ? 'border-primary bg-soft-mint/30 text-primary'
                    : 'border-outline-variant text-on-surface hover:bg-surface-container-low',
                )}
              >
                <Icon name={item.icon} size={22} />
                <span className="text-body-md font-medium">{item.label}</span>
                {!item.ready && <span className="text-caption text-outline">(به‌زودی)</span>}
              </button>
            ))}
          </div>

          {channel === 'email' && (
            <p className="text-caption leading-relaxed text-on-surface-variant">
              ایمیل فقط به مشتریانی می‌رسد که نشانی ایمیل ثبت کرده‌اند. ثبت‌نام فروشگاه با شماره
              موبایل انجام می‌شود، پس بخشی از مخاطبان این کمپین را دریافت نمی‌کنند.
            </p>
          )}

          {channel === 'sms' && (
            <p className="text-caption leading-relaxed text-on-surface-variant">
              پیامک گروهی از خط تبلیغاتی ارسال می‌شود و به شماره‌هایی که پیامک تبلیغاتی را مسدود
              کرده‌اند نمی‌رسد. هزینه‌ی هر پیامک از اعتبار حساب <span className="latin">sms.ir</span>{' '}
              شما کم می‌شود.
            </p>
          )}
        </FormSection>

        <FormSection title="مخاطبان" icon="group">
          <Select
            label="گروه هدف"
            value={audience}
            onChange={(event) => {
              setAudience(event.target.value);
              setConfirming(false);
            }}
          >
            {audiences.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </Select>
        </FormSection>

        <FormSection title="متن پیام" icon="edit_note">
          {channel !== 'sms' && <Input name="title" label="عنوان" placeholder="مثال: جشنواره بهاره" />}

          <Textarea
            name="body"
            label="متن"
            rows={5}
            value={body}
            onChange={(event) => {
              setBody(event.target.value);
              setConfirming(false);
            }}
            hint={
              channel === 'sms'
                ? `${toPersianDigits(body.length)} نویسه — ${toPersianDigits(parts)} بخش`
                : undefined
            }
          />
        </FormSection>
      </FormLayout>
    </form>
  );
}
