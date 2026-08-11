import type { Metadata } from 'next';
import { Card } from '@bojan/ui';
import { ContentPage } from '@/components/content/ContentPage';
import { canvasSizes, notebookSizes, sizeGuidePage } from '@/lib/content/pages';
import { getContentPage, pageSlugs } from '@/lib/api/pages';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'راهنمای سایز و مشخصات محصولات',
  description: 'ابعاد دفتر، پلنر و بوم نقاشی و مشخصات کاغذ و صحافی محصولات بوژان.',
};

function SizeTable({
  caption,
  rows,
}: {
  caption: string;
  rows: { name: string; dimensions: string; use: string }[];
}) {
  return (
    <Card surface="plain" className="overflow-hidden">
      <h2 className="border-b border-outline-variant/40 px-lg py-md font-headline text-display-md text-primary">
        {caption}
      </h2>

      {/* Wide content scrolls inside its own container rather than the page. */}
      <div className="overflow-x-auto">
        <table className="w-full min-w-[480px] text-start text-body-md">
          <thead className="bg-surface-container-low text-caption text-on-surface-variant">
            <tr>
              <th scope="col" className="px-lg py-sm text-start">
                سایز
              </th>
              <th scope="col" className="px-lg py-sm text-start">
                ابعاد
              </th>
              <th scope="col" className="px-lg py-sm text-start">
                مناسب برای
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.name} className="border-t border-outline-variant/30">
                <td className="px-lg py-md font-label-md text-primary">{row.name}</td>
                <td className="tabular px-lg py-md text-on-surface">{row.dimensions}</td>
                <td className="px-lg py-md text-on-surface-variant">{row.use}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

/** Screen 47 — Size and specification guide. */
export default async function SizeGuidePage() {
  // The prose is the shop's to rewrite; the two tables below are product
  // specifications rather than copy, so they stay where they are.
  const data = await getContentPage(pageSlugs.sizeGuide, sizeGuidePage);

  return (
    <ContentPage data={data} backHref={routes.buyingGuide}>
      <SizeTable caption="دفتر و پلنر" rows={notebookSizes} />
      <SizeTable caption="بوم نقاشی" rows={canvasSizes} />
    </ContentPage>
  );
}
