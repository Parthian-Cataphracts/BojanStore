import type { ReactNode } from 'react';
import { Card, Icon, cn } from '@bojan/ui';
import { Container } from '@/components/layout/Container';

export type StatusTone = 'success' | 'error' | 'neutral';

const tones: Record<StatusTone, { ring: string; badge: string }> = {
  success: { ring: 'bg-soft-mint/60', badge: 'bg-soft-mint text-primary' },
  error: { ring: 'bg-error-container/60', badge: 'bg-error-container text-on-error-container' },
  neutral: { ring: 'bg-surface-container-high', badge: 'bg-surface-container-high text-primary' },
};

export interface StatusScreenProps {
  icon: string;
  title: string;
  message: string;
  tone?: StatusTone;
  /** Key/value rows rendered inside the summary card. */
  details?: { label: string; value: ReactNode }[];
  /** Buttons or links, stacked on mobile and inline from `sm` up. */
  actions?: ReactNode;
  /** Extra content between the card and the actions. */
  children?: ReactNode;
  /** Small reassurance line at the very bottom. */
  note?: string;
}

/**
 * Shared outcome screen for 31 (order placed), 32 (payment success),
 * 33 (payment failed), 39 (server error) and 40 (offline): a haloed icon,
 * a headline, an optional detail card, then the actions.
 */
export function StatusScreen({
  icon,
  title,
  message,
  tone = 'success',
  details,
  actions,
  children,
  note,
}: StatusScreenProps) {
  const palette = tones[tone];

  return (
    <Container className="flex min-h-[70vh] flex-col items-center justify-center gap-lg py-xl text-center">
      <span className="relative flex h-24 w-24 items-center justify-center">
        {/* Soft halo behind the glyph, as drawn in the design. */}
        <span
          aria-hidden="true"
          className={cn('absolute inset-0 rounded-full', palette.ring)}
        />
        <span
          className={cn(
            'relative flex h-16 w-16 items-center justify-center rounded-full',
            palette.badge,
          )}
        >
          <Icon name={icon} size={34} />
        </span>
      </span>

      <div className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          {title}
        </h1>
        <p className="mx-auto max-w-xl text-body-md leading-loose text-on-surface-variant">
          {message}
        </p>
      </div>

      {details && details.length > 0 && (
        <Card className="w-full max-w-md p-lg text-start">
          <dl className="flex flex-col gap-sm">
            {details.map((row) => (
              <div
                key={row.label}
                className="flex items-center justify-between gap-md border-b border-paper-border pb-sm last:border-0 last:pb-0"
              >
                <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                <dd className="tabular text-body-md font-label-md text-primary">{row.value}</dd>
              </div>
            ))}
          </dl>
        </Card>
      )}

      {children}

      {actions && (
        <div className="flex w-full max-w-md flex-col gap-md sm:flex-row sm:justify-center">
          {actions}
        </div>
      )}

      {note && <p className="max-w-md text-caption leading-relaxed text-outline">{note}</p>}
    </Container>
  );
}
