import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Icon, Rating, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { ReviewActions } from '@/components/ReviewActions';
import { getAdminReviews, getReviewCounts } from '@/lib/api/content';
import type { AdminReviewDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'نظرات مشتریان' };
export const dynamic = 'force-dynamic';

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const statusMeta: Record<
  AdminReviewDto['status'],
  { label: string; tone: 'mint' | 'warning' | 'error' }
> = {
  published: { label: 'تأیید شده', tone: 'mint' },
  pending: { label: 'در انتظار', tone: 'warning' },
  rejected: { label: 'رد شده', tone: 'error' },
};

const columns: Column<AdminReviewDto>[] = [
  {
    key: 'review',
    header: 'نظر',
    cell: (row) => (
      <div className="flex max-w-xl flex-col gap-xs">
        <span className="flex flex-wrap items-center gap-sm">
          <Rating value={row.rating} />
          <span className="text-label-md font-label-md text-primary">{row.author}</span>
          {row.verified && <Badge tone="mint">خرید تأییدشده</Badge>}
          {row.featuredOnHome && (
            <Badge tone="teal">
              <span className="flex items-center gap-xs">
                <Icon name="star" filled size={13} />
                صفحه اصلی
              </span>
            </Badge>
          )}
        </span>

        {row.title && <span className="text-body-md text-on-surface">{row.title}</span>}

        {/*
          The whole review, not a truncated line. This is the screen where an
          operator decides whether the shop publishes these words, and a
          decision made on the first forty characters is not a moderation
          decision.
        */}
        <p className="whitespace-pre-line text-body-md leading-loose text-on-surface-variant">
          {row.body}
        </p>
      </div>
    ),
  },
  {
    key: 'product',
    header: 'محصول',
    cell: (row) => (
      /*
        The product's page in the panel, by id.

        Not the storefront's `/products/<slug>`: this link is rendered by the
        admin app, where `/products/[id]` is its own route — the product
        editor — so a slug pointed there resolves to a panel page looking up an
        id it will never find. The panel has no configured address for the
        storefront to link out to instead, and a guessed one is a link that
        breaks the day either app moves.
      */
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
    key: 'createdAt',
    header: 'تاریخ',
    cell: (row) => <span className="tabular">{formatDate(row.createdAt)}</span>,
  },
];

/**
 * «نظرات مشتریان» — the review moderation queue.
 *
 * Reviews arrive «در انتظار» and the storefront shows only published ones. That
 * half was built and this half was not, so every review a customer ever wrote
 * sat unpublished with nothing in the panel able to release it: from where the
 * shop's owner sat, reviews simply did not work.
 *
 * Defaults to the pending tab. That is the queue — the reviews waiting on
 * somebody, rather than the archive of everything ever written — and an
 * operator opening this screen is almost always here to work through it.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const status = first(params.status) ?? 'pending';

  const [{ items: rows, total }, counts] = await Promise.all([
    getAdminReviews({ q: query, status, pageSize: 100 }),
    getReviewCounts(),
  ]);

  // The count goes on the label so the tab reads «در انتظار (۷)». The filter
  // bar renders plain options, and a badge an operator has to hover for is not
  // the point of showing a backlog.
  const label = (text: string, key: string) =>
    counts[key] === undefined ? text : `${text} (${toPersianDigits(counts[key])})`;

  return (
    <AdminPage
      title="نظرات مشتریان"
      breadcrumbs={[{ label: 'محتوا', href: '/content' }, { label: 'نظرات مشتریان' }]}
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی نام، متن نظر یا محصول..."
          filters={[
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'pending', label: label('در انتظار', 'pending') },
                { value: 'published', label: label('تأیید شده', 'published') },
                { value: 'rejected', label: label('رد شده', 'rejected') },
                { value: 'featured', label: label('صفحه اصلی', 'featured') },
              ],
            },
          ]}
        />
      </Suspense>

      <p className="text-caption leading-relaxed text-on-surface-variant">
        {toPersianDigits(total)} نظر — تأییدشده‌ها در صفحه محصول دیده می‌شوند و نظرهای ستاره‌دار در
        بخش «نظرات مشتریان» صفحه اصلی نمایش داده می‌شوند.
      </p>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="reviews"
        emptyTitle="نظری در این وضعیت نیست"
        emptyDescription="وضعیت دیگری را انتخاب کنید یا عبارت جستجو را تغییر دهید."
        actions={(row) => <ReviewActions review={row} />}
      />
    </AdminPage>
  );
}
