import { forwardRef, useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

const controlBase =
  'w-full rounded-lg border bg-surface-container-lowest px-md text-body-md text-on-surface ' +
  'placeholder:text-outline transition-colors ' +
  'focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary ' +
  'disabled:cursor-not-allowed disabled:bg-surface-container disabled:text-outline';

interface FieldShellProps {
  id: string;
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  className?: string;
  children: ReactNode;
}

function FieldShell({ id, label, hint, error, required, className, children }: FieldShellProps) {
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
  { label, hint, error, icon, suffix, className, wrapperClassName, id, required, ...props },
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
      <div className="relative flex items-center">
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
  { label, hint, error, className, wrapperClassName, id, required, children, ...props },
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
      <div className="relative flex items-center">
        <select
          ref={ref}
          id={fieldId}
          required={required}
          aria-invalid={error ? true : undefined}
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
