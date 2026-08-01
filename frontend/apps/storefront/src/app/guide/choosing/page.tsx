import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ProductRail } from '@/components/product/ProductGrid';
import { getBestsellers } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'راهنمای انتخاب محصول',
  description:
    'بر اساس کاربرد انتخاب کنید: مدرسه و دانشگاه، طراحی، هدیه یا دفتر کار — راهنمای انتخاب محصول بوژان.',
};

/** The occasion tiles from screen 46; each opens a pre-filtered listing. */
const occasions = [
  {
    title: 'برای مدرسه و دانشگاه',
    description: 'دفتر، پلنر و نوشت‌افزار مقاوم برای استفاده روزمره',
    icon: 'school',
    href: `${routes.products}?category=notebooks`,
  },
  {
    title: 'برای طراحی',
    description: 'آبرنگ، قلم‌مو، بوم و دفتر طراحی با کاغذ مناسب',
    icon: 'draw',
    href: `${routes.products}?category=art-tools`,
  },
  {
    title: 'برای هدیه',
    description: 'اقلام خاص و بسته‌بندی‌شده برای هدیه دادن',
    icon: 'card_giftcard',
    href: `${routes.products}?category=gift-lifestyle`,
  },
  {
    title: 'برای دفتر کار',
    description: 'ارگانایزر، خودنویس و لوازم رومیزی مینیمال',
    icon: 'business_center',
    href: `${routes.products}?category=stationery`,
  },
];

/** Screen 46 — Product selection guide. */
export default async function ChoosingGuidePage() {
  const picks = await getBestsellers(8);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <PageHeader
        title="راهنمای انتخاب محصول"
        backHref={routes.buyingGuide}
        subtitle="بر اساس اینکه محصول را برای چه می‌خواهید، انتخاب کنید."
      />

      <section className="grid gap-md md:grid-cols-2">
        {occasions.map((occasion) => (
          <Link key={occasion.title} href={occasion.href} className="group block">
            <Card className="flex h-full items-start gap-md p-lg transition-shadow group-hover:shadow-soft">
              <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container transition-transform group-hover:scale-110">
                <Icon name={occasion.icon} size={26} />
              </span>

              <div className="flex min-w-0 flex-1 flex-col gap-xs">
                <h2 className="text-body-lg font-label-md text-primary-container">
                  {occasion.title}
                </h2>
                <p className="text-body-md leading-relaxed text-on-surface-variant">
                  {occasion.description}
                </p>
              </div>

              <Icon
                name="chevron_left"
                size={20}
                className="mt-1 shrink-0 text-outline transition-colors group-hover:text-primary"
              />
            </Card>
          </Link>
        ))}
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader
          title="پیشنهادهای سردبیر"
          subtitle="انتخاب تیم بوژان از میان محصولات پرطرفدار"
        />
        <ProductRail products={picks} />
      </section>

      <Link
        href={routes.sizeGuide}
        className="paper-card flex items-center justify-between gap-md rounded-lg p-lg transition-shadow hover:shadow-soft"
      >
        <span className="flex items-center gap-sm text-label-md font-label-md text-primary">
          <Icon name="straighten" size={20} />
          ابعاد و مشخصات دقیق را ببینید
        </span>
        <Icon name="chevron_left" size={20} className="text-outline" />
      </Link>
    </Container>
  );
}
