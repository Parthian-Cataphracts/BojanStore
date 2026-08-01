import { Card, Icon, cn } from '@bojan/ui';
import type { OrderTimelineStep } from '@/lib/api/types';

/**
 * The five-step fulfilment tracker from screen 13.
 *
 * Horizontal on desktop with the connecting rail behind the dots; stacked
 * vertically on mobile where five labels will not fit side by side.
 */
export function OrderTimeline({ steps }: { steps: OrderTimelineStep[] }) {
  return (
    <Card className="p-lg">
      <ol className="flex flex-col gap-0 md:flex-row md:justify-between">
        {steps.map((step, index) => {
          const done = step.state === 'done';
          const current = step.state === 'current';
          const isLast = index === steps.length - 1;

          return (
            <li
              key={step.id}
              className="relative flex items-center gap-md md:flex-1 md:flex-col md:gap-sm md:text-center"
            >
              {/* Connector: vertical on mobile, horizontal on desktop. */}
              {!isLast && (
                <span
                  aria-hidden="true"
                  className={cn(
                    'absolute bg-outline-variant',
                    'start-[15px] top-8 h-[calc(100%-2rem)] w-0.5',
                    'md:start-auto md:top-4 md:h-0.5 md:w-full md:translate-x-1/2',
                    done && 'bg-primary',
                  )}
                />
              )}

              <span
                className={cn(
                  'relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 transition-colors',
                  done && 'border-primary bg-primary text-on-primary',
                  current && 'border-primary bg-surface text-primary',
                  !done && !current && 'border-outline-variant bg-surface text-outline-variant',
                )}
              >
                {done ? (
                  <Icon name="check" size={18} />
                ) : (
                  <span
                    className={cn(
                      'h-2 w-2 rounded-full',
                      current ? 'bg-primary' : 'bg-outline-variant',
                    )}
                  />
                )}
              </span>

              <span
                className={cn(
                  'py-md text-body-md md:py-0 md:text-caption',
                  done || current ? 'font-label-md text-primary' : 'text-on-surface-variant',
                )}
              >
                {step.label}
              </span>
            </li>
          );
        })}
      </ol>
    </Card>
  );
}
