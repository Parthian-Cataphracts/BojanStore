'use client';

import { useEffect, useState } from 'react';
import { Card, Icon, cn } from '@bojan/ui';
import { deliverySlots } from '@/lib/mock/checkout';
import { useCheckout } from '@/lib/checkout/store';

export interface DeliveryDay {
  id: string;
  weekday: string;
  day: string;
  month: string;
}

/**
 * Screen 74 — day rail plus time-window cards.
 * The day strip scrolls horizontally on mobile, exactly as drawn.
 */
export function DeliveryTimePicker({ days }: { days: DeliveryDay[] }) {
  const { select } = useCheckout();

  const [day, setDay] = useState(days[0]?.id ?? '');
  const [slot, setSlot] = useState(deliverySlots[0]!.id);

  // The window travels with the order as one formatted line. Both halves used
  // to live only in this component, so whatever the shopper picked here was
  // discarded on the way to the next step and never reached the order at all.
  useEffect(() => {
    const chosenDay = days.find((option) => option.id === day);
    const chosenSlot = deliverySlots.find((option) => option.id === slot);
    if (!chosenDay || !chosenSlot) return;

    select({
      deliveryWindow: `${chosenDay.weekday} ${chosenDay.day} ${chosenDay.month}، ${chosenSlot.range}`,
    });
  }, [day, slot, days, select]);

  return (
    <div className="flex flex-col gap-lg">
      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-card-title text-primary">روز تحویل</h2>

        <div
          role="radiogroup"
          aria-label="روز تحویل"
          className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
        >
          {days.map((option) => {
            const active = day === option.id;
            return (
              <button
                key={option.id}
                type="button"
                role="radio"
                aria-checked={active}
                onClick={() => setDay(option.id)}
                className={cn(
                  'flex w-20 shrink-0 flex-col items-center gap-xs rounded-lg border px-sm py-md transition-colors',
                  active
                    ? 'border-primary bg-soft-mint/40 text-primary'
                    : 'border-outline-variant bg-surface-container-lowest text-on-surface-variant hover:bg-surface-container-low',
                )}
              >
                <span className="text-caption">{option.weekday}</span>
                <span className="tabular text-card-title font-semibold">{option.day}</span>
                <span className="text-caption">{option.month}</span>
              </button>
            );
          })}
        </div>
      </section>

      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-card-title text-primary">بازه زمانی</h2>

        <div role="radiogroup" aria-label="بازه زمانی" className="grid gap-md sm:grid-cols-3">
          {deliverySlots.map((option) => {
            const active = slot === option.id;
            return (
              <button
                key={option.id}
                type="button"
                role="radio"
                aria-checked={active}
                onClick={() => setSlot(option.id)}
                className={cn(
                  'flex items-center justify-between gap-sm rounded-lg border p-md transition-colors',
                  active
                    ? 'border-primary bg-soft-mint/30'
                    : 'border-outline-variant hover:bg-surface-container-low',
                )}
              >
                <span className="flex flex-col items-start gap-xs">
                  <span className="text-body-md font-medium text-on-surface">{option.label}</span>
                  <span className="tabular text-caption text-on-surface-variant">
                    {option.range}
                  </span>
                </span>
                <Icon
                  name={active ? 'check_circle' : 'radio_button_unchecked'}
                  size={22}
                  className={active ? 'text-primary' : 'text-outline-variant'}
                />
              </button>
            );
          })}
        </div>
      </section>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          در صورت عدم حضور در بازه انتخابی، پیک تا پایان همان روز مجدداً تماس می‌گیرد.
        </p>
      </Card>
    </div>
  );
}
