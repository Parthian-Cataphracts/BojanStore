'use client';

import { useMemo, useState } from 'react';
import { Card, EmptyState, Icon, cn } from '@bojan/ui';
import { faqCategories, faqs } from '@/lib/content/pages';

/**
 * Screen 19: search field, category chips and an accordion.
 * Filtering runs client-side — the whole FAQ ships with the page.
 */
export function FaqList() {
  const [term, setTerm] = useState('');
  const [category, setCategory] = useState<string | null>(null);
  const [openIndex, setOpenIndex] = useState<number | null>(0);

  const results = useMemo(() => {
    const needle = term.trim();
    return faqs.filter((faq) => {
      const matchesCategory = !category || faq.category === category;
      const matchesTerm =
        !needle || faq.question.includes(needle) || faq.answer.includes(needle);
      return matchesCategory && matchesTerm;
    });
  }, [term, category]);

  return (
    <div className="flex flex-col gap-lg">
      <div className="flex h-12 items-center gap-sm rounded-lg border-b border-surface-variant bg-soft-mint/30 px-md">
        <Icon name="search" className="text-primary" />
        <input
          type="search"
          value={term}
          onChange={(event) => {
            setTerm(event.target.value);
            setOpenIndex(null);
          }}
          placeholder="جستجو در سوالات..."
          aria-label="جستجو در سوالات متداول"
          className="w-full border-none bg-transparent text-body-md text-on-surface outline-none placeholder:text-outline-variant focus:ring-0"
        />
      </div>

      <nav
        aria-label="دسته‌بندی سوالات"
        className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
      >
        <button
          type="button"
          onClick={() => setCategory(null)}
          className={cn(
            'shrink-0 whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors',
            category === null
              ? 'bg-primary-fixed text-on-primary-fixed'
              : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
          )}
        >
          همه
        </button>
        {faqCategories.map((name) => (
          <button
            key={name}
            type="button"
            onClick={() => setCategory(name)}
            className={cn(
              'shrink-0 whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors',
              category === name
                ? 'bg-primary-fixed text-on-primary-fixed'
                : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
            )}
          >
            {name}
          </button>
        ))}
      </nav>

      {results.length > 0 ? (
        <div className="flex flex-col gap-sm">
          {results.map((faq, index) => {
            const open = openIndex === index;
            return (
              <Card key={faq.question} className="overflow-hidden">
                <h3>
                  <button
                    type="button"
                    aria-expanded={open}
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
                  <p className="border-t border-paper-border px-lg py-md text-body-md leading-loose text-on-surface-variant">
                    {faq.answer}
                  </p>
                )}
              </Card>
            );
          })}
        </div>
      ) : (
        <EmptyState
          icon="search_off"
          title="سوالی پیدا نشد"
          description="عبارت دیگری را امتحان کنید یا دسته‌بندی را تغییر دهید."
        />
      )}
    </div>
  );
}
