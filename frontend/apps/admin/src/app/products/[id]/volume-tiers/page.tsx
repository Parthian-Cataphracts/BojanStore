import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { VolumeTierTable } from '@/components/product/VolumeTierTable';
import { getProduct, getProductVolumeTiers } from '@/lib/api/products';

export const metadata: Metadata = { title: 'تخفیف پلکانی سازمانی' };

/** The B2B volume ladder on one product — the rungs a pro-forma is priced from. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const [product, tiers] = await Promise.all([getProduct(id), getProductVolumeTiers(id)]);
  if (!product) notFound();

  return (
    <AdminPage
      title="تخفیف پلکانی سازمانی"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: product.title, href: `/products/${id}` },
        { label: 'تخفیف پلکانی سازمانی' },
      ]}
    >
      {/* The list price is passed down so the preview shows money rather than
          percentages — the figure whoever sets the ladder actually cares about
          is what a carton of a hundred comes to. */}
      <VolumeTierTable productId={id} listPrice={product.price} tiers={tiers} />
    </AdminPage>
  );
}
