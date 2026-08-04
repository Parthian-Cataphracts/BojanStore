import type { ReactNode } from 'react';
import { Card, Icon, cn } from '@bojan/ui';

/**
 * The frame every auth screen sits in.
 *
 * One component because all four were the same card, the same icon medallion
 * and the same title block copied four times — and because the sizing has to
 * be decided once. These are the screens most likely to be met on a phone, and
 * the register form was 790px tall inside an 812px viewport: it technically
 * fitted and then did not, the moment the keyboard opened.
 *
 * So everything here is smaller below `md` and grows on a desktop that has the
 * room: the medallion, the heading, the card padding and the gap under the
 * title block. Nothing is hidden on small screens — a form that drops its
 * explanation on a phone is worse than one that scrolls.
 */
export function AuthCard({
  icon,
  title,
  caption,
  children,
  above,
  below,
  tone = 'brand',
}: {
  icon: string;
  title: string;
  caption?: ReactNode;
  children: ReactNode;
  /** Rendered above the title — the sign-in/register switch, a status banner. */
  above?: ReactNode;
  /** Rendered under the form, outside it. The terms line. */
  below?: ReactNode;
  /** `error` for the dead ends — an expired reset link, mainly. */
  tone?: 'brand' | 'error';
}) {
  return (
    <Card className="w-full max-w-md p-lg shadow-soft md:p-xl">
      {above}

      <div className="mb-md flex flex-col items-center gap-xs text-center md:mb-lg md:gap-sm">
        <span
          className={cn(
            'flex h-12 w-12 items-center justify-center rounded-full md:h-16 md:w-16',
            tone === 'error' ? 'bg-error-container text-error' : 'bg-soft-mint text-primary',
          )}
        >
          <Icon name={icon} size={24} className="md:hidden" />
          <Icon name={icon} size={32} className="max-md:hidden" />
        </span>

        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-display-md">
          {title}
        </h1>

        {caption && (
          <p className="text-body-md leading-relaxed text-on-surface-variant">{caption}</p>
        )}
      </div>

      {children}

      {below}
    </Card>
  );
}
