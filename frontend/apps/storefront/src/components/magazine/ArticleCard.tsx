import Image from 'next/image';
import Link from 'next/link';
import { Badge, Icon, formatDate, toPersianDigits } from '@bojan/ui';
import type { Article } from '@/lib/api/types';
import { routes } from '@/lib/routes';

/** Article card from screen 28. `featured` renders the wide hero variant. */
export function ArticleCard({ article, featured = false }: { article: Article; featured?: boolean }) {
  return (
    <article
      className={`paper-card group flex overflow-hidden rounded-xl shadow-ambient transition-shadow hover:shadow-soft ${
        featured ? 'flex-col md:flex-row' : 'flex-col'
      }`}
    >
      <Link
        href={routes.article(article.slug)}
        className={`relative block shrink-0 overflow-hidden bg-surface-container-highest ${
          featured ? 'aspect-[16/9] w-full md:aspect-auto md:h-auto md:w-1/2' : 'aspect-[16/10] w-full'
        }`}
      >
        <Image
          src={article.cover}
          alt={article.title}
          fill
          priority={featured}
          sizes={featured ? '(max-width: 768px) 100vw, 50vw' : '(max-width: 768px) 100vw, 33vw'}
          className="object-cover transition-transform duration-500 group-hover:scale-105"
        />
        <span className="absolute end-sm top-sm">
          <Badge tone="mint">{article.category}</Badge>
        </span>
      </Link>

      <div className="flex flex-1 flex-col gap-sm p-lg">
        <h3
          className={`font-headline text-primary ${featured ? 'text-display-md md:text-headline-lg' : 'text-body-lg'}`}
        >
          <Link href={routes.article(article.slug)} className="transition-colors hover:text-secondary">
            {article.title}
          </Link>
        </h3>

        <p
          className={`text-body-md leading-loose text-on-surface-variant ${featured ? '' : 'line-clamp-3'}`}
        >
          {article.excerpt}
        </p>

        <div className="mt-auto flex flex-wrap items-center gap-md pt-sm text-caption text-outline">
          <span className="tabular">{formatDate(article.publishedAt, 'long')}</span>
          <span className="flex items-center gap-xs">
            <Icon name="schedule" size={16} />
            <span className="tabular">{toPersianDigits(article.readingMinutes)} دقیقه</span>
          </span>
        </div>

        {featured && (
          <Link
            href={routes.article(article.slug)}
            className="mt-sm inline-flex items-center gap-xs self-start text-label-md font-label-md text-secondary transition-colors hover:text-primary"
          >
            ادامه مطلب
            <Icon name="arrow_back" size={18} />
          </Link>
        )}
      </div>
    </article>
  );
}
