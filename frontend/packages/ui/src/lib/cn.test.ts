import { describe, expect, it } from 'vitest';
import { cn } from './cn';

describe('cn', () => {
  it('joins class names', () => {
    expect(cn('flex', 'items-center')).toBe('flex items-center');
  });

  it('drops falsy values so conditional classes stay readable at the call site', () => {
    expect(cn('flex', false && 'hidden', undefined, null, 'gap-md')).toBe('flex gap-md');
  });

  it('lets a later Tailwind class win over an earlier one in the same group', () => {
    // This is the whole reason for tailwind-merge: a caller-supplied
    // `className` must be able to override a component's default.
    expect(cn('p-md', 'p-lg')).toBe('p-lg');
    expect(cn('text-primary', 'text-error')).toBe('text-error');
  });

  it('keeps classes from different groups side by side', () => {
    expect(cn('px-md', 'py-lg')).toBe('px-md py-lg');
  });

  /*
   * The design system replaces Tailwind's stock scales with named tokens.
   * tailwind-merge has to be taught about them or overrides silently fail —
   * both classes survive and the cascade picks one at random.
   */
  describe('custom design-system scales', () => {
    it('overrides custom spacing tokens', () => {
      expect(cn('p-md', 'p-lg')).toBe('p-lg');
      expect(cn('gap-sm', 'gap-xl')).toBe('gap-xl');
      expect(cn('px-margin-mobile', 'px-margin-desktop')).toBe('px-margin-desktop');
    });

    it('overrides custom font-size tokens', () => {
      expect(cn('text-body-md', 'text-card-title')).toBe('text-card-title');
      expect(cn('text-kpi-mobile', 'text-kpi')).toBe('text-kpi');
    });

    it('overrides custom radius tokens', () => {
      expect(cn('rounded-lg', 'rounded-pill')).toBe('rounded-pill');
    });

    it('overrides custom font-family tokens', () => {
      expect(cn('font-body', 'font-headline')).toBe('font-headline');
    });

    it('still distinguishes a font size from a text colour', () => {
      // `text-*` is overloaded; size and colour must not cancel each other.
      expect(cn('text-body-md', 'text-primary')).toBe('text-body-md text-primary');
    });

    it('keeps directional spacing independent', () => {
      expect(cn('pt-md', 'pb-lg')).toBe('pt-md pb-lg');
    });
  });
});
