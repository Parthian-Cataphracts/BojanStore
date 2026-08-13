'use client';

import { useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import { toPersianDigits } from '../lib/format';
import {
  JALALI_MONTHS,
  JALALI_WEEKDAYS,
  fromIsoDate,
  jalaliMonthLength,
  jalaliMonthStartColumn,
  toGregorian,
  toIsoDate,
  toJalali,
} from '../lib/jalali';
import { controlBase, FieldShell } from './Field';
import { Icon } from './Icon';

export interface JalaliDateInputProps {
  /**
   * Posted with the form. The value is always ISO `YYYY-MM-DD`.
   *
   * Optional, because a controlled caller reads the date out of its own state
   * and has nothing to post — the hidden input is only emitted when there is a
   * name to give it.
   */
  name?: string;
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  /** ISO `YYYY-MM-DD`, as the API stores it. Uncontrolled. */
  defaultValue?: string;
  /**
   * ISO `YYYY-MM-DD`, for a caller that holds the date in state.
   *
   * Supplying this makes the field controlled — the panel's discount and quote
   * screens validate a range while it is being picked, so they own the value
   * and this only draws it.
   */
  value?: string;
  /** Called with the ISO value whenever a day is picked or the field is cleared. */
  onChange?: (value: string) => void;
  required?: boolean;
  disabled?: boolean;
  /** How far back the year list runs. A birth date rarely needs more. */
  yearsBack?: number;
  /** Years ahead of today to offer — zero for a date that cannot be in the future. */
  yearsAhead?: number;
  placeholder?: string;
  wrapperClassName?: string;
}

/**
 * A date field that shows the shopper a Persian calendar and hands the form an
 * ISO one.
 *
 * The two halves of that sentence are the whole point. `<input type="date">`
 * posts exactly what the API wants but draws a Gregorian calendar, which is
 * not the calendar anyone here thinks in — a Persian speaker asked for their
 * birth date does not know it in March. A plain text box asking for Jalali
 * reads correctly and then posts something the API parses as the wrong
 * millennium: `DateOnly.TryParse("1373/02/28")` succeeds, as the year 1373 AD.
 * So the picker owns the conversion: Jalali on screen, ISO on the wire, and no
 * point in between where a Persian date is mistaken for a Gregorian one.
 *
 * A year dropdown rather than only arrows, because the field's main use is a
 * birth date and stepping back thirty years a month at a time is not a thing
 * anyone should be asked to do.
 */
export function JalaliDateInput({
  name,
  label,
  hint,
  error,
  defaultValue,
  value: controlledValue,
  onChange,
  required,
  disabled,
  yearsBack = 100,
  yearsAhead = 0,
  placeholder = 'انتخاب تاریخ',
  wrapperClassName,
}: JalaliDateInputProps) {
  const fieldId = useId();
  const [uncontrolled, setUncontrolled] = useState(() => defaultValue ?? '');

  /*
    Both shapes, because the screens that need a Persian calendar are split
    between them: the entity forms are uncontrolled and read at submit, while
    the discount panel, the quote composer and the coupon form hold the date in
    state to validate a range as it is typed. Only the uncontrolled half existed,
    so every controlled screen kept a native `<input type="date">` — a Gregorian
    calendar in a Persian panel, which is what this component was written to
    remove.
  */
  const isControlled = controlledValue !== undefined;
  const value = isControlled ? controlledValue : uncontrolled;

  const setValue = (next: string) => {
    if (!isControlled) setUncontrolled(next);
    onChange?.(next);
  };
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const today = useMemo(() => toJalali(new Date()) ?? { year: 1404, month: 1, day: 1 }, []);
  const selected = useMemo(() => {
    const parsed = value ? fromIsoDate(value) : null;
    return parsed ? toJalali(parsed) : null;
  }, [value]);

  // Which month the grid is showing — the selected one to begin with, today's
  // when nothing is selected yet.
  const [view, setView] = useState(() => ({
    year: selected?.year ?? today.year,
    month: selected?.month ?? today.month,
  }));

  // Re-open onto the selected date rather than wherever the last browse ended.
  useEffect(() => {
    if (!open) return;
    setView({ year: selected?.year ?? today.year, month: selected?.month ?? today.month });
  }, [open, selected?.year, selected?.month, today.year, today.month]);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: MouseEvent | TouchEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('touchstart', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('touchstart', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  const years = useMemo(() => {
    const newest = today.year + yearsAhead;
    return Array.from({ length: yearsBack + yearsAhead + 1 }, (_, index) => newest - index);
  }, [today.year, yearsBack, yearsAhead]);

  const daysInMonth = jalaliMonthLength(view.year, view.month);
  const startColumn = jalaliMonthStartColumn(view.year, view.month);

  function shiftMonth(step: number) {
    setView((current) => {
      const raw = current.month - 1 + step;
      return {
        year: current.year + Math.floor(raw / 12),
        month: ((raw % 12) + 12) % 12 + 1,
      };
    });
  }

  function choose(day: number) {
    const date = toGregorian(view.year, view.month, day);
    if (!date) return;
    setValue(toIsoDate(date));
    setOpen(false);
  }

  const shown = selected
    ? toPersianDigits(
        `${selected.year}/${String(selected.month).padStart(2, '0')}/${String(selected.day).padStart(2, '0')}`,
      )
    : '';

  return (
    <FieldShell
      id={fieldId}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      <div ref={containerRef} className="relative">
        {/*
          The form posts this, never the Persian text above it. Keeping the
          wire value in its own hidden field is what lets the visible control
          be a button rather than something a browser would try to parse.
        */}
        {name && <input type="hidden" name={name} value={value} />}

        <button
          type="button"
          id={fieldId}
          disabled={disabled}
          onClick={() => setOpen((current) => !current)}
          aria-haspopup="dialog"
          aria-expanded={open}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${fieldId}-error` : hint ? `${fieldId}-hint` : undefined}
          className={cn(
            controlBase,
            'flex h-12 items-center gap-sm text-start',
            error ? 'border-error' : 'border-outline-variant',
          )}
        >
          <Icon name="calendar_today" size={20} className="shrink-0 text-outline" />
          <span className={cn('tabular flex-1', shown ? 'text-on-surface' : 'text-outline')}>
            {shown || placeholder}
          </span>
          {shown && !disabled && (
            /*
              A <span>, not a nested <button> — a button inside a button is not
              something a browser will let you click. The click is caught on the
              way up and kept from reaching the trigger.
            */
            <span
              role="button"
              tabIndex={0}
              aria-label="پاک کردن تاریخ"
              onClick={(event) => {
                event.stopPropagation();
                setValue('');
              }}
              onKeyDown={(event) => {
                if (event.key !== 'Enter' && event.key !== ' ') return;
                event.preventDefault();
                event.stopPropagation();
                setValue('');
              }}
              className="grid h-6 w-6 shrink-0 place-items-center rounded-full text-outline transition-colors hover:bg-surface-container hover:text-on-surface"
            >
              <Icon name="close" size={16} />
            </span>
          )}
        </button>

        {open && (
          <div
            role="dialog"
            aria-label="انتخاب تاریخ"
            className="absolute inset-x-0 top-full z-30 mt-xs w-full min-w-[17rem] rounded-xl border border-paper-border bg-surface-container-lowest p-sm shadow-soft"
          >
            <div className="mb-sm flex items-center gap-xs">
              {/*
                The page is right-to-left, so "back a month" is the chevron
                pointing right and "forward" the one pointing left.
              */}
              <button
                type="button"
                onClick={() => shiftMonth(-1)}
                aria-label="ماه قبل"
                className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
              >
                <Icon name="chevron_right" size={20} />
              </button>

              <select
                aria-label="ماه"
                value={view.month}
                onChange={(event) => setView((c) => ({ ...c, month: Number(event.target.value) }))}
                className="h-9 flex-1 rounded-lg border border-outline-variant bg-surface-container-lowest px-sm text-body-md text-on-surface focus:border-primary focus:outline-none"
              >
                {JALALI_MONTHS.map((monthName, index) => (
                  <option key={monthName} value={index + 1}>
                    {monthName}
                  </option>
                ))}
              </select>

              <select
                aria-label="سال"
                value={view.year}
                onChange={(event) => setView((c) => ({ ...c, year: Number(event.target.value) }))}
                className="tabular h-9 w-24 rounded-lg border border-outline-variant bg-surface-container-lowest px-sm text-body-md text-on-surface focus:border-primary focus:outline-none"
              >
                {years.map((year) => (
                  <option key={year} value={year}>
                    {toPersianDigits(year)}
                  </option>
                ))}
              </select>

              <button
                type="button"
                onClick={() => shiftMonth(1)}
                aria-label="ماه بعد"
                className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
              >
                <Icon name="chevron_left" size={20} />
              </button>
            </div>

            <div className="grid grid-cols-7 gap-xs" role="presentation">
              {JALALI_WEEKDAYS.map((weekday, index) => (
                <span
                  key={`${weekday}-${index}`}
                  aria-hidden
                  className="grid h-8 place-items-center text-caption text-on-surface-variant"
                >
                  {weekday}
                </span>
              ))}

              {Array.from({ length: startColumn }, (_, index) => (
                <span key={`pad-${index}`} aria-hidden />
              ))}

              {Array.from({ length: daysInMonth }, (_, index) => index + 1).map((day) => {
                const isSelected =
                  selected?.year === view.year &&
                  selected?.month === view.month &&
                  selected?.day === day;
                const isToday =
                  today.year === view.year && today.month === view.month && today.day === day;

                return (
                  <button
                    key={day}
                    type="button"
                    onClick={() => choose(day)}
                    aria-pressed={isSelected}
                    aria-current={isToday ? 'date' : undefined}
                    className={cn(
                      'tabular grid h-9 place-items-center rounded-lg text-body-md transition-colors',
                      isSelected
                        ? 'bg-primary font-label-md text-on-primary'
                        : 'text-on-surface hover:bg-soft-mint',
                      !isSelected && isToday && 'ring-1 ring-primary/40',
                    )}
                  >
                    {toPersianDigits(day)}
                  </button>
                );
              })}
            </div>

            <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-sm">
              <button
                type="button"
                onClick={() => {
                  const now = toGregorian(today.year, today.month, today.day);
                  if (now) setValue(toIsoDate(now));
                  setOpen(false);
                }}
                className="rounded-lg px-sm py-xs text-caption text-primary transition-colors hover:bg-soft-mint"
              >
                امروز
              </button>

              <button
                type="button"
                onClick={() => {
                  setValue('');
                  setOpen(false);
                }}
                className="rounded-lg px-sm py-xs text-caption text-on-surface-variant transition-colors hover:bg-surface-container"
              >
                پاک کردن
              </button>
            </div>
          </div>
        )}
      </div>
    </FieldShell>
  );
}
