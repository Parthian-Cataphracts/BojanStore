'use client';

import { useState, type ReactNode } from 'react';
import Link from 'next/link';
import { Badge, Icon, Rating, Tabs, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { routes } from '@/lib/routes';
import type { ProductQuestion, ProductReview, RatingBreakdown } from '@/lib/api/types';

/**
 * Everything about the product that is not the price and the button.
 *
 * It used to be four link tiles and three stacked accordions crammed into the
 * column beside the gallery — a half-width strip holding a paragraph of prose,
 * a specification table and a shipping policy, each collapsed to a summary row,
 * so reading any of them meant opening one and pushing the next two down the
 * page. The gallery meanwhile ended and left the whole other half of the screen
 * empty for the rest of the scroll.
 *
 * So it moved out from beside the image to underneath it, and the width it
 * gained is the point: a description gets a full measure to be read at,
 * specifications get two columns instead of one squeezed one, and reviews sit
 * beside their own score summary rather than behind a link.
 *
 * One panel shows at a time, but every panel is rendered — hidden with the
 * `hidden` attribute rather than dropped from the tree. Tabs that mount their
 * content on click put the description, the specifications and the reviews
 * behind a click a crawler never makes, which for a shop means the page
 * disappearing from search except for its title.
 */

export interface ProductTabsProps {
  slug: string;
  description: string;
  specs: { label: string; value: string }[];
  reviews: ProductReview[];
  breakdown: RatingBreakdown;
  questions: ProductQuestion[];
  /** The promises the shop actually keeps, read from its own settings. */
  shipping: {
    deliveryEstimate: string;
    returnWindowDays: number;
    freeShippingLabel: string | null;
  };
}

/** How many rows a tab shows before it stops and points at the full screen. */
const PREVIEW = 3;

export function ProductTabs({
  slug,
  description,
  specs,
  reviews,
  breakdown,
  questions,
  shipping,
}: ProductTabsProps) {
  const [active, setActive] = useState('about');

  const items = [
    { id: 'about', label: 'درباره محصول' },
    { id: 'specs', label: 'مشخصات محصول' },
    { id: 'reviews', label: 'نظرات کاربران', count: breakdown.total },
    { id: 'questions', label: 'پرسش و پاسخ', count: questions.length },
    { id: 'shipping', label: 'ارسال و مرجوعی' },
  ];

  const panel = (id: string) => ({ id, active });

  return (
    <section className="paper-card shadow-ambient overflow-hidden rounded-xl">
      <Tabs
        items={items}
        value={active}
        onChange={setActive}
        variant="underline"
        idBase="product"
        className="px-md md:px-lg"
      />

      <div className="p-lg md:p-xl">
        {/* درباره محصول */}
        <Panel {...panel('about')} className="gap-xl grid lg:grid-cols-[1fr_320px]">
          <div className="gap-md flex flex-col">
            <h2 className="font-headline text-card-title text-primary">درباره این محصول</h2>
            {/* The measure is capped on the paragraph, not on the panel: a line
                of Persian is tiring to read much past 70 characters, and the
                panel is now wide enough to produce far more than that. */}
            <p className="text-body-lg text-on-surface-variant max-w-[68ch] leading-loose">
              {description}
            </p>
          </div>

          {/* The few facts somebody checks before reading the prose — beside it
              rather than under it, which is what the width bought. */}
          <aside className="paper-card gap-sm bg-soft-mint/30 p-lg flex h-fit flex-col rounded-lg">
            <h3 className="text-label-md text-primary">در یک نگاه</h3>
            <dl className="gap-sm flex flex-col">
              {specs.slice(0, 4).map((spec) => (
                <div key={spec.label} className="gap-md flex items-baseline justify-between">
                  <dt className="text-caption text-on-surface-variant shrink-0">{spec.label}</dt>
                  <dd className="text-body-md text-on-surface text-end">{spec.value}</dd>
                </div>
              ))}
            </dl>
            {specs.length > 4 && (
              <button
                type="button"
                onClick={() => setActive('specs')}
                className="text-caption text-primary gap-xs mt-xs flex items-center self-start hover:underline"
              >
                همه‌ی مشخصات
                <Icon name="chevron_left" size={16} />
              </button>
            )}
          </aside>
        </Panel>

        {/* مشخصات محصول */}
        <Panel {...panel('specs')} className="gap-md flex flex-col">
          <h2 className="font-headline text-card-title text-primary">مشخصات محصول</h2>
          {/* Two columns of rows rather than one long list. Each row keeps its
              own rule, so a value still lines up with its label across the gap. */}
          <dl className="gap-x-xl grid sm:grid-cols-2">
            {specs.map((spec) => (
              <div
                key={spec.label}
                className="gap-md border-paper-border py-sm flex items-baseline justify-between border-b"
              >
                <dt className="text-caption text-on-surface-variant shrink-0">{spec.label}</dt>
                <dd className="text-body-md text-on-surface text-end">{spec.value}</dd>
              </div>
            ))}
          </dl>
        </Panel>

        {/* نظرات کاربران */}
        {/* The breakdown column only exists once there is something to break
            down. A five-bar chart of zeros beside «هنوز نظری ثبت نشده» says the
            same nothing twice and takes a third of the width to do it. */}
        <Panel
          {...panel('reviews')}
          className={
            breakdown.total > 0 ? 'gap-xl grid lg:grid-cols-[280px_1fr]' : 'gap-md flex flex-col'
          }
        >
          {breakdown.total > 0 && <ScoreSummary breakdown={breakdown} slug={slug} />}

          <div className="gap-md flex flex-col">
            {reviews.length === 0 ? (
              <>
                <Empty
                  icon="reviews"
                  title="هنوز نظری ثبت نشده"
                  body="اولین نفری باشید که تجربه‌اش را از این محصول می‌نویسد."
                />
                <Link
                  href={routes.writeReview(slug)}
                  className={buttonClasses({ className: 'gap-xs self-center' })}
                >
                  <Icon name="edit" size={18} />
                  ثبت نظر
                </Link>
              </>
            ) : (
              <>
                <ul className="gap-md flex flex-col">
                  {reviews.slice(0, PREVIEW).map((review) => (
                    <li key={review.id} className="paper-card gap-sm p-lg flex flex-col rounded-lg">
                      <div className="gap-sm flex flex-wrap items-center justify-between">
                        <span className="gap-sm flex items-center">
                          <span className="bg-soft-mint text-label-md text-primary flex h-10 w-10 items-center justify-center rounded-full">
                            {review.author.charAt(0)}
                          </span>
                          <span className="gap-xs flex flex-col">
                            <span className="text-body-md text-on-surface font-medium">
                              {review.author}
                            </span>
                            <Rating value={review.rating} size={14} />
                          </span>
                        </span>
                        {review.verified && <Badge tone="mint">خرید تاییدشده</Badge>}
                      </div>

                      <p className="text-body-md text-on-surface-variant leading-loose">
                        {review.body}
                      </p>

                      <span className="tabular text-caption text-outline">
                        {formatDate(review.createdAt, 'long')}
                      </span>
                    </li>
                  ))}
                </ul>

                {reviews.length > PREVIEW && (
                  <MoreLink
                    href={routes.productReviews(slug)}
                    label={`مشاهده‌ی همه‌ی ${toPersianDigits(breakdown.total)} نظر`}
                  />
                )}
              </>
            )}
          </div>
        </Panel>

        {/* پرسش و پاسخ */}
        <Panel {...panel('questions')} className="gap-md flex flex-col">
          <div className="gap-md flex flex-wrap items-center justify-between">
            <h2 className="font-headline text-card-title text-primary">پرسش و پاسخ</h2>
            <Link
              href={routes.productQuestions(slug)}
              className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
            >
              <Icon name="help" size={18} />
              پرسیدن سؤال
            </Link>
          </div>

          {questions.length === 0 ? (
            <Empty
              icon="help"
              title="هنوز سؤالی پرسیده نشده"
              body="اگر چیزی درباره‌ی این محصول برایتان روشن نیست بپرسید — پاسخ همین‌جا منتشر می‌شود."
            />
          ) : (
            <>
              <ul className="gap-md flex flex-col">
                {questions.slice(0, PREVIEW).map((item) => (
                  <li key={item.id} className="paper-card gap-md p-lg flex flex-col rounded-lg">
                    <div className="gap-xs flex flex-col">
                      <span className="gap-sm flex flex-wrap items-center">
                        <Icon name="help" size={20} className="text-primary" />
                        <span className="text-body-md text-on-surface font-medium">
                          {item.question}
                        </span>
                      </span>
                      <span className="tabular text-caption text-outline ps-7">
                        {item.author} · {formatDate(item.askedAt, 'long')}
                      </span>
                    </div>

                    {item.answer ? (
                      <div className="gap-xs bg-soft-mint/40 p-md flex flex-col rounded-lg">
                        <span className="gap-sm flex items-center">
                          <Icon name="support_agent" size={20} className="text-primary" />
                          <span className="text-label-md text-primary">{item.answer.author}</span>
                        </span>
                        <p className="text-body-md text-on-surface-variant leading-loose">
                          {item.answer.body}
                        </p>
                      </div>
                    ) : (
                      <Badge tone="warning" className="self-start">
                        در انتظار پاسخ
                      </Badge>
                    )}
                  </li>
                ))}
              </ul>

              {questions.length > PREVIEW && (
                <MoreLink
                  href={routes.productQuestions(slug)}
                  label={`مشاهده‌ی همه‌ی ${toPersianDigits(questions.length)} پرسش`}
                />
              )}
            </>
          )}
        </Panel>

        {/* ارسال و مرجوعی */}
        <Panel {...panel('shipping')} className="gap-md flex flex-col">
          <h2 className="font-headline text-card-title text-primary">ارسال و مرجوعی</h2>

          {/*
            The figures come from the settings screen rather than being written
            here. They were «۲ تا ۵ روز» and «۷ روز» in this file, quoted
            differently again on the policy pages and in the FAQ — and a shop
            that answers the same question three ways is wrong twice.
          */}
          <ul className="gap-md grid sm:grid-cols-2 lg:grid-cols-4">
            <Promise
              icon="local_shipping"
              title="ارسال به سراسر ایران"
              body={`تحویل ${shipping.deliveryEstimate}`}
            />
            <Promise
              icon="assignment_return"
              title="مهلت مرجوعی"
              body={`تا ${toPersianDigits(shipping.returnWindowDays)} روز پس از تحویل`}
            />
            {shipping.freeShippingLabel !== null && (
              <Promise icon="local_mall" title="ارسال رایگان" body={shipping.freeShippingLabel} />
            )}
            <Promise
              icon="verified"
              title="ضمانت اصالت کالا"
              body="بررسی‌شده و تأییدشده توسط بوژان"
            />
          </ul>
        </Panel>
      </div>
    </section>
  );
}

/**
 * One tab's content, shown or hidden.
 *
 * The `hidden` attribute sits on a wrapper with no `display` of its own, and
 * the layout classes go on the element inside it. Putting both on one element
 * does not work: `hidden` is `display: none` from an attribute selector and a
 * layout utility is `display: grid` from a class of equal specificity, so the
 * utility wins on source order and every panel renders at once — which is
 * precisely what this page did until the wrapper appeared.
 */
function Panel({
  id,
  active,
  className,
  children,
}: {
  id: string;
  active: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <div
      role="tabpanel"
      id={`product-panel-${id}`}
      aria-labelledby={`product-tab-${id}`}
      hidden={id !== active}
    >
      <div className={className}>{children}</div>
    </div>
  );
}

/** The star breakdown, beside the reviews rather than on a screen of its own. */
function ScoreSummary({ breakdown, slug }: { breakdown: RatingBreakdown; slug: string }) {
  const stars = [5, 4, 3, 2, 1] as const;

  return (
    <div className="paper-card gap-md bg-soft-mint/30 p-lg flex h-fit flex-col rounded-lg">
      <div className="gap-xs flex flex-col items-center">
        <span className="tabular font-headline text-hero-mobile text-primary md:text-page-title">
          {toPersianDigits(breakdown.average)}
        </span>
        <Rating value={breakdown.average} />
        <span className="tabular text-caption text-on-surface-variant">
          از {toPersianDigits(breakdown.total)} نظر
        </span>
      </div>

      <div className="gap-sm flex flex-col">
        {stars.map((star) => {
          const count = breakdown.counts[star];
          const percent = breakdown.total > 0 ? Math.round((count / breakdown.total) * 100) : 0;
          return (
            <div key={star} className="gap-sm flex items-center">
              <span className="tabular text-caption text-on-surface-variant w-4 shrink-0">
                {toPersianDigits(star)}
              </span>
              <Icon name="star" filled size={14} className="text-secondary-container shrink-0" />
              <span
                className="bg-surface-container-high h-2 flex-1 overflow-hidden rounded-full"
                role="img"
                aria-label={`${toPersianDigits(percent)}٪`}
              >
                <span
                  className="bg-secondary-container block h-full rounded-full"
                  style={{ width: `${percent}%` }}
                />
              </span>
              <span className="tabular text-caption text-outline w-8 shrink-0">
                {toPersianDigits(count)}
              </span>
            </div>
          );
        })}
      </div>

      <Link
        href={routes.writeReview(slug)}
        className={buttonClasses({ size: 'sm', fullWidth: true, className: 'gap-xs' })}
      >
        <Icon name="edit" size={18} />
        ثبت نظر
      </Link>
    </div>
  );
}

function Promise({ icon, title, body }: { icon: string; title: string; body: string }) {
  return (
    <li className="paper-card gap-sm p-lg flex flex-col items-center rounded-lg text-center">
      <span className="bg-soft-mint text-primary flex h-12 w-12 items-center justify-center rounded-full">
        <Icon name={icon} size={24} />
      </span>
      <span className="text-label-md text-primary">{title}</span>
      <span className="text-caption text-on-surface-variant leading-relaxed">{body}</span>
    </li>
  );
}

function Empty({ icon, title, body }: { icon: string; title: string; body: string }) {
  return (
    <div className="gap-xs border-paper-border p-xl flex flex-col items-center rounded-lg border border-dashed text-center">
      <Icon name={icon} size={32} className="text-outline-variant" />
      <span className="text-body-md text-on-surface font-medium">{title}</span>
      <span className="text-caption text-on-surface-variant max-w-[46ch] leading-relaxed">
        {body}
      </span>
    </div>
  );
}

function MoreLink({ href, label }: { href: string; label: string }) {
  return (
    <Link
      href={href}
      className={buttonClasses({ variant: 'outline', className: 'gap-xs self-start' })}
    >
      {label}
      <Icon name="chevron_left" size={18} />
    </Link>
  );
}
