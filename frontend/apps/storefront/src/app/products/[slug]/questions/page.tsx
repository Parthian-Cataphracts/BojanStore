import type { Metadata } from 'next';
import Image from 'next/image';
import { notFound } from 'next/navigation';
import { Badge, Card, Icon, Rating, formatDate } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { AskQuestionForm } from '@/components/product/AskQuestionForm';
import { getProduct } from '@/lib/api/catalog';
import { mockProductQuestions } from '@/lib/mock/product-detail';
import { routes } from '@/lib/routes';

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
  const product = await getProduct(slug);
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
        {mockProductQuestions.map((item) => (
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
