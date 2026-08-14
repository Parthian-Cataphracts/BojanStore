import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Icon, buttonClasses, formatDate } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAdminArticles, getContent } from '@/lib/api/content';
import type { ContentEntryDto } from '@/lib/api/types';
import type { ContentEntry } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت محتوا' };
export const dynamic = 'force-dynamic';

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const typeLabels: Record<ContentEntry['type'], string> = {
  article: 'مقاله',
  page: 'صفحه ثابت',
  banner: 'بنر',
  faq: 'سوال متداول',
};

/**
 * A row on this screen, whichever table it came out of.
 *
 * `href` rather than a lookup by kind, because the two article sources open in
 * different editors and the row is the only thing that knows which it is.
 */
interface ContentRow {
  key: string;
  title: string;
  type: ContentEntry['type'];
  status: string;
  author: string;
  updatedAt: string;
  href: string;
  /** True for the rows the magazine actually serves. */
  live: boolean;
}

const statusMeta: Record<string, { label: string; tone: 'mint' | 'warning' | 'neutral' }> = {
  published: { label: 'منتشر شده', tone: 'mint' },
  draft: { label: 'پیش‌نویس', tone: 'warning' },
  archived: { label: 'بایگانی', tone: 'neutral' },
};

const columns: Column<ContentRow>[] = [
  {
    key: 'title',
    header: 'عنوان',
    cell: (row) => (
      <span className="flex flex-wrap items-center gap-xs">
        {row.title}
        {/*
          Which of two article rows this is. The magazine reads one table; the
          panel used to write another, and the only way to tell them apart was
          that one of them never appeared on the site.
        */}
        {row.type === 'article' &&
          (row.live ? (
            <Badge tone="mint">در مجله</Badge>
          ) : (
            <Badge tone="neutral">قدیمی — در مجله نیست</Badge>
          ))}
      </span>
    ),
  },
  { key: 'type', header: 'نوع', cell: (row) => typeLabels[row.type] },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => {
      const meta = statusMeta[row.status] ?? statusMeta.draft!;
      return <Badge tone={meta.tone}>{meta.label}</Badge>;
    },
  },
  { key: 'author', header: 'نویسنده', cell: (row) => row.author || '—' },
  {
    key: 'updated',
    header: 'آخرین ویرایش',
    cell: (row) => <span className="tabular">{formatDate(row.updatedAt)}</span>,
  },
];

/** Which screen edits each kind of content entry. */
const editorPath: Record<ContentEntry['type'], string> = {
  article: '/content/pages',
  page: '/content/pages',
  banner: '/content/banners',
  faq: '/content/faq',
};

/**
 * Screen 121 — مدیریت محتوا.
 *
 * Both article tables, side by side, which is the whole point of this rewrite.
 * The magazine is served from `articles`; the panel's generic content editor
 * writes `content_entries`. This screen listed only the second, and «مقالات
 * مجله» was linked from nowhere at all — so an operator opened محتوا, saw
 * articles that were not the site's, added one, and it never appeared. The
 * articles the magazine was actually showing were unreachable from the panel.
 *
 * Merged here rather than by making one table serve both: `articles` carries a
 * cover, an excerpt, a category and a featured flag that the generic entry has
 * nowhere to put, and every published URL points at its slug.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const type = first(params.type);

  // The magazine's own articles are skipped when the filter excludes them,
  // rather than fetched and thrown away.
  const wantsArticles = !type || type === 'article';

  const [entries, articles] = await Promise.all([
    getContent({ q: query, kind: type, pageSize: 100 }),
    wantsArticles
      ? getAdminArticles({ q: query, pageSize: 100 })
      : Promise.resolve({ items: [], total: 0, page: 1, pageSize: 0 }),
  ]);

  const rows: ContentRow[] = [
    ...articles.items.map((article) => ({
      key: `article-${article.id}`,
      title: article.title,
      type: 'article' as const,
      status: article.status,
      // The magazine's articles carry a publish date and no author column;
      // the category is what the card in the list is actually filed under, so
      // that is what this shows rather than an empty cell.
      author: article.category,
      updatedAt: article.publishedAt,
      href: `/content/articles/${article.id}`,
      live: true,
    })),
    ...entries.items.map((entry: ContentEntryDto) => ({
      key: `entry-${entry.id}`,
      title: entry.title,
      type: entry.type as ContentEntry['type'],
      status: entry.status,
      author: entry.author,
      updatedAt: entry.updatedAt,
      href: `${editorPath[entry.type as ContentEntry['type']] ?? '/content'}/${entry.id}`,
      live: false,
    })),
  ].sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());

  return (
    <AdminPage
      title="مدیریت محتوا"
      description="مقاله‌های مجله، صفحه‌های ثابت، بنرها و سوالات متداول. مقاله‌ای که «در مجله» است همان چیزی است که مشتری می‌بیند."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'محتوا' }]}
      actions={
        <Link href="/content/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          محتوای جدید
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی عنوان..."
          filters={[
            {
              param: 'type',
              label: 'نوع',
              options: [
                { value: 'article', label: 'مقاله' },
                { value: 'page', label: 'صفحه ثابت' },
                { value: 'banner', label: 'بنر' },
                { value: 'faq', label: 'سوال متداول' },
              ],
            },
          ]}
        />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.key}
        emptyTitle="محتوایی یافت نشد"
        emptyIcon="article"
        actions={(row) => (
          <Link
            href={row.href}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
