import { forwardRef, useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

/**
 * The look every control in the shop shares. Exported so a field that is not a
 * plain `<input>` — the Jalali date picker, which is a button opening a
 * calendar — is visually the same control rather than a lookalike that drifts.
 */
export const controlBase =
  'w-full rounded-lg border bg-surface-container-lowest px-md text-body-md text-on-surface ' +
  'placeholder:text-outline transition-colors ' +
  'focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary ' +
  'disabled:cursor-not-allowed disabled:bg-surface-container disabled:text-outline';

export interface FieldShellProps {
  id: string;
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  className?: string;
  children: ReactNode;
}

/** Label, control, and the one line of hint-or-error underneath it. */
export function FieldShell({ id, label, hint, error, required, className, children }: FieldShellProps) {
  return (
    <div className={cn('flex flex-col gap-xs', className)}>
      {label && (
        <label htmlFor={id} className="text-label-md font-label-md text-on-surface-variant">
          {label}
          {required && <span className="text-error"> *</span>}
        </label>
      )}
      {children}
      {error ? (
        <p id={`${id}-error`} className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </p>
      ) : (
        hint && (
          <p id={`${id}-hint`} className="text-caption text-on-surface-variant">
            {hint}
          </p>
        )
      )}
    </div>
  );
}

export interface InputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'size'> {
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  /** Material Symbols name shown at the start of the field. */
  icon?: string;
  /** Trailing adornment — a currency unit, or an action button. */
  suffix?: ReactNode;
  wrapperClassName?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, hint, error, icon, suffix, className, wrapperClassName, id, required, dir, ...props },
  ref,
) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;

  return (
    <FieldShell
      id={fieldId}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      {/*
        The direction goes on the wrapper, not only on the input.

        The icon is positioned `start-md` against this box and the input
        reserves room for it with `ps-[44px]` — two logical properties that
        only agree while both elements resolve `start` the same way. A field
        marked `dir="ltr"` (a phone number, an email, a password) flipped the
        input alone: the page is RTL, so the icon stayed pinned to the right
        while the padding moved to the left. Every one of those fields had a
        44px gap on one side and text running under the icon on the other.

        Setting it here flips the box and its contents together, so an LTR
        field looks like any Latin field — icon on the left, text starting
        after it. The label and hint sit outside in `FieldShell` and stay RTL,
        which is what they should be: «شماره موبایل» is Persian either way.
      */}
      <div className="relative flex items-center" dir={dir}>
        {icon && (
          <Icon
            name={icon}
            size={20}
            className="pointer-events-none absolute start-md text-outline"
          />
        )}
        <input
          ref={ref}
          id={fieldId}
          required={required}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${fieldId}-error` : hint ? `${fieldId}-hint` : undefined}
          dir={dir}
          className={cn(
            controlBase,
            'h-12',
            icon && 'ps-[44px]',
            suffix && 'pe-[72px]',
            error ? 'border-error' : 'border-outline-variant',
            className,
          )}
          {...props}
        />
        {suffix && (
          <span className="absolute end-md text-caption text-on-surface-variant">{suffix}</span>
        )}
      </div>
    </FieldShell>
  );
});

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  wrapperClassName?: string;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
  { label, hint, error, className, wrapperClassName, id, required, rows = 4, ...props },
  ref,
) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;

  return (
    <FieldShell
      id={fieldId}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      <textarea
        ref={ref}
        id={fieldId}
        rows={rows}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${fieldId}-error` : hint ? `${fieldId}-hint` : undefined}
        className={cn(
          controlBase,
          'resize-y py-md leading-relaxed',
          error ? 'border-error' : 'border-outline-variant',
          className,
        )}
        {...props}
      />
    </FieldShell>
  );
});

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  wrapperClassName?: string;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  { label, hint, error, className, wrapperClassName, id, required, children, dir, ...props },
  ref,
) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;

  return (
    <FieldShell
      id={fieldId}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      {/* Same reasoning as `Input`: the chevron is placed `end-md` against
          this box while the control reserves room with `pe-[40px]`, so both
          have to resolve `end` the same way. See the note there. */}
      <div className="relative flex items-center" dir={dir}>
        <select
          ref={ref}
          id={fieldId}
          required={required}
          aria-invalid={error ? true : undefined}
          dir={dir}
          className={cn(
            controlBase,
            'h-12 appearance-none pe-[40px]',
            error ? 'border-error' : 'border-outline-variant',
            className,
          )}
          {...props}
        >
          {children}
        </select>
        <Icon
          name="expand_more"
          size={20}
          className="pointer-events-none absolute end-md text-outline"
        />
      </div>
    </FieldShell>
  );
});

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: ReactNode;
  description?: ReactNode;
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, description, className, id, ...props },
  ref,
) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;

  return (
    <div className={cn('flex items-start gap-sm', className)}>
      <input
        ref={ref}
        id={fieldId}
        type="checkbox"
        className="mt-px h-5 w-5 shrink-0 rounded border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
        {...props}
      />
      {(label || description) && (
        <label htmlFor={fieldId} className="cursor-pointer select-none">
          {label && <span className="block text-body-md text-on-surface">{label}</span>}
          {description && (
            <span className="block text-caption text-on-surface-variant">{description}</span>
          )}
        </label>
      )}
    </div>
  );
});

export interface RadioProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: ReactNode;
  description?: ReactNode;
}

export const Radio = forwardRef<HTMLInputElement, RadioProps>(function Radio(
  { label, description, className, id, ...props },
  ref,
) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;

  return (
    <div className={cn('flex items-start gap-sm', className)}>
      <input
        ref={ref}
        id={fieldId}
        type="radio"
        className="mt-px h-5 w-5 shrink-0 border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
        {...props}
      />
      {(label || description) && (
        <label htmlFor={fieldId} className="cursor-pointer select-none">
          {label && <span className="block text-body-md text-on-surface">{label}</span>}
          {description && (
            <span className="block text-caption text-on-surface-variant">{description}</span>
          )}
        </label>
      )}
    </div>
  );
});
