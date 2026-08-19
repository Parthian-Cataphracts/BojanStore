'use client';

import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { FieldShell, Icon, cn, controlBase, matchesPersian } from '@bojan/ui';
import type { CatalogueOptionDto } from '@/lib/api/types';

/**
 * A picker for "this thing belongs to several of those" — the product form's
 * categories and collections.
 *
 * Closed it is the same control as the brand select beside it, so a form of
 * eight fields still reads as eight rows; open it is a list of checkboxes.
 * A `<select multiple>` would have been the shorter way to that shape and is
 * worse at exactly the job: on a touchpad, adding a second selection without
 * ctrl-clicking clears the first, and nothing about the control says so.
 * Checkboxes cannot lose a selection by accident.
 *
 * The value is the catalogue *slug*, which is what `POST /products` resolves —
 * the same thing the single-category `<Select>` submitted before it.
 */
export function SlugPicker({
  label,
  hint,
  error,
  options,
  value,
  onChange,
  placeholder,
  /** Marks the first pick as the primary one, for categories. */
  primaryHint,
  triggerLabel,
}: {
  label: string;
  hint?: string;
  error?: string;
  options: CatalogueOptionDto[];
  value: string[];
  onChange: (next: string[]) => void;
  placeholder: string;
  primaryHint?: string;
  /**
   * Collapses the trigger to this label plus a count.
   *
   * For a picker whose selection is already listed underneath it — the
   * collection's products — chips in the trigger would say the same thing
   * twice, and with twenty of them the closed control would be taller than the
   * list it duplicates.
   */
  triggerLabel?: string;
}) {
  const fieldId = useId();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const root = useRef<HTMLDivElement>(null);

  /*
    Closing on a click elsewhere and on Escape, because a panel that overlays
    the fields under it has to be dismissible without picking something. The
    listener is only attached while the panel is open — a form with two of
    these would otherwise keep two document handlers alive for a control
    nobody has touched.
  */
  useEffect(() => {
    if (!open) return;

    function onPointerDown(event: MouseEvent) {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') setOpen(false);
    }

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  // Persian is written several ways for one word, so the filter folds both
  // sides — the same rule the storefront's own search uses. A picker that
  // answered «چیزی پیدا نشد» for a category the shop has would send the
  // operator back to scrolling.
  const shown = useMemo(() => {
    const needle = search.trim();
    if (needle.length === 0) return options;
    return options.filter((option) => matchesPersian(option.name, needle));
  }, [options, search]);

  const names = useMemo(
    () => new Map(options.map((option) => [option.slug, option.name])),
    [options],
  );

  function toggle(slug: string) {
    onChange(value.includes(slug) ? value.filter((item) => item !== slug) : [...value, slug]);
  }

  /** Moves a pick to the front, which is what makes it the primary one. */
  function promote(slug: string) {
    onChange([slug, ...value.filter((item) => item !== slug)]);
  }

  return (
    <FieldShell id={fieldId} label={label} hint={hint} error={error}>
      <div ref={root} className="relative">
        <button
          type="button"
          id={fieldId}
          onClick={() => setOpen((current) => !current)}
          aria-expanded={open}
          aria-haspopup="listbox"
          // A button cannot carry aria-invalid, so the refusal reaches a
          // screen reader the way Input's does: by pointing at the line
          // FieldShell already renders under the control.
          aria-describedby={error ? `${fieldId}-error` : hint ? `${fieldId}-hint` : undefined}
          className={cn(
            controlBase,
            'flex min-h-12 items-center justify-between gap-sm py-sm text-start',
            error ? 'border-error' : 'border-outline-variant',
          )}
        >
          {value.length === 0 ? (
            <span className="text-outline">{placeholder}</span>
          ) : triggerLabel ? (
            <span className="flex items-center gap-sm">
              <span className="text-on-surface">{triggerLabel}</span>
              <span className="rounded-full bg-soft-mint/60 px-sm py-xs text-caption text-on-surface">
                {value.length.toLocaleString('fa-IR')}
              </span>
            </span>
          ) : (
            <span className="flex flex-wrap items-center gap-xs">
              {value.map((slug, index) => (
                <span
                  key={slug}
                  className="flex items-center gap-xs rounded-full bg-soft-mint/60 px-sm py-xs text-caption text-on-surface"
                >
                  {/* A slug with no name belongs to a category or collection
                      archived since. Shown as the slug rather than hidden: the
                      product really is filed there, and a pick the operator
                      cannot see is one they cannot undo. */}
                  {names.get(slug) ?? slug}
                  {primaryHint && index === 0 && (
                    <span className="rounded-full bg-primary/15 px-xs text-primary">
                      {primaryHint}
                    </span>
                  )}
                </span>
              ))}
            </span>
          )}

          {/*
            One glyph turned over, not two. The shipped font is a subset of
            Material Symbols and carries no `expand_less` — and a ligature the
            font does not have is drawn as its own name, so the control read
            "EXPAND_LESS" in Latin capitals the moment it was opened. Rotating
            the arrow it does carry also animates, which two swapped glyphs
            would not.
          */}
          <Icon
            name="expand_more"
            size={20}
            className={cn('shrink-0 text-outline transition-transform', open && 'rotate-180')}
          />
        </button>

        {open && (
          <div className="absolute inset-x-0 top-full z-20 mt-xs flex flex-col gap-sm rounded-lg border border-outline-variant bg-surface-container-lowest p-sm shadow-lg">
            {/* Only once the list is long enough that scanning it is the
                slower way. Below that the search box is one more thing to
                look past. */}
            {options.length > 8 && (
              <div className="relative flex items-center">
                <Icon
                  name="search"
                  size={18}
                  className="pointer-events-none absolute start-sm text-outline"
                />
                <input
                  type="search"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="جست‌وجو…"
                  autoFocus
                  className="h-10 w-full rounded-lg border border-outline-variant bg-surface-container-lowest ps-[38px] pe-sm text-caption text-on-surface placeholder:text-outline focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
                />
              </div>
            )}

            <div className="max-h-64 overflow-y-auto">
              {shown.length === 0 ? (
                <p className="px-xs py-sm text-caption text-on-surface-variant">موردی پیدا نشد.</p>
              ) : (
                <ul className="flex flex-col">
                  {shown.map((option) => {
                    const checked = value.includes(option.slug);
                    const isPrimary = checked && value[0] === option.slug;

                    return (
                      <li
                        key={option.slug}
                        className="flex items-center gap-sm rounded-lg pe-xs transition-colors hover:bg-surface-container-low"
                      >
                        {/* A real checkbox rather than an icon that looks like
                            one: it is the control a screen reader announces,
                            it takes focus from the keyboard, and it is what
                            the rest of the panel's forms use. */}
                        <label className="flex flex-1 cursor-pointer select-none items-center gap-sm px-xs py-sm">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => toggle(option.slug)}
                            className="h-5 w-5 shrink-0 rounded border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
                          />
                          <span className="text-body-md text-on-surface">{option.name}</span>
                        </label>

                        {/*
                          Which pick is primary is its position in the list, and
                          the only way to change that used to be unticking
                          everything and starting again. One button says it
                          instead.
                        */}
                        {primaryHint && checked
                          ? (isPrimary ? (
                              <span className="rounded-full bg-primary/15 px-sm py-xs text-caption text-primary">
                                {primaryHint}
                              </span>
                            ) : (
                              <button
                                type="button"
                                onClick={() => promote(option.slug)}
                                className="rounded-full px-sm py-xs text-caption text-primary transition-colors hover:bg-primary/10"
                              >
                                {primaryHint} شود
                              </button>
                            ))
                          : null}
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          </div>
        )}
      </div>
    </FieldShell>
  );
}
