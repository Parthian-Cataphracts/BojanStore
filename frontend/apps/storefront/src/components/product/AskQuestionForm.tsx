'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Textarea } from '@bojan/ui';
import { formPayload, postJson } from '@/lib/api/submit';

/** The "ask a question" box on screen 85. */
export function AskQuestionForm({ productSlug }: { productSlug: string }) {
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const question = String(new FormData(event.currentTarget).get('question') ?? '').trim();

    if (question.length < 10) {
      setError('پرسش خود را کامل‌تر بنویسید.');
      return;
    }

    setError(null);
    setSending(true);
    try {
      await postJson('/api/account/question', { ...formPayload(form), productSlug });
      setSent(true);
      form.reset();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت پرسش انجام نشد.');
    } finally {
      setSending(false);
    }
  }

  if (sent) {
    return (
      <Card className="flex items-start gap-sm p-md">
        <Icon name="check_circle" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          پرسش شما ثبت شد. پس از بررسی و پاسخ کارشناسان، همین‌جا نمایش داده می‌شود.
        </p>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <h2 className="font-headline text-card-title text-primary">پرسش خود را بپرسید</h2>

      <form onSubmit={submit} noValidate className="flex flex-col gap-md">
        <Textarea
          name="question"
          label="متن پرسش"
          placeholder="درباره این محصول چه چیزی می‌خواهید بدانید؟"
          rows={3}
          {...(error ? { error } : null)}
        />
        <Button type="submit" loading={sending} className="self-start px-xl">
          ثبت پرسش
        </Button>
      </form>
    </Card>
  );
}
