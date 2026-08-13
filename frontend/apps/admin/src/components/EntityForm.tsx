'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, FormStatus, FormSwitch, Input, JalaliDateInput, Select, Textarea } from '@bojan/ui';
import type { ResourceKey } from '@/lib/api/resources';
import { postJson } from '@/lib/submit';
import { FormLayout, FormSection } from './FormLayout';
import { ImageField } from './ImageField';

export interface EntityField {
  name: string;
  label: string;
  kind?: 'text' | 'textarea' | 'select' | 'switch' | 'number' | 'date' | 'image';
  /**
   * Which upload folder an `image` field writes to — products, brands,
   * collections, content or campaigns.
   *
   * Required for that kind and meaningless for the rest: the API checks a
   * stored image URL against the folder its entity belongs to, so a cover
   * uploaded to the wrong one is refused exactly as a typed address is.
   */
  folder?: string;
  value?: string;
  checked?: boolean;
  /** What a `switch` field submits when on/off — defaults to `'true'`/`'false'`. */
  onValue?: string;
  offValue?: string;
  options?: { value: string; label: string }[];
  hint?: string;
  latin?: boolean;
  required?: boolean;
}

export interface EntityFormProps {
  /** Which allow-listed resource this form writes to. */
  resource: ResourceKey;
  /** Id of the record being edited; absent when creating. */
  entityId?: string;
  /**
   * Values the operator never edits but the write still needs — e.g. which
   * content `kind` a `/content/{type}/new` screen creates. Sent alongside
   * whatever the visible fields collect, and cannot be overridden by them.
   */
  fixedFields?: Record<string, string>;
  /** Main column sections. */
  sections: { title: string; icon: string; fields: EntityField[] }[];
  /** Sidebar sections — publishing, media, SEO. */
  asideSections?: { title: string; icon: string; fields: EntityField[] }[];
  submitLabel: string;
  /**
   * Removing this record, for the entities whose API supports it.
   *
   * Archiving rather than deleting, which is what the services underneath
   * actually do: `status: 'archived'` soft-deletes, so the row stays and stops
   * being served. Every one of these screens had a create and an edit and no
   * way at all to get rid of anything — an article typed in by mistake stayed
   * on the site permanently, and the only route out was the database.
   *
   * Absent means the resource has no archive path and no button is drawn,
   * rather than one that would be refused.
   */
  archive?: {
    /** What is being removed, as the confirmation sentence names it — «مقاله», «کمپین». */
    noun: string;
    /** Where to go once it is gone. */
    returnTo: string;
  };
}

function Field({ field }: { field: EntityField }) {
  if (field.kind === 'image') {
    return (
      <ImageField
        name={field.name}
        label={field.label}
        folder={field.folder ?? 'content'}
        defaultValue={field.value ?? ''}
        {...(field.hint ? { hint: field.hint } : null)}
      />
    );
  }

  if (field.kind === 'switch') {
    return (
      <FormSwitch
        name={field.name}
        label={field.label}
        defaultChecked={field.checked ?? false}
        {...(field.onValue ? { onValue: field.onValue } : null)}
        {...(field.offValue ? { offValue: field.offValue } : null)}
        {...(field.hint ? { hint: field.hint } : null)}
      />
    );
  }

  if (field.kind === 'date') {
    return (
      <JalaliDateInput
        name={field.name}
        label={field.label}
        // A Persian calendar, not the browser's Gregorian one. `type="date"`
        // posts the right string and draws the wrong months — a campaign that
        // runs through Mordad is scheduled by somebody reading August, and the
        // rest of this panel has spoken Jalali since it was written.
        defaultValue={field.value ? field.value.slice(0, 10) : ''}
        // Campaigns and discounts are scheduled ahead, so the year list has to
        // reach forward as well as back.
        yearsBack={5}
        yearsAhead={5}
        {...(field.hint ? { hint: field.hint } : null)}
      />
    );
  }

  if (field.kind === 'textarea') {
    return (
      <Textarea
        name={field.name}
        label={field.label}
        defaultValue={field.value ?? ''}
        rows={4}
        {...(field.hint ? { hint: field.hint } : null)}
      />
    );
  }

  if (field.kind === 'select') {
    return (
      <Select name={field.name} label={field.label} defaultValue={field.value ?? ''}>
        <option value="">انتخاب کنید</option>
        {(field.options ?? []).map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </Select>
    );
  }

  return (
    <Input
      name={field.name}
      label={field.label}
      defaultValue={field.value ?? ''}
      {...(field.kind === 'number' ? { inputMode: 'numeric' as const } : null)}
      {...(field.hint ? { hint: field.hint } : null)}
      {...(field.latin ? { className: 'latin' } : null)}
      {...(field.required ? { required: true } : null)}
    />
  );
}

/**
 * Generic create/edit form for the simple admin entities — categories, brands,
 * collections, articles, banners, FAQ. They differ only in their fields, so
 * they are data rather than separate components.
 *
 * The form posts every named field to `/api/admin/{resource}`, which keeps only
 * the fields that resource declares. Naming a field the resource does not list
 * is therefore harmless: it is dropped rather than saved.
 */
export function EntityForm({
  resource,
  entityId,
  fixedFields,
  sections,
  asideSections,
  submitLabel,
  archive,
}: EntityFormProps) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmingArchive, setConfirmingArchive] = useState(false);
  const [archiving, setArchiving] = useState(false);

  async function runArchive() {
    if (!entityId || !archive) return;

    setArchiving(true);
    setError(null);
    try {
      // The same write endpoint the save uses. `archived` is not offered in the
      // status picker on purpose — removing something is a decision, not a
      // value somebody scrolls past on the way to «منتشر شده».
      await postJson(`/api/admin/${resource}`, { id: entityId, status: 'archived' });
      router.push(archive.returnTo);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'حذف انجام نشد.');
      setArchiving(false);
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const allFields = [...sections, ...(asideSections ?? [])].flatMap((section) => section.fields);
    const dateFields = new Set(allFields.filter((field) => field.kind === 'date').map((field) => field.name));

    // A `switch` with no `onValue`/`offValue` of its own carries a real
    // boolean — `SaveBrandRequest.Featured`, `SaveCategoryRequest.ShowInMenu`
    // — and its hidden input's value is the literal string "true"/"false".
    // .NET's JSON binder does not accept a JSON string where a `bool?` is
    // declared; it threw, unhandled, turning every save of that field into a
    // 500 rather than a stored value. A switch that *does* set one of those
    // (published/draft, running/ended) is a string-typed field on purpose —
    // status, not a flag — and is left as the string it already is.
    const booleanFields = new Set(
      allFields
        .filter((field) => field.kind === 'switch' && field.onValue === undefined && field.offValue === undefined)
        .map((field) => field.name),
    );

    // `kind: 'number'` fields are declared `int?`/`long?` on the API side and
    // hit the same binder, the same way, for the same reason.
    const numberFields = new Set(allFields.filter((field) => field.kind === 'number').map((field) => field.name));

    const data = new FormData(event.currentTarget);
    const payload: Record<string, unknown> = entityId ? { id: entityId } : {};
    for (const [key, value] of data.entries()) {
      if (typeof value !== 'string') continue;
      // A native date input gives yyyy-mm-dd; the API binds these fields as
      // DateTimeOffset and rejects that shape, so convert or drop it — leaving
      // it blank must not overwrite the saved date with an empty string.
      if (dateFields.has(key)) {
        if (value) payload[key] = new Date(value).toISOString();
        continue;
      }
      if (booleanFields.has(key)) {
        payload[key] = value === 'true';
        continue;
      }
      if (numberFields.has(key)) {
        if (value.trim() !== '' && Number.isFinite(Number(value))) payload[key] = Number(value);
        continue;
      }
      payload[key] = value;
    }
    Object.assign(payload, fixedFields);

    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson(`/api/admin/${resource}`, payload);
      setSaved(true);
      // Server components on this route read the record that just changed.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره اطلاعات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <FormLayout
        aside={
          asideSections && asideSections.length > 0 ? (
            <>
              {asideSections.map((section) => (
                <FormSection key={section.title} title={section.title} icon={section.icon}>
                  {section.fields.map((field) => (
                    <Field key={field.name} field={field} />
                  ))}
                </FormSection>
              ))}
            </>
          ) : undefined
        }
        actions={
          <>
            <Button type="submit" size="lg" loading={saving} className="px-xl">
              {submitLabel}
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="lg"
              className="px-xl"
              onClick={() => router.back()}
            >
              انصراف
            </Button>

            {archive && entityId && (
              confirmingArchive ? (
                <div className="flex flex-wrap items-center gap-sm">
                  <span className="text-caption text-on-surface-variant">
                    این {archive.noun} از سایت برداشته می‌شود. سابقه‌اش می‌ماند و بازگرداندنش
                    از پشتیبانی ممکن است.
                  </span>
                  <Button
                    type="button"
                    variant="danger"
                    size="lg"
                    loading={archiving}
                    className="px-xl"
                    onClick={runArchive}
                  >
                    بله، حذف کن
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="lg"
                    onClick={() => setConfirmingArchive(false)}
                  >
                    انصراف
                  </Button>
                </div>
              ) : (
                // Asked twice, because the thing it removes is one an operator
                // may have spent an afternoon writing.
                <Button
                  type="button"
                  variant="outline"
                  size="lg"
                  icon="delete"
                  className="px-xl"
                  onClick={() => setConfirmingArchive(true)}
                >
                  حذف {archive.noun}
                </Button>
              )
            )}

            <FormStatus ok={saved ? 'ذخیره شد.' : null} />
            <FormStatus error={error} />
          </>
        }
      >
        {sections.map((section) => (
          <FormSection key={section.title} title={section.title} icon={section.icon}>
            {section.fields.map((field) => (
              <Field key={field.name} field={field} />
            ))}
          </FormSection>
        ))}
      </FormLayout>
    </form>
  );
}
