import { cn } from '../lib/cn';
import { Icon } from './Icon';

export interface FormStatusProps {
  /** Shown when a write succeeded — "ذخیره شد." and its variants. */
  ok?: string | null;
  /** Shown when it did not. The server's own sentence, where there is one. */
  error?: string | null;
  className?: string;
}

/**
 * What a form says after it submits.
 *
 * Twenty-seven components had written this pair by hand, and the pair is doing
 * two things that are easy to get subtly wrong and impossible to notice one
 * screen at a time: the success line is `aria-live="polite"`, so a screen
 * reader hears it without being interrupted, and the failure line is
 * `role="alert"`, so it interrupts — because a failed save is worth the
 * interruption and a successful one is not.
 *
 * They had already drifted. Most used `text-caption` inside a `<span>`; the
 * invoice settings screen used `text-body-sm` inside a `<p>`, which is a
 * different size on a screen an operator reaches from the same menu. That is
 * the ordinary fate of a pattern that lives in twenty-seven places.
 *
 * Both may show at once, which is deliberate: a form that saved and then failed
 * a connection test has two things to say, and collapsing them into one line
 * loses whichever the caller happened to check second.
 */
export function FormStatus({ ok, error, className }: FormStatusProps) {
  if (!ok && !error) return null;

  return (
    <div className={cn('flex flex-wrap items-center gap-md', className)}>
      {ok && (
        <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
          <Icon name="check_circle" size={16} />
          {ok}
        </span>
      )}

      {error && (
        <span role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </span>
      )}
    </div>
  );
}
