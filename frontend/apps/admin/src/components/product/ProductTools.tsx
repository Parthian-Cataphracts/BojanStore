import Link from 'next/link';
import { Icon } from '@bojan/ui';

/**
 * The screens that edit one product's detail.
 *
 * Every one of them existed and only <b>variants</b> could be reached: the
 * product form had a single button pointing there, and pricing, SKUs, images,
 * attributes and discounts were addressable by URL and linked from nowhere. An
 * operator could not get to most of the screens the panel was built with.
 *
 * One list, rendered from the product form, so adding a screen means adding a
 * row here rather than remembering to link it from somewhere.
 */
const tools = [
  {
    slug: 'pricing',
    label: 'قیمت‌گذاری',
    icon: 'sell',
    description: 'قیمت فروش، قیمت خرید و قیمت مقایسه‌ای.',
  },
  {
    slug: 'discount',
    label: 'تخفیف',
    icon: 'percent',
    description: 'تخفیف زمان‌دار روی همین محصول.',
  },
  {
    slug: 'volume-tiers',
    label: 'تخفیف پلکانی سازمانی',
    icon: 'stacked_bar_chart',
    description: 'پله‌های خرید عمده — از چند عدد به بعد، چند درصد.',
  },
  {
    slug: 'variants',
    label: 'تنوع محصول',
    icon: 'tune',
    description: 'رنگ، سایز و محورهای دیگر.',
  },
  {
    slug: 'skus',
    label: 'کدهای انبار',
    icon: 'inventory_2',
    description: 'قیمت و موجودی هر ترکیب.',
  },
  {
    slug: 'attributes',
    label: 'ویژگی‌ها',
    icon: 'list_alt',
    description: 'مشخصات فنی و فیلترهای کاتالوگ.',
  },
  {
    slug: 'images',
    label: 'تصاویر',
    icon: 'image',
    description: 'گالری محصول و ترتیب نمایش.',
  },
] as const;

/** Renders nothing until the product exists — every screen here is addressed by id. */
export function ProductTools({ productId }: { productId: string }) {
  return (
    <div className="grid gap-sm sm:grid-cols-2">
      {tools.map((tool) => (
        <Link
          key={tool.slug}
          href={`/products/${productId}/${tool.slug}`}
          className="flex min-w-0 items-start gap-sm rounded-lg border border-outline-variant p-md transition-colors hover:bg-surface-container-low focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        >
          <Icon name={tool.icon} size={22} className="mt-2xs shrink-0 text-primary" />

          <span className="flex min-w-0 flex-col gap-2xs">
            <span className="text-body-md font-medium text-on-surface">{tool.label}</span>
            <span className="text-caption leading-relaxed text-on-surface-variant">
              {tool.description}
            </span>
          </span>
        </Link>
      ))}
    </div>
  );
}
