'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Icon, Sheet } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';

/**
 * Delete an address, on screen 14.
 *
 * Deleting is not undoable, so it asks first — in the same bottom sheet the
 * design uses for its other confirmations rather than a browser `confirm()`.
 */
export function DeleteAddressButton({
  addressId,
  title,
}: {
  addressId: string;
  title: string;
}) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function remove() {
    setPending(true);
    setError(null);
    try {
      await postJson('/api/account/address-delete', { id: addressId });
      setOpen(false);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'حذف آدرس انجام نشد.');
    } finally {
      setPending(false);
    }
  }

  return (
    <>
      <button
        type="button"
        aria-label={`حذف آدرس ${title}`}
        onClick={() => setOpen(true)}
        className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
      >
        <Icon name="delete" size={20} />
      </button>

      <Sheet
        open={open}
        onClose={() => setOpen(false)}
        title="حذف آدرس"
        footer={
          <div className="flex flex-col gap-sm sm:flex-row-reverse">
            <Button
              variant="danger"
              size="lg"
              loading={pending}
              onClick={remove}
              className="sm:px-xl"
            >
              حذف آدرس
            </Button>
            <Button
              variant="ghost"
              size="lg"
              onClick={() => setOpen(false)}
              className="sm:px-xl"
            >
              انصراف
            </Button>
          </div>
        }
      >
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          آدرس «{title}» حذف شود؟ این کار قابل بازگشت نیست.
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
