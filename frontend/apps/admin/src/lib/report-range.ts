/**
 * The date-range chips, resolved into the window a report actually queries.
 *
 * Shared rather than repeated per screen: the sales report had its own copy and
 * the others had none at all, so four screens rendered the picker and ignored
 * it — the chips highlighted, the URL changed, and the numbers underneath
 * stayed exactly the same.
 *
 * A report whose subject has no time dimension — the current catalogue, stock
 * on hand right now — does not render the picker at all. A control that cannot
 * change what is on screen is worse than a missing one.
 */

export type ReportRange = '7d' | '30d' | '90d' | 'year';

export const DEFAULT_RANGE: ReportRange = '30d';

function isRange(value: string | undefined): value is ReportRange {
  return value === '7d' || value === '30d' || value === '90d' || value === 'year';
}

export interface ReportWindow {
  range: ReportRange;
  from: string;
  to: string;
}

export function resolveRange(value: string | string[] | undefined): ReportWindow {
  const raw = Array.isArray(value) ? value[0] : value;
  const range = isRange(raw) ? raw : DEFAULT_RANGE;

  const to = new Date();
  const from = new Date(to);

  switch (range) {
    case '7d':
      from.setDate(from.getDate() - 7);
      break;
    case '90d':
      from.setDate(from.getDate() - 90);
      break;
    case 'year':
      from.setMonth(0);
      from.setDate(1);
      from.setHours(0, 0, 0, 0);
      break;
    default:
      from.setDate(from.getDate() - 30);
      break;
  }

  return { range, from: from.toISOString(), to: to.toISOString() };
}

/** Whether a timestamp falls inside the window — for lists filtered client-side. */
export function withinRange(value: string | undefined, window: ReportWindow): boolean {
  if (!value) return false;
  const at = Date.parse(value);
  return Number.isFinite(at) && at >= Date.parse(window.from) && at <= Date.parse(window.to);
}
