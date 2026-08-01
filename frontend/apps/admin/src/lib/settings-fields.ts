import type { SettingsField } from '@/components/SettingsForm';
import type { SettingsSectionDto } from '@/lib/api/types';

/**
 * Every settings screen (141-144, 148-150, 154) declares its fields with a
 * design-derived default, then had no way to show what was actually saved —
 * `GET /settings/{section}` existed and was never called. This overlays the
 * real stored value on each field's default, so the default only shows for a
 * section nobody has saved yet.
 */
export function withSavedValues(fields: SettingsField[], settings: SettingsSectionDto): SettingsField[] {
  return fields.map((field) => {
    const saved = settings[field.name];
    if (saved === undefined) return field;

    return field.kind === 'switch'
      ? { ...field, checked: saved === 'true' }
      : { ...field, value: String(saved) };
  });
}
