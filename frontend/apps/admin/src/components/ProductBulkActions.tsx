'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Card, FormStatus, Icon, toPersianDigits } from '@bojan/ui';
import { useTableSelection } from './TableSelection';
import { postJson } from '@/lib/submit';

/**
 * What the API did with the batch.
 *
 * `blocked` is only ever filled by the delete — the titles of the products it
 * kept because they have trading history. See
 * `AdminCatalogueService.DeleteProductsAsync`.
 */
interface BulkResult {
  changed: number;
  blocked?: string[];
}

/**
 * The bar over screen 96's list, once something is ticked.
 *
 * Retiring a season's stock used to be twenty round trips through the product
 * form, and an operator doing that twenty times gets one of them wrong. The two
 * verbs here are the two an operator actually wants from a list: take these out
 * of the shop, or take these away.
 *
 * They are not the same verb. «بایگانی» is the soft delete — the product is
 * gone from the storefront and still on every invoice that sold it — and it is
 * the right answer for almost everything. «حذف» takes the row away and the API
 * refuses it for any product that has ever been ordered, counted in a stocktake
 * or returned; it is for the product created by mistake. Only the delete asks
 * for a confirmation, because it is the only one of the two that cannot be
 * undone from this screen.
 *
 * «خروج از بایگانی» appears only while the archived filter is the one in force,
 * and it restores to «پیش‌نویس» rather than to «منتشر شده». Both are deliberate:
 * under any other filter the selection is a mixture and «restore» would silently
 * unpublish whatever in it was live, and taking something out of the archive is
 * not the same decision as putting it back in front of shoppers.
 */
export function ProductBulkActions({ archivedFilter }: { archivedFilter: boolean }) {
  const { picked, clear } = useTableSelection();
  const router = useRouter();
  const [pending, setPending] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const ids = [...picked];

  /*
    The bar belongs to the selection, and a batch that succeeds clears the
    selection — so reporting the result inside it would mean the sentence is
    removed by the very thing it reports. It stays up for the report instead,
    with the count and the verbs gone, until the operator ticks the next row.
    A failure keeps its selection, so it keeps the buttons too: the whole point
    of the message is that the same batch can be tried again.
  */
  const idle = ids.length === 0;

  if (idle && !done) {
    return null;
  }

  async function run(
    action: string,
    path: string,
    body: Record<string, unknown>,
    describe: (result: BulkResult) => string,
  ) {
    setPending(action);
    setError(null);
    setDone(null);

    try {
      const result = await postJson<BulkResult>(path, { ids, ...body });
      setConfirmingDelete(false);
      // Cleared before the refresh rather than after it: the rows are about to
      // change under the table, and a tick left on a row that has just been
      // archived out of the current filter is a selection the operator cannot
      // see and would act on again.
      clear();
      setDone(describe(result));
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'عملیات روی محصولات انجام نشد.');
    } finally {
      setPending(null);
    }
  }

  const count = toPersianDigits(ids.length);

  return (
    <Card className="gap-sm p-md flex flex-col">
      {!idle && (
        <div className="gap-md flex flex-wrap items-center">
          <span className="text-body-md text-on-surface font-medium">
            {count} محصول از این صفحه انتخاب شده
          </span>

          <span className="gap-sm flex flex-wrap items-center">
            <Button
              size="sm"
              variant="outline"
              icon="inventory_2"
              loading={pending === 'archive'}
              disabled={pending !== null}
              onClick={() =>
                run(
                  'archive',
                  '/api/admin/product-bulk-status',
                  { status: 'archived' },
                  (result) => `${toPersianDigits(result.changed)} محصول بایگانی شد.`,
                )
              }
            >
              بایگانی
            </Button>

            {archivedFilter && (
              <Button
                size="sm"
                variant="outline"
                loading={pending === 'restore'}
                disabled={pending !== null}
                onClick={() =>
                  run(
                    'restore',
                    '/api/admin/product-bulk-status',
                    { status: 'draft' },
                    (result) =>
                      `${toPersianDigits(result.changed)} محصول از بایگانی درآمد و پیش‌نویس شد.`,
                  )
                }
              >
                خروج از بایگانی
              </Button>
            )}

            {confirmingDelete ? (
              <span className="gap-sm flex flex-wrap items-center">
                <Button
                  size="sm"
                  variant="danger"
                  loading={pending === 'delete'}
                  disabled={pending !== null}
                  onClick={() =>
                    run('delete', '/api/admin/product-bulk-delete', {}, (result) => {
                      const removed = `${toPersianDigits(result.changed)} محصول حذف شد.`;
                      // Named rather than counted. "۳ محصول حذف نشد" on a page of
                      // twenty leaves the operator to work out which three. The
                      // reason names all three causes the API refuses on, not
                      // only sales: a product kept because of a stocktake would
                      // otherwise be reported as having sold.
                      return result.blocked?.length
                        ? `${removed} این‌ها به‌خاطر سابقه‌ی فروش، انبارگردانی یا مرجوعی باقی ماندند: ${result.blocked.join('، ')}`
                        : removed;
                    })
                  }
                >
                  حذف قطعی
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  disabled={pending !== null}
                  onClick={() => setConfirmingDelete(false)}
                >
                  انصراف
                </Button>
              </span>
            ) : (
              <Button
                size="sm"
                variant="ghost"
                icon="delete"
                disabled={pending !== null}
                onClick={() => setConfirmingDelete(true)}
              >
                حذف
              </Button>
            )}

            <Button size="sm" variant="ghost" disabled={pending !== null} onClick={clear}>
              لغو انتخاب
            </Button>
          </span>
        </div>
      )}

      {!idle && confirmingDelete && (
        <p className="text-caption text-on-surface-variant gap-xs flex items-start leading-relaxed">
          <Icon name="warning" size={18} className="text-error shrink-0" />
          <span>
            حذف برگشت‌پذیر نیست و نظرها، پرسش‌ها و تصویرهای این محصول‌ها هم با آن‌ها می‌روند. محصولی
            که سابقه‌ی فروش، انبارگردانی یا مرجوعی دارد حذف نمی‌شود و نامش را همین‌جا می‌بینید. برای
            بیرون بردن محصول از فروشگاه، «بایگانی» را انتخاب کنید.
          </span>
        </p>
      )}

      <FormStatus ok={done} error={error} />
    </Card>
  );
}
