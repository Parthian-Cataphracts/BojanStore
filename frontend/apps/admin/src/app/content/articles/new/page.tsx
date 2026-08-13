import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { articleSections, articleAsideSections } from '../fields';

export const metadata: Metadata = { title: 'افزودن مقاله' };

/** Screen 123 — افزودن مقاله. Writes `/articles`, the table the magazine reads. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن مقاله"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'مقالات مجله', href: '/content/articles' },
        { label: 'افزودن مقاله' },
      ]}
    >
      <EntityForm
        resource="articles"
        submitLabel="ذخیره مقاله"
        sections={articleSections()}
        asideSections={articleAsideSections()}
      />
    </AdminPage>
  );
}
