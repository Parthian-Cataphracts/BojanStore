import Link from 'next/link';
import { Icon, cn } from '@bojan/ui';

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

const tileClasses =
  'flex w-full min-w-0 items-start gap-sm rounded-lg border border-outline-variant p-md text-start transition-colors hover:bg-surface-container-low focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-60';

/**
 * The seven screens that edit one product's detail.
 *
 * Each is addressed by the product's id, so on a product that already exists
 * a tile is an ordinary link. On the create form there is no id yet — and the
 * answer is not to grey the list out and tell the operator to come back after
 * saving, because filling in the basics and going straight on to the variants
 * is the order the work happens in. There the tile calls `onOpen`, which saves
 * the product and then opens the screen, so the id arrives without the
 * operator having to think about it.
 */
export function ProductTools({
  productId,
  onOpen,
  opening,
  busy,
}: {
  productId?: string;
  /** Create form only: save the product, then open this screen. */
  onOpen?: (slug: string) => void;
  /** The one tile mid-save, so the spinner sits on the tile that was clicked. */
  opening?: string | null;
  /** Any save in flight — the others go quiet rather than queueing a second one. */
  busy?: boolean;
}) {
  return (
    <div className="grid gap-sm sm:grid-cols-2">
      {tools.map((tool) => {
        const face = (
          <>
            <Icon
              name={opening === tool.slug ? 'progress_activity' : tool.icon}
              size={22}
              className={cn(
                'mt-2xs shrink-0 text-primary',
                opening === tool.slug && 'animate-spin',
              )}
            />

            <span className="flex min-w-0 flex-col gap-2xs">
              <span className="text-body-md font-medium text-on-surface">{tool.label}</span>
              <span className="text-caption leading-relaxed text-on-surface-variant">
                {tool.description}
              </span>
            </span>
          </>
        );

        if (productId) {
          return (
            <Link key={tool.slug} href={`/products/${productId}/${tool.slug}`} className={tileClasses}>
              {face}
            </Link>
          );
        }

        // `type="button"` matters more here than anywhere else in this form:
        // the default is `submit`, and a tile that submitted the form would
        // save the product and then go nowhere — the exact behaviour this
        // replaces.
        return (
          <button
            key={tool.slug}
            type="button"
            disabled={busy}
            onClick={() => onOpen?.(tool.slug)}
            className={tileClasses}
          >
            {face}
          </button>
        );
      })}
    </div>
  );
}
