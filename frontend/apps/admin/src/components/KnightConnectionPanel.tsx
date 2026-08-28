'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Badge, Button, Card, Code, FormStatus, Icon, Input, formatDateTime } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { KnightStatusDto } from '@/lib/api/knight';

/**
 * اتصال به نایت — whether this shop is connected to the platform that delivers
 * its Features, and the form that connects it.
 *
 * The screen is deliberately two things in one: a state, and a way to change
 * it. Connecting a shop used to mean an environment variable and a redeploy,
 * which the person who owns the shop cannot do — so "connect us" became "send
 * your client secret to whoever can", which is the worst possible handling of a
 * credential that lets somebody install code here.
 *
 * **No secret is ever shown.** The client id is, because that is the half a
 * support conversation needs; the secret is write-only, and an empty box on a
 * connected shop means "leave the stored one alone".
 */
export function KnightConnectionPanel({ status }: { status: KnightStatusDto }) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);

    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      await postJson('/api/admin/knight', {
        baseUrl: String(data.get('baseUrl') ?? '').trim(),
        clientId: String(data.get('clientId') ?? '').trim(),
        clientSecret: String(data.get('clientSecret') ?? '').trim(),
        environment: String(data.get('environment') ?? '').trim(),
      });

      setSaved(true);
      // The handshake happens on the agent's next pass, so the page is asked
      // for again rather than the form claiming success on its behalf.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'اتصال به نایت انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  async function disconnect() {
    setDisconnecting(true);
    setError(null);

    try {
      await postJson('/api/admin/knight', undefined, { method: 'DELETE' });
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'قطع اتصال انجام نشد.');
    } finally {
      setDisconnecting(false);
    }
  }

  const state = !status.configured
    ? { tone: 'neutral' as const, label: 'وصل نشده', icon: 'link_off' }
    : status.connected
      ? { tone: 'success' as const, label: 'متصل', icon: 'sync_alt' }
      : { tone: 'warning' as const, label: 'در انتظار دست‌دادن', icon: 'pending' };

  return (
    <div className="flex flex-col gap-lg">
      <Card>
        <div className="flex flex-col gap-md p-lg">
          <div className="flex flex-wrap items-center gap-sm">
            <Icon name={state.icon} />
            <h2 className="text-title-medium">وضعیت اتصال</h2>
            <Badge tone={state.tone}>{state.label}</Badge>
          </div>

          {!status.configured ? (
            <p className="text-body-medium leading-relaxed text-on-surface-variant">
              این فروشگاه هنوز به نایت وصل نشده است. شناسه و کلیدی که نایت برای این فروشگاه صادر
              کرده را در فرم پایین وارد کنید؛ نیازی به راه‌اندازی دوباره‌ی سرور نیست.
            </p>
          ) : (
            <dl className="grid gap-sm text-body-medium sm:grid-cols-2">
              <Row label="نشانی نایت" value={<Code>{status.baseUrl}</Code>} />
              <Row label="شناسه فروشگاه" value={<Code>{status.clientId}</Code>} />
              <Row label="نام در نایت" value={status.storeName || '—'} />
              <Row label="وضعیت یکپارچگی" value={status.integrationStatus || '—'} />
              <Row label="آخرین دست‌دادن" value={when(status.lastHandshakeAt)} />
              <Row label="آخرین ضربان" value={when(status.lastHeartbeatAt)} />
              <Row label="آخرین کار" value={status.lastJob || '—'} />
              <Row label="زمان آخرین کار" value={when(status.lastJobAt)} />
            </dl>
          )}

          {status.lastError ? (
            <p className="text-caption leading-relaxed text-error">
              آخرین خطا ({when(status.lastErrorAt)}): {status.lastError}
            </p>
          ) : null}

          {status.configured ? (
            <div className="flex flex-wrap items-center gap-md">
              <Button type="button" variant="outline" loading={disconnecting} onClick={disconnect}>
                قطع اتصال
              </Button>
              <p className="text-caption leading-relaxed text-on-surface-variant">
                قطع اتصال فقط ارتباط با نایت را متوقف می‌کند. قابلیت‌هایی که تا الان تحویل گرفته‌اید
                نصب و فعال می‌مانند.
              </p>
            </div>
          ) : null}
        </div>
      </Card>

      <Card>
        <div className="p-lg">
          <h2 className="mb-md text-title-medium">قابلیت‌های تحویل‌گرفته</h2>

          {status.features.length === 0 ? (
            <p className="text-body-medium leading-relaxed text-on-surface-variant">
              هنوز چیزی تحویل داده نشده است. هر قابلیتی که مشتری این فروشگاه حق استفاده از آن را
              داشته باشد، خودکار از همین اتصال می‌رسد.
            </p>
          ) : (
            <ul className="flex flex-col gap-md">
              {status.features.map((feature) => (
                <li key={feature.slug} className="rounded-lg border border-outline-variant p-md">
                  <div className="flex flex-wrap items-center gap-sm">
                    <span className="text-body-large">{feature.slug}</span>
                    <Code>{feature.version}</Code>
                    <Badge tone={feature.enabled ? 'success' : 'neutral'}>
                      {feature.enabled ? 'فعال' : 'غیرفعال'}
                    </Badge>
                    {feature.architecture === 'external_service' ? (
                      <Badge tone="teal">سرویس بیرونی</Badge>
                    ) : null}
                    {/*
                      Presence, never value. A Feature whose configuration has
                      not arrived looks identical to a working one until the
                      first request it forwards is refused — this is the line
                      that tells the two apart.
                    */}
                    {feature.architecture === 'external_service' && !feature.hasServiceSecret ? (
                      <Badge tone="warning">کلید مشترک نرسیده</Badge>
                    ) : null}
                  </div>

                  {feature.routes.length > 0 ? (
                    <p className="mt-sm text-caption text-on-surface-variant">
                      مسیرهای این فروشگاه:{' '}
                      {/*
                        Separated, because three of these run together into one
                        unreadable string — which is what the first person to
                        look at this screen saw.
                      */}
                      {feature.routes.map((route, index) => (
                        <span key={route}>
                          {index > 0 ? '، ' : null}
                          <Code>{route}</Code>
                        </span>
                      ))}
                    </p>
                  ) : null}

                  {feature.mounts.length > 0 ? (
                    <p className="mt-sm text-caption text-on-surface-variant">
                      صفحه‌ها: {feature.mounts.map((mount) => `${mount.label} (${mount.slot})`).join('، ')}
                    </p>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </div>
      </Card>

      <form onSubmit={submit} className="flex flex-col gap-lg">
        <FormSection title={status.configured ? 'تغییر اعتبارنامه' : 'اتصال به نایت'} icon="key">
          <Input
            name="baseUrl"
            label="نشانی نایت"
            defaultValue={status.baseUrl}
            placeholder="https://knight.example.com"
            className="latin"
            dir="ltr"
          />
          <Input
            name="clientId"
            label="شناسه (client id)"
            defaultValue={status.clientId}
            className="latin"
            dir="ltr"
          />
          <Input
            name="clientSecret"
            label={status.configured ? 'کلید (ذخیره شده — برای تغییر پر کنید)' : 'کلید (client secret)'}
            type="password"
            autoComplete="new-password"
            className="latin"
            dir="ltr"
          />
          <Input
            name="environment"
            label="محیط"
            defaultValue={status.integrationStatus ? '' : 'Production'}
            placeholder="Production"
            className="latin"
            dir="ltr"
          />
          <p className="text-caption leading-relaxed text-on-surface-variant">
            کلید فقط یک بار — همان لحظه‌ای که نایت آن را صادر می‌کند — قابل خواندن است و اینجا هم
            دیگر نمایش داده نمی‌شود. محیط باید با همان چیزی که فروشگاه در نایت با آن ثبت شده یکی
            باشد، وگرنه دست‌دادن رد می‌شود.
          </p>
        </FormSection>

        <div className="flex flex-wrap items-center gap-md">
          <Button type="submit" loading={saving}>
            {status.configured ? 'ذخیره و اتصال دوباره' : 'اتصال'}
          </Button>

          <FormStatus
            ok={saved ? 'ثبت شد. اولین دست‌دادن تا چند لحظه دیگر انجام می‌شود.' : null}
            error={error}
          />
        </div>
      </form>
    </div>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex flex-wrap items-baseline gap-xs">
      <dt className="text-caption text-on-surface-variant">{label}:</dt>
      <dd className="text-body-medium">{value}</dd>
    </div>
  );
}

/**
 * A moment, in Jalali, or a dash.
 *
 * Never "just now" or "a few minutes ago": this screen is read when something
 * is wrong, and the exact time is what somebody correlates against a log.
 */
function when(value: string | null): string {
  return value ? formatDateTime(value) : '—';
}
