'use client';

import { useId, useState } from 'react';
import { cn } from '../lib/cn';

export interface SwitchProps {
  /** The label read beside the track. Also the control's accessible name. */
  label: string;
  checked: boolean;
  onChange: (next: boolean) => void;
  /** The line of explanation drawn under the row, where a switch needs one. */
  hint?: string;
  disabled?: boolean;
  /**
   * Emits a hidden input under this name, for a switch inside an uncontrolled
   * form that is read back with `FormData`.
   */
  name?: string;
  /**
   * What that hidden input carries when on and off.
   *
   * Defaults to the strings every settings screen posts. The catalogue forms
   * need their own pair, because a switch there stands for a status the API
   * spells out — `published`/`draft` rather than a boolean.
   */
  onValue?: string;
  offValue?: string;
  className?: string;
}

/**
 * The panel's on/off control.
 *
 * There were five of these, byte-for-byte identical, copied into every settings
 * screen that needed one — so the next person to change how a toggle looks
 * would have changed one of them and left the panel disagreeing with itself.
 * That is the whole reason this is here.
 *
 * None of the five could be seen when focused. They were `<button>`s with no
 * focus style at all, which for a keyboard user means tabbing through a
 * settings screen with no idea which control is live — and a switch is exactly
 * the control where guessing is expensive, because activating it changes
 * something. The ring matches `Button`'s, so focus looks the same everywhere.
 *
 * `role="switch"` with `aria-checked` rather than a checkbox: a screen reader
 * announces "on"/"off" instead of "checked", which is what these mean.
 */
export function Switch({
  label,
  checked,
  onChange,
  hint,
  disabled = false,
  name,
  onValue = 'true',
  offValue = 'false',
  className,
}: SwitchProps) {
  const hintId = useId();

  return (
    <div className={cn('flex flex-col gap-xs', className)}>
      <div className="flex items-center justify-between gap-md">
        <span className="text-body-md text-on-surface">{label}</span>

        {/* Uncontrolled forms read this back; controlled callers ignore it. */}
        {name && <input type="hidden" name={name} value={checked ? onValue : offValue} readOnly />}

        <button
          type="button"
          role="switch"
          aria-checked={checked}
          aria-label={label}
          aria-describedby={hint ? hintId : undefined}
          disabled={disabled}
          onClick={() => onChange(!checked)}
          className={cn(
            'relative h-6 w-11 shrink-0 rounded-full transition-colors',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-background',
            disabled && 'cursor-not-allowed opacity-50',
            checked ? 'bg-primary' : 'bg-outline-variant',
          )}
        >
          <span
            className={cn(
              'absolute top-1 h-4 w-4 rounded-full bg-surface-container-lowest transition-all',
              // Logical offsets, so the knob travels the right way in RTL: it
              // moves to the end when on. The two were once the wrong way round.
              checked ? 'start-6' : 'start-1',
            )}
          />
        </button>
      </div>

      {hint && (
        <p id={hintId} className="text-caption leading-relaxed text-on-surface-variant">
          {hint}
        </p>
      )}
    </div>
  );
}

export interface FormSwitchProps extends Omit<SwitchProps, 'checked' | 'onChange'> {
  name: string;
  defaultChecked?: boolean;
  /** Told about changes, for a form that reveals fields when a switch is on. */
  onChange?: (next: boolean) => void;
}

/**
 * A {@link Switch} that keeps its own state, for a form read back with
 * `FormData` rather than held in React.
 *
 * Most of the settings screens are that shape — an uncontrolled form with a
 * submit handler — and each of them had grown its own copy of both the switch
 * and the `useState` behind it. The hidden input is what makes the value
 * readable at submit, and it is emitted here so no caller has to remember it.
 */
export function FormSwitch({ name, defaultChecked = false, onChange, ...rest }: FormSwitchProps) {
  const [checked, setChecked] = useState(defaultChecked);

  return (
    <Switch
      {...rest}
      name={name}
      checked={checked}
      onChange={(next) => {
        setChecked(next);
        onChange?.(next);
      }}
    />
  );
}
