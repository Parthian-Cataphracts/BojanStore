import type { CSSProperties } from 'react';
import { cn } from '../lib/cn';

export type IconWeight = 100 | 200 | 300 | 400 | 500 | 600 | 700;

export interface IconProps {
  /** Material Symbols ligature name, e.g. `shopping_cart`. */
  name: string;
  /** Render the filled variant. The design uses outlined almost everywhere. */
  filled?: boolean;
  weight?: IconWeight;
  /** Optical size in px; also drives the rendered glyph size. */
  size?: number;
  className?: string;
  style?: CSSProperties;
}

/**
 * Material Symbols Outlined icon — the only icon set the design uses.
 * The font itself is loaded once in each app's root layout.
 */
export function Icon({ name, filled = false, weight = 400, size, className, style }: IconProps) {
  return (
    <span
      aria-hidden="true"
      className={cn('material-symbols-outlined select-none', className)}
      style={{
        fontVariationSettings: `'FILL' ${filled ? 1 : 0}, 'wght' ${weight}, 'GRAD' 0, 'opsz' ${size ?? 24}`,
        ...(size ? { fontSize: `${size}px` } : null),
        ...style,
      }}
    >
      {name}
    </span>
  );
}
