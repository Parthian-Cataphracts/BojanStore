import Link from 'next/link';
import { Card, Icon, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import type { ContentPageData } from '@/lib/content/pages';
import { routes } from '@/lib/routes';

/**
 * Shared layout for the informational screens (41-45, 47): intro, a stack of
 * blocks, an optional numbered process, and a support call-out at the end.
 */
export function ContentPage({
  data,
  backHref = routes.home,
  children,
}: {
  data: ContentPageData;
  backHref?: string;
  children?: React.ReactNode;
}) {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title={data.title} backHref={backHref} />

      {/* A shop that opened its page with a heading has no intro paragraph, and
          an empty one leaves a gap under the title with nothing in it. */}
      {data.intro && (
        <p className="max-w-3xl text-body-md leading-loose text-on-surface-variant md:text-body-lg">
          {data.intro}
        </p>
      )}

      {data.blocks.length > 0 && (
        <div className="flex flex-col gap-md">
          {data.blocks.map((block, index) => (
            // Keyed by position as well as title: a section the shop wrote with
            // no heading has no title to key on, and two of them would collide.
            <Card key={block.title ?? index} className="flex flex-col gap-md p-lg">
              {block.title && (
                <h2 className="flex items-center gap-sm font-headline text-display-md text-primary">
                  {block.icon && <Icon name={block.icon} size={22} />}
                  {block.title}
                </h2>
              )}

              {block.body?.map((paragraph) => (
                <p key={paragraph} className="text-body-md leading-loose text-on-surface-variant">
                  {paragraph}
                </p>
              ))}

              {block.items && (
                <ul className="flex flex-col gap-sm">
                  {block.items.map((item) => (
                    <li key={item} className="flex items-start gap-sm">
                      <Icon
                        name="check_circle"
                        size={18}
                        className="mt-1 shrink-0 text-primary-fixed-dim"
                      />
                      <span className="text-body-md leading-loose text-on-surface-variant">
                        {item}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          ))}
        </div>
      )}

      {data.steps && (
        <ol className="flex flex-col gap-md">
          {data.steps.map((step, index) => (
            <li key={step.title}>
              <Card className="flex items-start gap-md p-lg">
                <span className="tabular flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/25 text-label-md font-label-md text-primary-container">
                  {toPersianDigits(index + 1)}
                </span>
                <div className="flex flex-col gap-xs">
                  <h3 className="text-label-md font-label-md text-primary">{step.title}</h3>
                  <p className="text-body-md leading-loose text-on-surface-variant">{step.body}</p>
                </div>
              </Card>
            </li>
          ))}
        </ol>
      )}

      {children}

      {data.footerNote && (
        <Card className="flex flex-col items-center gap-sm p-xl text-center" surface="paper">
          <span className="flex h-14 w-14 items-center justify-center rounded-full bg-soft-mint text-primary">
            <Icon name="headset_mic" size={26} />
          </span>
          <h2 className="font-headline text-display-md text-primary">{data.footerNote.title}</h2>
          <p className="max-w-xl text-body-md leading-loose text-on-surface-variant">
            {data.footerNote.body}
          </p>
          <Link
            href={routes.contact}
            className="mt-sm text-label-md font-label-md text-secondary transition-colors hover:text-primary"
          >
            ارتباط با پشتیبانی
          </Link>
        </Card>
      )}
    </Container>
  );
}
