'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, FormStatus, Icon, Select, Sheet } from '@bojan/ui';
import { postJson } from '@/lib/submit';

export interface BusinessRequestActionsProps {
  requestId: string;
  /** Operators the request can be handed to — screen 145's admin users. */
  specialists: { id: string; name: string }[];
  /**
   * Which part of screen 120 to render: the title-row buttons, the
   * quick-action panel, or the specialist's note.
   */
  slot: 'header' | 'aside' | 'note';
}

/**
 * Screen 120's controls: take the request up, reject it, or hand it to a
 * colleague. All of them were buttons with no handler.
 *
 * Issuing a pro-forma is not here — it needs products and quantities, so it
 * lives in the composer on the same screen.
 *
 * Rejection asks first — it is the one action here the operator cannot undo
 * from this screen.
 */
export function BusinessRequestActions({
  requestId,
  specialists,
  slot,
}: BusinessRequestActionsProps) {
  const router = useRouter();
  const [pending, setPending] = useState<string | null>(null);
  const [confirmingReject, setConfirmingReject] = useState(false);
  const [assignee, setAssignee] = useState(specialists[0]?.id ?? '');
  const [note, setNote] = useState('');
  const [noteSaved, setNoteSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function send(action: string, body: Record<string, unknown>) {
    setPending(action);
    setError(null);
    try {
      await postJson('/api/admin/business-requests', { id: requestId, ...body });
      setConfirmingReject(false);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'عملیات انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  if (slot === 'note') {
    return (
      <>
        <textarea
          rows={4}
          value={note}
          onChange={(event) => {
            setNote(event.target.value);
            setNoteSaved(false);
          }}
          maxLength={2000}
          aria-label="یادداشت کارشناس"
          placeholder="نتیجه تماس، شرایط توافق‌شده یا دلیل رد درخواست..."
          className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-md py-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
        />

        <div className="flex flex-wrap items-center gap-md">
          <Button
            size="sm"
            className="px-lg"
            loading={pending === 'note'}
            disabled={note.trim().length === 0}
            onClick={async () => {
              await send('note', { note: note.trim() });
              setNoteSaved(true);
            }}
          >
            ثبت یادداشت
          </Button>

          <FormStatus ok={noteSaved && !error ? 'ذخیره شد.' : null} error={error} />
        </div>
      </>
    );
  }

  if (slot === 'header') {
    return (
      <>
        {/*
          No "issue a quote" button here any more. It used to post
          `status: 'quoted'` — telling the organisation a pro-forma existed while
          producing none. Issuing one means naming products and quantities, so it
          is the composer further down the screen rather than a button in the
          title row.
        */}
        <Button
          size="sm"
          variant="outline"
          icon="fact_check"
          loading={pending === 'reviewing'}
          onClick={() => send('reviewing', { status: 'reviewing' })}
        >
          شروع بررسی
        </Button>

        <Button
          size="sm"
          variant="outline"
          icon="close"
          onClick={() => setConfirmingReject(true)}
        >
          رد درخواست
        </Button>

        <Sheet
          open={confirmingReject}
          onClose={() => setConfirmingReject(false)}
          title="رد درخواست سازمانی"
          footer={
            <div className="flex flex-col gap-sm sm:flex-row-reverse">
              <Button
                variant="danger"
                size="lg"
                loading={pending === 'reject'}
                onClick={() => send('reject', { status: 'rejected' })}
                className="sm:px-xl"
              >
                رد کردن درخواست
              </Button>
              <Button
                variant="ghost"
                size="lg"
                onClick={() => setConfirmingReject(false)}
                className="sm:px-xl"
              >
                انصراف
              </Button>
            </div>
          }
        >
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            این درخواست رد شود؟ مشتری از تغییر وضعیت مطلع می‌شود.
          </p>

          {error && (
            <p
              role="alert"
              className="mt-md flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
            >
              <Icon name="error" size={20} className="mt-px shrink-0" />
              {error}
            </p>
          )}
        </Sheet>
      </>
    );
  }

  return (
    <div className="flex flex-col gap-sm">
      <Select
        label="تخصیص به کارشناس"
        value={assignee}
        onChange={(event) => setAssignee(event.target.value)}
      >
        {specialists.map((person) => (
          <option key={person.id} value={person.id}>
            {person.name}
          </option>
        ))}
      </Select>

      <Button
        variant="outline"
        icon="person_add"
        loading={pending === 'assign'}
        disabled={!assignee}
        onClick={() => send('assign', { assigneeId: assignee })}
      >
        تخصیص درخواست
      </Button>

      {/* Suppressed while the reject confirmation is open — that panel shows
          the same error itself, and two copies of one failure is one too many. */}
      <FormStatus error={confirmingReject ? null : error} />
    </div>
  );
}
