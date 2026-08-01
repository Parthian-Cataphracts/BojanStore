import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'درباره بوژان',
  description:
    'بوژان برای لحظه‌های خلاق زندگی ساخته شده؛ انتخاب دقیق نوشت‌افزار، دفتر، ابزار هنری و هدیه با طراحی مینیمال.',
};

const values = [
  { icon: 'auto_awesome', label: 'انتخاب دقیق محصولات' },
  { icon: 'shopping_basket', label: 'تجربه خرید ساده' },
  { icon: 'palette', label: 'کیفیت و زیبایی' },
  { icon: 'draw', label: 'مناسب برای زندگی خلاق' },
];

const storyImage =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuD0bLSDR3LG058UKuwpzAND3hPxtrZl_fnR9OZMvPFbuWfchZT2nIFuMkLMa1g8FavBFsmow9lF50Q4CI2FrnweZ_0-jKLI_VdNNpprV-Of3tCqQh-DseezhHVCRzCZo5r0DgdXGNeYfiliw5K46ogWF3aPb_GBUQSrTmrrDCOjZPk0C-kFwSfmXLCUD33gKy5fiDCuYkMc5XPXv5zwDAo-c3njSZJ90ZOqlOiZCHByrMkn5oGvXzNfPofx8mlN4avRWvKIzOcrXfY';

/** Screen 17 — About Bojan. */
export default function AboutPage() {
  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      {/* Editorial hero */}
      <section className="flex flex-col gap-md text-center">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-xl">
          درباره بوژان
        </h1>
        <p className="mx-auto max-w-2xl text-body-md leading-loose text-on-surface-variant md:text-body-lg">
          بوژان برای لحظه‌های خلاق زندگی ساخته شده — جایی برای پیدا کردن ابزارهایی که کار کردن با
          آن‌ها لذت‌بخش است.
        </p>
      </section>

      {/* Brand story */}
      <section className="grid items-center gap-lg md:grid-cols-2">
        <div className="flex flex-col gap-md">
          <h2 className="font-headline text-display-md text-primary md:text-headline-lg">
            ما به انتخاب‌های ساده، زیبا و کاربردی باور داریم
          </h2>
          <p className="text-body-md leading-loose text-on-surface-variant">
            در دنیای پرهیاهوی امروز، بوژان پناهگاهی است برای یافتن زیبایی در سادگی. ما با دقت و
            وسواس، محصولاتی را انتخاب می‌کنیم که نه تنها کاربردی هستند، بلکه با طراحی مینیمال و هنری
            خود، حس آرامش و اصالت را به فضای زندگی شما می‌بخشند.
          </p>
        </div>

        <div className="paper-card relative aspect-[4/3] overflow-hidden rounded-xl shadow-soft">
          <Image
            src={storyImage}
            alt="چیدمان ابزارهای هنری و دفترهای بوژان روی میز کار"
            fill
            sizes="(max-width: 768px) 100vw, 50vw"
            className="object-cover"
          />
        </div>
      </section>

      {/* Values */}
      <section className="grid grid-cols-2 gap-gutter md:grid-cols-4">
        {values.map((value) => (
          <Card
            key={value.label}
            className="flex flex-col items-center gap-sm p-lg text-center transition-shadow hover:shadow-soft"
          >
            <span className="flex h-14 w-14 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
              <Icon name={value.icon} size={26} />
            </span>
            <span className="text-label-md font-label-md text-primary-container">{value.label}</span>
          </Card>
        ))}
      </section>

      {/* CTA */}
      <section className="flex flex-col items-center gap-md">
        <Link href={routes.products} className={buttonClasses({ size: 'lg', className: 'gap-sm' })}>
          مشاهده محصولات
          <Icon name="arrow_back" size={20} />
        </Link>

        <Link
          href={routes.contact}
          className="text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
        >
          سوالی دارید؟ با ما در ارتباط باشید
        </Link>
      </section>
    </Container>
  );
}
