'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, FormStatus, Icon, cn } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { AdminReviewDto } from '@/lib/api/types';

/**
 * The verdict controls on one row of the review queue.
 *
 * Two decisions kept apart. «تأیید» / «رد» decides whether the review appears
 * on its product page; the star decides whether it is also one of the handful
 * quoted on the home page. Collapsing them would make every approval a
 * promotion to the shop's front door, which is not what approving a three-star
 * review means.
 *
 * Deleting asks first, and is the only action here that does. The other three
 * are one click to undo — a review moved to «رد شده» moves back — so a
 * confirmation on each would be a dialog the operator learns to dismiss without
 * reading, on the day it matters.
 */
export function ReviewActions({ review }: { review: AdminReviewDto }) {
  const router = useRouter();
  const [pending, setPending] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function send(action: string, path: string, body: Record<string, unknown>) {
    setPending(action);
    setError(null);
    try {
      await postJson(path, { id: review.id, ...body });
      setConfirmingDelete(false);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'عملیات انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex flex-col items-start gap-sm">
      <div className="flex flex-wrap items-center gap-sm">
        {review.status !== 'published' && (
          <Button
            size="sm"
            variant="ghost"
            loading={pending === 'approve'}
            onClick={() => send('approve', '/api/admin/review-status', { status: 'published', slug: review.productSlug })}
          >
            تأیید
          </Button>
        )}

        {review.status !== 'rejected' && (
          <Button
            size="sm"
            variant="ghost"
            loading={pending === 'reject'}
            onClick={() => send('reject', '/api/admin/review-status', { status: 'rejected', slug: review.productSlug })}
          >
            رد
          </Button>
        )}

        {/*
          Only offered on a published review, because the API refuses it on any
          other — see AdminReviewService.SetFeaturedAsync. A button that is
          shown, clicked, and comes back with an error the operator cannot act
          on is worse than one that is not there until it can work.
        */}
        {review.status === 'published' && (
          <button
            type="button"
            disabled={pending === 'feature'}
            aria-pressed={review.featuredOnHome}
            title={
              review.featuredOnHome
                ? 'برداشتن از صفحه اصلی'
                : 'نمایش این نظر در صفحه اصلی'
            }
            onClick={() =>
              send('feature', '/api/admin/review-featured', { featured: !review.featuredOnHome })
            }
            className={cn(
              'inline-flex items-center gap-xs rounded-full px-md py-xs text-label-md transition-colors disabled:opacity-50',
              review.featuredOnHome
                ? 'bg-soft-mint text-primary'
                : 'text-on-surface-variant hover:bg-surface-container-high',
            )}
          >
            <Icon name="star" filled={review.featuredOnHome} size={18} />
            صفحه اصلی
          </button>
        )}

        {confirmingDelete ? (
          <span className="flex items-center gap-sm">
            <Button
              size="sm"
              variant="ghost"
              loading={pending === 'delete'}
              onClick={() => send('delete', '/api/admin/review-delete', { slug: review.productSlug })}
            >
              حذف قطعی
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setConfirmingDelete(false)}>
              انصراف
            </Button>
          </span>
        ) : (
          <Button size="sm" variant="ghost" onClick={() => setConfirmingDelete(true)}>
            حذف
          </Button>
        )}
      </div>

      {confirmingDelete && (
        <p className="text-caption leading-relaxed text-on-surface-variant">
          حذف برگشت‌پذیر نیست. برای پنهان کردن نظر، «رد» را انتخاب کنید.
        </p>
      )}

      <FormStatus ok={null} error={error} />
    </div>
  );
}
