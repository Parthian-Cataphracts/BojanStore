'use client';

import { useState } from 'react';
import { Card, Icon, cn } from '@bojan/ui';
import type { Faq } from '@/lib/api/pages';

/**
 * The home page's «سوالات متداول» accordion.
 *
 * A handful of questions, not the whole FAQ — the caller slices, and the page
 * links to `/faq` for the rest. The search box and category chips that make
 * `FaqList` worth its weight on a page of forty questions are noise on a
 * section of five, so this is the accordion alone.
 *
 * Nothing is open to begin with, unlike the full page. On `/faq` the first
 * answer is the point of arriving; here it would push the sections below it
 * down by a paragraph the reader did not ask for, on the page most likely to
 * be measured for layout shift.
 */
export function HomeFaq({ items }: { items: Faq[] }) {
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  return (
    <div className="flex flex-col gap-sm">
      {items.map((faq, index) => {
        const open = openIndex === index;
        const answerId = `home-faq-answer-${index}`;

        return (
          <Card key={faq.question} className="overflow-hidden">
            <h3>
              <button
                type="button"
                aria-expanded={open}
                aria-controls={answerId}
                onClick={() => setOpenIndex(open ? null : index)}
                className="flex w-full items-center justify-between gap-md px-lg py-md text-start text-label-md font-label-md text-primary transition-colors hover:bg-surface-container-low"
              >
                {faq.question}
                <Icon
                  name="keyboard_arrow_down"
                  size={22}
                  className={cn('shrink-0 transition-transform', open && 'rotate-180')}
                />
              </button>
            </h3>

            {open && (
              <p
                id={answerId}
                className="border-t border-paper-border px-lg py-md text-body-md leading-loose text-on-surface-variant"
              >
                {faq.answer}
              </p>
            )}
          </Card>
        );
      })}
    </div>
  );
}
