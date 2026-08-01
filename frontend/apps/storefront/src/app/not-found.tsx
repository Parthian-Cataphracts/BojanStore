import Link from 'next/link';
import { EmptyState, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { routes } from '@/lib/routes';

/** Screen 38 — Not found. */
export default function NotFound() {
  return (
    <Container className="flex min-h-[60vh] items-center justify-center py-xl">
      <EmptyState
        icon="explore_off"
        title="این صفحه پیدا نشد"
        description="ممکن است آدرس اشتباه باشد یا صفحه حذف شده باشد. از خانه شروع کنید."
        action={
          <Link href={routes.home} className={buttonClasses()}>
            بازگشت به خانه
          </Link>
        }
      />
    </Container>
  );
}
