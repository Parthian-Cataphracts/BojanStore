'use client';

import { useMemo, useState } from 'react';
import { Button, FormStatus, Icon, cn } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { SlugPicker } from './SlugPicker';
import { postJson } from '@/lib/submit';
import type { CatalogueOptionDto } from '@/lib/api/types';

/**
 * What a collection holds, and in what order — screen 104.
 *
 * A collection could be created in the panel and never filled: membership was
 * writable only from the product form, which puts a product at the end of
 * whatever it joins and gives an editor no way to say which product leads the
 * grouping. That order is the whole point of a curated collection, so it gets
 * a control that shows it rather than a picker that hides it.
 *
 * Saved on its own button rather than with the collection's fields. The list
 * is posted whole and replaces what is stored, and folding that into the
 * entity form would mean every save of a title also rewrote the membership.
 */
export function CollectionProductsPanel({
  collectionId,
  products,
  selected,
}: {
  collectionId: string;
  /** Everything the shop sells, for the picker. */
  products: CatalogueOptionDto[];
  /** What the collection holds now, in stored order. */
  selected: string[];
}) {
  const [picked, setPicked] = useState<string[]>(selected);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const titles = useMemo(
    () => new Map(products.map((product) => [product.slug, product.name])),
    [products],
  );

  /** Swaps a row with the one beside it — one step, not a drag. */
  function move(index: number, by: -1 | 1) {
    const target = index + by;
    if (target < 0 || target >= picked.length) return;

    const next = [...picked];
    [next[index], next[target]] = [next[target]!, next[index]!];
    setPicked(next);
    setSaved(false);
  }

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/collection-products', { id: collectionId, products: picked });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره محصولات کالکشن انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <FormSection
      title="محصولات کالکشن"
      icon="inventory_2"
      description="ترتیب این فهرست، همان ترتیبی است که در فروشگاه دیده می‌شود."
    >
      <SlugPicker
        label="افزودن یا حذف محصول"
        options={products}
        value={picked}
        onChange={(next) => {
          setPicked(next);
          setSaved(false);
        }}
        placeholder="انتخاب کنید"
        triggerLabel="محصولات انتخاب‌شده"
      />

      {picked.length === 0 ? (
        <p className="rounded-lg border border-dashed border-outline-variant p-md text-center text-caption text-on-surface-variant">
          این کالکشن هنوز خالی است.
        </p>
      ) : (
        <ol className="flex flex-col gap-xs">
          {picked.map((slug, index) => (
            <li
              key={slug}
              className="flex items-center gap-sm rounded-lg border border-outline-variant p-sm"
            >
              <span
                className={cn(
                  'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-caption',
                  index === 0
                    ? 'bg-primary/15 text-primary'
                    : 'bg-surface-container text-on-surface-variant',
                )}
              >
                {index + 1}
              </span>

              {/* A slug with no title belongs to a product archived since. Shown
                  as the slug rather than hidden: it really is in the
                  collection, and a row the operator cannot see is one they
                  cannot take out. */}
              <span className="flex-1 truncate text-body-md text-on-surface">
                {titles.get(slug) ?? slug}
              </span>

              {/* The subset font carries no `arrow_upward`, and a ligature it
                  does not have is drawn as its own name — so this is the arrow
                  it does carry, turned over. Same as the picker's chevron. */}
              <button
                type="button"
                aria-label="یک پله بالاتر"
                disabled={index === 0}
                onClick={() => move(index, -1)}
                className="flex h-8 w-8 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container-low disabled:opacity-30"
              >
                <Icon name="arrow_downward" size={18} className="rotate-180" />
              </button>
              <button
                type="button"
                aria-label="یک پله پایین‌تر"
                disabled={index === picked.length - 1}
                onClick={() => move(index, 1)}
                className="flex h-8 w-8 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container-low disabled:opacity-30"
              >
                <Icon name="arrow_downward" size={18} />
              </button>
              <button
                type="button"
                aria-label="حذف از کالکشن"
                onClick={() => {
                  setPicked(picked.filter((item) => item !== slug));
                  setSaved(false);
                }}
                className="flex h-8 w-8 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container-low"
              >
                <Icon name="close" size={18} />
              </button>
            </li>
          ))}
        </ol>
      )}

      <div className="flex flex-wrap items-center gap-md">
        <Button type="button" onClick={save} loading={saving}>
          ذخیره محصولات
        </Button>
        <FormStatus error={error} />
        <FormStatus ok={saved ? 'محصولات کالکشن ذخیره شد.' : null} />
      </div>
    </FormSection>
  );
}
