import { cn } from '../lib/cn';

export interface SkeletonProps {
  className?: string;
  /** Renders a pill instead of a rounded rectangle. */
  circle?: boolean;
}

export function Skeleton({ className, circle = false }: SkeletonProps) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'animate-pulse bg-surface-container-high',
        circle ? 'rounded-full' : 'rounded-lg',
        className,
      )}
    />
  );
}

/** Placeholder matching the storefront's product card footprint. */
export function ProductCardSkeleton() {
  return (
    <div className="flex flex-col gap-sm">
      <Skeleton className="aspect-square w-full rounded-xl" />
      <Skeleton className="h-4 w-3/4" />
      <Skeleton className="h-4 w-1/2" />
    </div>
  );
}
