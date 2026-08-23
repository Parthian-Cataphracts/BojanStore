import Image from 'next/image';
import Link from 'next/link';
import { Badge, Icon, Rating, formatDate } from '@bojan/ui';
import type { Testimonial } from '@/lib/api/types';
import { routes } from '@/lib/routes';

/**
 * The home page's «نظرات مشتریان» rail.
 *
 * A scrolling row rather than a carousel with its own timer. The design's
 * autoplaying version moves the card a reader is part-way through and takes the
 * page's focus with it; a row that scrolls where it is pushed does the same job
 * for a reader in either direction and costs no JavaScript at all — this is a
 * server component, and it stays one.
 *
 * Each card names the product the review is about and links to it. That is the
 * whole difference between a testimonial and a slogan: the reader can go and
 * read the rest of the reviews on the thing being praised.
 */
export function HomeTestimonials({ testimonials }: { testimonials: Testimonial[] }) {
  return (
    <ul
      className="hide-scrollbar -mx-margin-mobile flex snap-x snap-mandatory gap-gutter overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:grid md:grid-cols-2 md:overflow-visible md:px-0 lg:grid-cols-3"
      /*
        A list, and announced as one. Screen-reader users get "3 items" before
        the first quote rather than discovering the count by arrowing to the
        end of a run of anonymous divs.
      */
    >
      {testimonials.map((testimonial) => (
        <li
          key={testimonial.id}
          className="paper-card flex w-[85vw] shrink-0 snap-start flex-col gap-md rounded-xl p-lg shadow-ambient sm:w-[60vw] md:w-auto"
        >
          <div className="flex items-center justify-between gap-sm">
            <Rating value={testimonial.rating} />
            {testimonial.verified && (
              <Badge tone="mint">
                <span className="flex items-center gap-xs">
                  <Icon name="verified" size={14} />
                  خرید تأییدشده
                </span>
              </Badge>
            )}
          </div>

          {/*
            A real <blockquote>, so the quote is marked up as one rather than
            being a paragraph that happens to sit under a star rating.
          */}
          <blockquote className="line-clamp-5 text-body-md leading-loose text-on-surface">
            {testimonial.body}
          </blockquote>

          <div className="mt-auto flex flex-col gap-sm border-t border-outline-variant/30 pt-md">
            <div className="flex items-baseline justify-between gap-sm">
              <span className="text-label-md font-label-md text-primary-container">
                {testimonial.author}
              </span>
              <span className="tabular text-caption text-outline">
                {formatDate(testimonial.createdAt, 'long')}
              </span>
            </div>

            <Link
              href={routes.product(testimonial.productSlug)}
              className="group flex items-center gap-sm rounded-lg bg-surface-container-lowest p-sm transition-colors hover:bg-soft-mint/40"
            >
              <span className="relative h-12 w-12 shrink-0 overflow-hidden rounded-md bg-surface-container-highest">
                <Image
                  src={testimonial.productImage}
                  alt=""
                  fill
                  sizes="48px"
                  className="object-cover"
                />
              </span>
              <span className="line-clamp-2 flex-1 text-caption leading-relaxed text-on-surface-variant transition-colors group-hover:text-primary">
                {testimonial.productTitle}
              </span>
              <Icon
                name="chevron_left"
                size={18}
                className="shrink-0 text-outline transition-transform group-hover:-translate-x-xs"
              />
            </Link>
          </div>
        </li>
      ))}
    </ul>
  );
}
