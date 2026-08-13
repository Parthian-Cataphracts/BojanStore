import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getAdminArticle } from '@/lib/api/content';
import { articleSections, articleAsideSections } from '../fields';

export const metadata: Metadata = { title: 'ویرایش مقاله' };
export const dynamic = 'force-dynamic';

/** Screen 123 — ویرایش مقاله. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const article = await getAdminArticle(id);

  if (!article) notFound();

  return (
    <AdminPage
      title="ویرایش مقاله"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'مقالات مجله', href: '/content/articles' },
        { label: article.title },
      ]}
    >
      <EntityForm
        resource="articles"
        entityId={article.id}
        submitLabel="ذخیره مقاله"
        archive={{ noun: 'مقاله', returnTo: '/content/articles' }}
        sections={articleSections(article)}
        asideSections={articleAsideSections(article)}
      />
    </AdminPage>
  );
}
