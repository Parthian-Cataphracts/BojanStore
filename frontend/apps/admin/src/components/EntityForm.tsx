'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, Select, Textarea, cn } from '@bojan/ui';
import type { ResourceKey } from '@/lib/api/resources';
import { postJson } from '@/lib/submit';
import { FormLayout, FormSection } from './FormLayout';

export interface EntityField {
  name: string;
  label: string;
  kind?: 'text' | 'textarea' | 'select' | 'switch' | 'number' | 'date';
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
}

function Field({ field }: { field: EntityField }) {
  const [on, setOn] = useState(field.checked ?? false);

  if (field.kind === 'switch') {
    return (
      <div className="flex items-center justify-between gap-md">
        <span className="text-body-md text-on-surface">{field.label}</span>
        {/* The switch is a button, so it carries its value in a hidden input —
            without this the form submits without it. */}
        <input
          type="hidden"
          name={field.name}
          value={on ? (field.onValue ?? 'true') : (field.offValue ?? 'false')}
          readOnly
        />
        <button
          type="button"
          role="switch"
          aria-checked={on}
          aria-label={field.label}
          onClick={() => setOn((value) => !value)}
          className={cn(
            'relative h-6 w-11 shrink-0 rounded-full transition-colors',
            on ? 'bg-primary' : 'bg-outline-variant',
          )}
        >
          <span
            className={cn(
              'absolute top-1 h-4 w-4 rounded-full bg-surface-container-lowest transition-all',
              // Knob travels to the end when on — it used to be the other way
              // round, so the control showed the opposite of its own state.
              on ? 'start-6' : 'start-1',
            )}
          />
        </button>
      </div>
    );
  }

  if (field.kind === 'date') {
    return (
      <Input
        name={field.name}
        type="date"
        label={field.label}
        defaultValue={field.value ? field.value.slice(0, 10) : ''}
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
}: EntityFormProps) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
            {saved && (
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
