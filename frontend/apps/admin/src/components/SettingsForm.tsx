'use client';

import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, Select, Textarea, cn } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';

export interface SettingsField {
  name: string;
  label: string;
  kind: 'text' | 'textarea' | 'switch' | 'select';
  value?: string;
  checked?: boolean;
  suffix?: string;
  options?: string[];
  /** Render in the Latin face — API keys, URLs, colour hex, e-mail. */
  latin?: boolean;
}

export interface SettingsSection {
  title: string;
  icon: string;
  fields: SettingsField[];
}

/** Toggle styled as the pill switch the design uses across settings screens. */
function Switch({ field }: { field: SettingsField }) {
  const [on, setOn] = useState(field.checked ?? false);

  return (
    <div className="flex items-center justify-between gap-md">
      <span className="text-body-md text-on-surface">{field.label}</span>

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
            // Logical offset so the knob travels the right way in RTL. It
            // moves to the end when on — the two were the wrong way round.
            on ? 'start-6' : 'start-1',
          )}
        />
      </button>
    </div>
  );
}

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
    <form onSubmit={submit} className="flex flex-col gap-lg">
      {sections.map((section) => (
        <FormSection key={section.title} title={section.title} icon={section.icon}>
          {section.fields.map((field) => {
            if (field.kind === 'switch') return <Switch key={field.name} field={field} />;

            if (field.kind === 'textarea') {
              return (
                <Textarea
                  key={field.name}
                  name={field.name}
                  label={field.label}
                  defaultValue={field.value ?? ''}
                  rows={3}
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
                {...(field.suffix ? { suffix: field.suffix } : null)}
                {...(field.latin ? { className: 'latin' } : null)}
              />
            );
          })}
        </FormSection>
      ))}

      <div className="flex flex-wrap items-center gap-md border-t border-outline-variant/40 pt-lg">
        <Button type="submit" size="lg" loading={saving} className="px-xl">
          ذخیره تنظیمات
        </Button>

        {error && (
          <span role="alert" className="flex items-center gap-xs text-caption text-error">
            <Icon name="error" size={16} />
            {error}
          </span>
        )}
        {saved && (
          <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
            <Icon name="check_circle" size={16} />
            تنظیمات ذخیره شد.
          </span>
        )}
      </div>
    </form>
  );
}
