'use client';

import { useState, type FormEvent } from 'react';
import { Button, FormStatus, FormSwitch, Input, Select, Textarea } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';

export interface SettingsField {
  name: string;
  label: string;
  kind: 'text' | 'textarea' | 'switch' | 'select' | 'number';
  value?: string;
  checked?: boolean;
  suffix?: string;
  options?: string[];
  /** Render in the Latin face — API keys, URLs, colour hex, e-mail. */
  latin?: boolean;
  /**
   * The line under the field saying what it does.
   *
   * Worth having on a settings screen more than anywhere else: the owner is
   * looking at a figure with no context around it, and "۱۰۰۰۰۰۰" tells them
   * nothing about which page quotes it.
   */
  hint?: string;
  placeholder?: string;
}

export interface SettingsSection {
  title: string;
  icon: string;
  fields: SettingsField[];
}

/** Toggle styled as the pill switch the design uses across settings screens. */

/**
 * Shared settings editor. The settings screens (141-144, 148-150, 154) differ
 * only in their sections, so they are data rather than separate components.
 */
export function SettingsForm({
  section,
  sections,
}: {
  /** Settings group this form writes — screens 141-144, 148-150 and 154. */
  section: string;
  sections: SettingsSection[];
}) {
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const values: Record<string, string> = {};
    for (const [key, value] of data.entries()) {
      if (typeof value === 'string') values[key] = value;
    }

    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/settings', { section, values });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    /*
      Bounded rather than full-bleed. These are single-column forms of labelled
      fields, and on a 1440px screen minus the rail that is a 1150px-wide text
      box for a phone number — the label sits at one edge and the caret at the
      other. `max-w-4xl` is the same measure `TrustSealsForm` uses beside it on
      the store settings screen, so the two read as one column of forms.
    */
    <form onSubmit={submit} className="gap-lg flex max-w-4xl flex-col">
      {sections.map((section) => (
        <FormSection key={section.title} title={section.title} icon={section.icon}>
          {section.fields.map((field) => {
            if (field.kind === 'switch') {
              return (
                <FormSwitch
                  key={field.name}
                  name={field.name}
                  label={field.label}
                  defaultChecked={field.checked ?? false}
                />
              );
            }

            if (field.kind === 'textarea') {
              return (
                <Textarea
                  key={field.name}
                  name={field.name}
                  label={field.label}
                  defaultValue={field.value ?? ''}
                  rows={3}
                  {...(field.hint ? { hint: field.hint } : null)}
                  {...(field.placeholder ? { placeholder: field.placeholder } : null)}
                />
              );
            }

            if (field.kind === 'select') {
              return (
                <Select key={field.name} name={field.name} label={field.label}>
                  {(field.options ?? []).map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </Select>
              );
            }

            return (
              <Input
                key={field.name}
                name={field.name}
                label={field.label}
                defaultValue={field.value ?? ''}
                // Numbers in the Latin face and a numeric keypad on a phone: a
                // threshold typed with Persian digits is a threshold the API
                // parses as zero.
                {...(field.kind === 'number'
                  ? { type: 'number', inputMode: 'numeric' as const, min: 0, className: 'latin' }
                  : null)}
                {...(field.suffix ? { suffix: field.suffix } : null)}
                {...(field.latin ? { className: 'latin' } : null)}
                {...(field.hint ? { hint: field.hint } : null)}
                {...(field.placeholder ? { placeholder: field.placeholder } : null)}
              />
            );
          })}
        </FormSection>
      ))}

      <div className="gap-md border-outline-variant/40 pt-lg flex flex-wrap items-center border-t">
        <Button type="submit" size="lg" loading={saving} className="px-xl">
          ذخیره تنظیمات
        </Button>

        <FormStatus error={error} />
        <FormStatus ok={saved ? 'تنظیمات ذخیره شد.' : null} />
      </div>
    </form>
  );
}
