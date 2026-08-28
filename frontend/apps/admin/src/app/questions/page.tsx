import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { QuestionActions } from '@/components/QuestionActions';
import { getAdminQuestions, getQuestionCounts } from '@/lib/api/content';
import type { AdminQuestionDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'پرسش‌های مشتریان' };
export const dynamic = 'force-dynamic';

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const statusMeta: Record<
  AdminQuestionDto['status'],
  { label: string; tone: 'mint' | 'warning' | 'error' }
> = {
  published: { label: 'پاسخ داده شده', tone: 'mint' },
  pending: { label: 'در انتظار پاسخ', tone: 'warning' },
  rejected: { label: 'رد شده', tone: 'error' },
};

const columns: Column<AdminQuestionDto>[] = [
  {
    key: 'question',
    header: 'پرسش',
    cell: (row) => (
      <div className="flex max-w-xl flex-col gap-xs">
        <span className="text-label-md font-label-md text-primary">{row.author}</span>

        {/*
          The whole question, not a truncated line. This is the screen where an
          operator answers it, and a reply written against the first forty
          characters is a reply to something the shopper did not ask.
        */}
        <p className="whitespace-pre-line text-body-md leading-loose text-on-surface-variant">
          {row.body}
        </p>

        {row.answer && (
          <div className="mt-xs flex flex-col gap-xs rounded-lg bg-surface-container-low p-md">
            <span className="text-caption text-outline">
              پاسخ {row.answerAuthor}
              {row.answeredAt ? ` — ${formatDate(row.answeredAt)}` : ''}
            </span>
            <p className="whitespace-pre-line text-body-md leading-loose text-on-surface">
              {row.answer}
            </p>
          </div>
        )}
      </div>
    ),
  },
  {
    key: 'product',
    header: 'محصول',
    cell: (row) => (
      // The product's page in the panel, by id — see the review queue's own
      // column for why not the storefront slug.
      <Link
        href={`/products/${row.productId}`}
        className="text-label-md text-on-surface-variant transition-colors hover:text-primary"
      >
        {row.productTitle}
      </Link>
    ),
  },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => <Badge tone={statusMeta[row.status].tone}>{statusMeta[row.status].label}</Badge>,
  },
  {
    key: 'askedAt',
    header: 'تاریخ',
    cell: (row) => <span className="tabular">{formatDate(row.askedAt)}</span>,
  },
];

/**
 * «پرسش‌های مشتریان» — the question queue.
 *
 * Questions arrive «در انتظار پاسخ» and the storefront shows only answered
 * ones. That half was built and this half was not: `ProductQuestion.Answer` had
 * no callers anywhere, there was no admin query, no endpoint and no screen. So
 * a shopper asked, the form thanked them, the row was written, and nobody could
 * ever see it — every question the shop had ever been asked sat unread, and the
 * product page showed none of them however many there were.
 *
 * Defaults to the pending tab, and the rows come oldest first. That is the
 * queue — somebody waiting, longest first — rather than the archive of
 * everything ever asked.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const status = first(params.status) ?? 'pending';

  const [{ items: rows, total }, counts] = await Promise.all([
    getAdminQuestions({ q: query, status, pageSize: 100 }),
    getQuestionCounts(),
  ]);

  const label = (text: string, key: string) =>
    counts[key] === undefined ? text : `${text} (${toPersianDigits(counts[key])})`;

  return (
    <AdminPage
      title="پرسش‌های مشتریان"
      breadcrumbs={[{ label: 'محتوا', href: '/content' }, { label: 'پرسش‌های مشتریان' }]}
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی نام، متن پرسش یا محصول..."
          filters={[
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'pending', label: label('در انتظار پاسخ', 'pending') },
                { value: 'published', label: label('پاسخ داده شده', 'published') },
                { value: 'rejected', label: label('رد شده', 'rejected') },
              ],
            },
          ]}
        />
      </Suspense>

      <p className="text-caption leading-relaxed text-on-surface-variant">
        {toPersianDigits(total)} پرسش — نوشتن پاسخ همان چیزی است که پرسش را منتشر می‌کند، و پس از آن
        پرسش و پاسخ با هم در صفحه محصول دیده می‌شوند.
      </p>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="help"
        emptyTitle="پرسشی در این وضعیت نیست"
        emptyDescription="وضعیت دیگری را انتخاب کنید یا عبارت جستجو را تغییر دهید."
        actions={(row) => <QuestionActions question={row} />}
      />
    </AdminPage>
  );
}
