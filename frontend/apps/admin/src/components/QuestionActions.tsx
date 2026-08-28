'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, FormStatus, Textarea } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { AdminQuestionDto } from '@/lib/api/types';

/**
 * The reply box and verdict controls on one row of the question queue.
 *
 * There is no «تأیید» here, and that is the whole shape of the screen: a
 * question is published by being answered, so approving one without a reply
 * would put a customer's question on the product page with nothing under it —
 * a shop displaying that it does not respond. The API refuses that outright;
 * this simply does not offer it.
 *
 * «رد» is the other direction, for something abusive or off-topic that needs no
 * reply. Deleting asks first, and is the only action here that does: rejecting
 * is one click to undo, so a confirmation on each would be a dialog the
 * operator learns to dismiss without reading, on the day it matters.
 */
export function QuestionActions({ question }: { question: AdminQuestionDto }) {
  const router = useRouter();
  const [draft, setDraft] = useState(question.answer ?? '');
  const [pending, setPending] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function send(action: string, path: string, body: Record<string, unknown>) {
    setPending(action);
    setError(null);
    try {
      await postJson(path, { id: question.id, slug: question.productSlug, ...body });
      setConfirmingDelete(false);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'عملیات انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  const answered = question.status === 'published';

  return (
    <div className="flex w-full max-w-xl flex-col items-start gap-sm">
      <Textarea
        name="answer"
        label={answered ? 'ویرایش پاسخ' : 'پاسخ شما'}
        rows={3}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        placeholder="پاسخی که زیر این پرسش در صفحه محصول دیده می‌شود…"
      />

      <div className="flex flex-wrap items-center gap-sm">
        <Button
          size="sm"
          loading={pending === 'answer'}
          // An empty reply is refused by the API too; not offering the button
          // is how the operator finds that out without a round trip.
          disabled={draft.trim().length === 0}
          onClick={() => send('answer', '/api/admin/question-answer', { body: draft.trim() })}
        >
          {answered ? 'ثبت ویرایش' : 'ثبت پاسخ و انتشار'}
        </Button>

        {question.status !== 'rejected' && (
          <Button
            size="sm"
            variant="ghost"
            loading={pending === 'reject'}
            onClick={() => send('reject', '/api/admin/question-status', { status: 'rejected' })}
          >
            رد
          </Button>
        )}

        {question.status === 'rejected' && (
          <Button
            size="sm"
            variant="ghost"
            loading={pending === 'restore'}
            onClick={() => send('restore', '/api/admin/question-status', { status: 'pending' })}
          >
            بازگرداندن به صف
          </Button>
        )}

        {confirmingDelete ? (
          <span className="flex items-center gap-sm">
            <Button
              size="sm"
              variant="ghost"
              loading={pending === 'delete'}
              onClick={() => send('delete', '/api/admin/question-delete', {})}
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
          حذف برگشت‌پذیر نیست. برای پنهان کردن پرسش، «رد» را انتخاب کنید.
        </p>
      )}

      <FormStatus ok={null} error={error} />
    </div>
  );
}
