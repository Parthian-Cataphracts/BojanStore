'use client';

import { useState, type FormEvent } from 'react';
import { Badge, Button, Card, Checkbox, Code, Icon, Input, Sheet, formatDate } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';
import type { ApiKeyDto } from '@/lib/api/types';

/**
 * The plaintext secret for a key just created — the backend never stores it
 * and will not return it again, so this is the only copy that will ever
 * exist. Kept separate from `ApiKeyDto` because every other key in the list
 * genuinely has no full value to reveal, only the prefix it was created with.
 */
interface CreatedKey {
  id: string;
  secret: string;
}

const events = [
  'order.created',
  'order.paid',
  'order.shipped',
  'order.cancelled',
  'product.stock_low',
  'customer.registered',
];

/** Screen 155 — API keys and webhooks. */
export function ApiSettingsPanel({ keys: initialKeys }: { keys: ApiKeyDto[] }) {
  const [keys, setKeys] = useState(initialKeys);
  const [created, setCreated] = useState<CreatedKey | null>(null);
  const [copied, setCopied] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState<string | null>(null);
  const [webhookSaved, setWebhookSaved] = useState(false);
  const [creating, setCreating] = useState(false);
  const [newKeyLabel, setNewKeyLabel] = useState('');

  /**
   * Revoking is immediate and cannot be undone — a key that might be leaking
   * should stop working the moment the operator says so, not after a dialog.
   */
  async function revoke(key: ApiKeyDto) {
    const previous = keys;
    setKeys((current) => current.filter((item) => item.id !== key.id));
    setError(null);

    try {
      await postJson('/api/admin/api-keys', { id: key.id, revoked: true });
    } catch (cause) {
      setKeys(previous);
      setError(cause instanceof Error ? cause.message : 'ابطال کلید انجام نشد.');
    }
  }

  async function copy(value: string, id: string) {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(id);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      // Clipboard blocked — the value is on screen to read.
    }
  }

  /**
   * The backend returns the plaintext secret once, on creation, and never
   * again — it stores only a hash. That response is the only place this
   * screen can ever show the full key, so it is held in state until the
   * operator closes the sheet rather than derived from the list afterward.
   */
  async function createKey() {
    const label = newKeyLabel.trim();
    if (!label) return;

    setPending('create');
    setError(null);
    try {
      const result = await postJson<{ id: string; label: string; prefix: string; scope: string; key: string }>(
        '/api/admin/api-keys',
        { label, scope: 'read' },
      );
      const now = new Date().toISOString();

      setKeys((current) => [
        ...current,
        {
          id: result.id,
          label: result.label,
          prefix: result.prefix,
          scope: result.scope,
          revoked: false,
          createdAt: now,
          lastUsedAt: null,
        },
      ]);
      setCreated({ id: result.id, secret: result.key });
      setCreating(false);
      setNewKeyLabel('');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ساخت کلید انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  async function saveWebhook(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);

    setPending('webhook');
    setError(null);
    setWebhookSaved(false);
    try {
      await postJson('/api/admin/settings', {
        section: 'webhooks',
        values: {
          url: String(data.get('webhookUrl') ?? ''),
          secret: String(data.get('secret') ?? ''),
          // Only the events the operator actually ticked.
          events: events.filter((name) => data.get(name) !== null).join(','),
        },
      });
      setWebhookSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره وبهوک انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  async function sendTestEvent() {
    setPending('test');
    setError(null);
    try {
      await postJson('/api/admin/settings', {
        section: 'webhooks',
        values: { test: 'order.created' },
      });
      setWebhookSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال رویداد آزمایشی انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <section className="flex flex-col gap-md">
        <div className="flex flex-wrap items-center justify-between gap-md">
          <h3 className="font-headline text-card-title text-primary">کلیدهای API</h3>
          <Button size="sm" icon="add" onClick={() => setCreating(true)}>
            ساخت کلید جدید
          </Button>
        </div>

        {created && (
          <Card className="flex flex-col gap-sm border-primary/40 bg-soft-mint/40 p-md">
            <span className="flex items-center gap-xs text-label-md font-semibold text-primary">
              <Icon name="key" size={18} />
              کلید جدید ساخته شد — همین حالا کپی کنید
            </span>
            <div className="flex items-center gap-xs">
              <Code className="flex-1 break-all">{created.secret}</Code>
              <button
                type="button"
                aria-label="کپی کلید"
                onClick={() => copy(created.secret, created.id)}
                className="shrink-0 rounded p-xs text-on-surface-variant transition-colors hover:text-primary"
              >
                <Icon name={copied === created.id ? 'check' : 'content_copy'} size={18} />
              </button>
            </div>
            <p className="text-caption text-on-surface-variant">
              این مقدار دیگر نمایش داده نمی‌شود. در صورت گم شدن باید کلید را ابطال و دوباره بسازید.
            </p>
            <Button variant="ghost" size="sm" className="self-start" onClick={() => setCreated(null)}>
              بستن
            </Button>
          </Card>
        )}

        <DataTable
          rows={keys}
          rowKey={(row) => row.id}
          emptyIcon="key"
          emptyTitle="کلیدی ساخته نشده"
          emptyDescription="برای اتصال سامانه‌های بیرونی یک کلید API بسازید."
          columns={[
            { key: 'label', header: 'عنوان', cell: (row) => row.label },
            {
              key: 'key',
              header: 'کلید',
              // The backend stores only a hash, so the prefix is all a key
              // ever shows again after the creation banner above closes.
              cell: (row) => <Code>{row.prefix}••••••••••••••••</Code>,
            },
            {
              key: 'scope',
              header: 'سطح دسترسی',
              cell: (row) => (
                <Badge tone={row.scope === 'read' ? 'neutral' : 'warning'}>
                  {row.scope === 'read' ? 'فقط خواندن' : 'خواندن و نوشتن'}
                </Badge>
              ),
            },
            {
              key: 'lastUsed',
              header: 'آخرین استفاده',
              cell: (row) => (
                <span className="tabular">
                  {row.lastUsedAt ? formatDate(row.lastUsedAt, 'long') : '—'}
                </span>
              ),
            },
          ]}
          actions={(row) => (
            <button
              type="button"
              aria-label={`ابطال ${row.label}`}
              onClick={() => revoke(row)}
              className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
            >
              <Icon name="block" size={18} />
            </button>
          )}
        />
      </section>

      <form onSubmit={saveWebhook}>
      <FormSection
        title="وبهوک"
        icon="webhook"
        description="رویدادهای انتخاب‌شده به این آدرس با متد POST ارسال می‌شوند."
      >
        <Input
          name="webhookUrl"
          label="آدرس وبهوک"
          className="latin"
          placeholder="https://example.com/webhooks/bojan"
        />

        <Input
          name="secret"
          label="کلید امضا"
          className="latin"
          hint="برای بررسی صحت درخواست با HMAC-SHA256 استفاده می‌شود."
          placeholder="whsec_••••••••"
        />

        <fieldset className="flex flex-col gap-sm">
          <legend className="mb-sm text-label-md font-medium text-on-surface-variant">
            رویدادها
          </legend>
          <div className="grid gap-sm sm:grid-cols-2">
            {events.map((event) => (
              <Checkbox
                key={event}
                name={event}
                defaultChecked={event.startsWith('order.')}
                label={<Code>{event}</Code>}
              />
            ))}
          </div>
        </fieldset>

        <div className="flex flex-wrap items-center gap-md">
          <Button type="submit" loading={pending === 'webhook'} className="px-xl">
            ذخیره وبهوک
          </Button>
          <Button
            type="button"
            variant="outline"
            icon="send"
            loading={pending === 'test'}
            onClick={sendTestEvent}
            className="px-lg"
          >
            ارسال رویداد آزمایشی
          </Button>

          {webhookSaved && !error && (
            <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
              <Icon name="check_circle" size={16} />
              ذخیره شد.
            </span>
          )}

          {error && (
            <span role="alert" className="flex items-center gap-xs text-caption text-error">
              <Icon name="error" size={16} />
              {error}
            </span>
          )}
        </div>
      </FormSection>
      </form>

      {/* The design gives this button no destination, so the key is named
          in the same sheet the panel uses for its other confirmations. */}
      <Sheet
        open={creating}
        onClose={() => setCreating(false)}
        title="ساخت کلید API"
        footer={
          <div className="flex flex-col gap-sm sm:flex-row-reverse">
            <Button
              size="lg"
              loading={pending === 'create'}
              disabled={newKeyLabel.trim().length === 0}
              onClick={createKey}
              className="sm:px-xl"
            >
              ساخت کلید
            </Button>
            <Button
              variant="ghost"
              size="lg"
              onClick={() => setCreating(false)}
              className="sm:px-xl"
            >
              انصراف
            </Button>
          </div>
        }
      >
        <Input
          label="عنوان کلید"
          hint="برای اینکه بعداً بدانید این کلید کجا استفاده می‌شود."
          placeholder="مثلاً اپلیکیشن موبایل"
          value={newKeyLabel}
          onChange={(event) => setNewKeyLabel(event.target.value)}
          maxLength={60}
        />

        {error && (
          <p
            role="alert"
            className="mt-md flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
          >
            <Icon name="error" size={20} className="mt-px shrink-0" />
            {error}
          </p>
        )}
      </Sheet>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="warning" size={20} className="mt-px shrink-0 text-secondary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          کلید API فقط یک بار هنگام ساخت به‌طور کامل نمایش داده می‌شود. در صورت گم شدن باید ابطال و
          دوباره ساخته شود.
        </p>
      </Card>
    </div>
  );
}
