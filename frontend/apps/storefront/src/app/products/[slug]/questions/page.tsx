import type { Metadata } from 'next';
import Image from 'next/image';
import { notFound } from 'next/navigation';
import { Badge, Card, Icon, Rating, formatDate } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { AskQuestionForm } from '@/components/product/AskQuestionForm';
import { getProduct, getProductQuestions } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

/*
 * Rendered on request, not at build.
 *
 * This page reads the catalogue, and the catalogue lives behind the API — which
 * does not exist when the image is built. Prerendering it meant `next build`
 * fetching from a host that is not up yet, which is exactly how the Docker
 * build failed. The alternative, emitting it with whatever an unreachable API
 * returns, is worse: the first visitors after a deploy would be served an empty
 * shop until the first revalidation filled it in.
 *
 * Nothing is lost by it. The fetches underneath already declare their own
 * `revalidate` window, so the API is not called per request either way — the
 * caching just happens a layer down, where stock and prices can expire on their
 * own schedule instead of being frozen into the image.
 */
export const dynamic = 'force-dynamic';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProduct(slug);
  return { title: product ? `پرسش و پاسخ ${product.title}` : 'پرسش و پاسخ محصول' };
}

/** Screen 85 — Product questions and answers. */
export default async function ProductQuestionsPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const [product, questions] = await Promise.all([getProduct(slug), getProductQuestions(slug)]);
  if (!product) notFound();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="پرسش و پاسخ محصول" backHref={routes.product(slug)} />

      <Card className="flex items-center gap-md p-lg">
        <span className="relative h-16 w-16 shrink-0 overflow-hidden rounded-lg border border-outline-variant">
          <Image src={product.image} alt={product.title} fill sizes="64px" className="object-cover" />
        </span>
        <div className="flex min-w-0 flex-col gap-xs">
          <h2 className="line-clamp-2 text-body-lg font-semibold text-primary">{product.title}</h2>
          <span className="text-caption text-on-surface-variant">{product.brand}</span>
          <Rating value={product.rating} count={product.reviewCount} compact />
        </div>
      </Card>

      <AskQuestionForm productSlug={product.slug} />

      <ul className="flex flex-col gap-md">
        {questions.map((item) => (
          <li key={item.id}>
            <Card className="flex flex-col gap-md p-lg">
              <div className="flex flex-col gap-xs">
                <span className="flex flex-wrap items-center gap-sm">
                  <Icon name="help" size={20} className="text-primary" />
                  <span className="text-body-md font-medium text-on-surface">{item.question}</span>
                </span>
                <span className="tabular ps-7 text-caption text-outline">
                  {item.author} · {formatDate(item.askedAt, 'long')}
                </span>
              </div>

              {item.answer ? (
                <div className="flex flex-col gap-xs rounded-lg bg-soft-mint/40 p-md">
                  <span className="flex items-center gap-sm">
                    <Icon name="support_agent" size={20} className="text-primary" />
                    <span className="text-label-md font-semibold text-primary">
                      {item.answer.author}
                    </span>
                  </span>
                  <p className="text-body-md leading-loose text-on-surface-variant">
                    {item.answer.body}
                  </p>
                  <span className="tabular text-caption text-outline">
                    {formatDate(item.answer.answeredAt, 'long')}
                  </span>
                </div>
              ) : (
                <Badge tone="warning" className="self-start">
                  در انتظار پاسخ
                </Badge>
              )}
            </Card>
          </li>
        ))}
      </ul>
    </Container>
  );
}
