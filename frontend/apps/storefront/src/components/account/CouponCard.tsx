'use client';

import { useState } from 'react';
import { Code, Icon, cn, formatDate, formatNumber, formatPercent } from '@bojan/ui';
import type { Coupon } from '@/lib/api/types';

/**
 * Screen 59's ticket-shaped coupon: description on one side, code and copy
 * button on the other, separated by a dashed perforation with punched corners.
 */
export function CouponCard({ coupon }: { coupon: Coupon }) {
  const [copied, setCopied] = useState(false);
  const expired = new Date(coupon.expiresAt).getTime() < Date.now();
  const inactive = coupon.used || expired;

  async function copy() {
    try {
      await navigator.clipboard.writeText(coupon.code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard unavailable — the code is on screen to read anyway.
    }
  }

  const value = coupon.percent
    ? formatPercent(coupon.percent) + ' تخفیف'
    : `${formatNumber(coupon.amount ?? 0)} تومان`;

  return (
    <article
      className={cn(
        'paper-card relative flex flex-col overflow-hidden rounded-xl shadow-ambient sm:flex-row',
        inactive && 'opacity-60',
      )}
    >
      <div className="flex flex-1 flex-col gap-sm p-lg">
        <div className="flex flex-wrap items-start justify-between gap-sm">
          <h3 className="text-body-lg font-label-md text-primary">{coupon.title}</h3>
          <span className="shrink-0 rounded-full bg-soft-mint px-md py-xs text-caption font-label-md text-primary">
            {value}
          </span>
        </div>

        <p className="text-caption leading-relaxed text-on-surface-variant">{coupon.condition}</p>

        <span
          className={cn(
            'mt-auto flex items-center gap-xs pt-sm text-caption',
            expired ? 'text-error' : 'text-on-surface-variant',
          )}
        >
          <Icon name="schedule" size={16} />
          <span className="tabular">
            {expired ? 'منقضی شده' : `اعتبار تا ${formatDate(coupon.expiresAt, 'long')}`}
          </span>
        </span>
      </div>

      {/* Perforation: dashed rule plus the two punched notches. */}
      <div className="relative flex items-center" aria-hidden="true">
        <span className="absolute inset-x-0 top-0 -translate-y-1/2 sm:inset-y-0 sm:start-0 sm:top-auto sm:-translate-x-1/2 sm:translate-y-0">
          <span className="block h-4 w-4 rounded-full bg-paper-bg sm:-mt-2" />
        </span>
        <span className="h-0 w-full border-t border-dashed border-paper-border sm:h-full sm:w-0 sm:border-s sm:border-t-0" />
        <span className="absolute inset-x-0 bottom-0 translate-y-1/2 sm:inset-y-0 sm:bottom-auto sm:end-0 sm:translate-x-1/2 sm:translate-y-0">
          <span className="ms-auto block h-4 w-4 rounded-full bg-paper-bg sm:mt-auto" />
        </span>
      </div>

      <div className="flex flex-col items-center justify-center gap-md p-lg sm:w-44">
        <Code className="rounded-lg border border-dashed border-primary/40 bg-surface-container-lowest px-md py-sm text-label-md font-semibold tracking-widest text-primary">
          {coupon.code}
        </Code>

        <button
          type="button"
          disabled={inactive}
          onClick={copy}
          className={cn(
            'flex w-full items-center justify-center gap-xs rounded-lg px-md py-sm text-label-md font-label-md transition-colors',
            'bg-secondary-container text-on-tertiary hover:bg-secondary',
            'disabled:cursor-not-allowed disabled:bg-surface-container disabled:text-outline',
          )}
        >
          {copied ? 'کپی شد' : 'کپی کد'}
          <Icon name={copied ? 'check' : 'content_copy'} size={18} />
        </button>
      </div>
    </article>
  );
}
