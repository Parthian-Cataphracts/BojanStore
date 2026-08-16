/**
 * A picked day, as the instant the API should filter from or to.
 *
 * Iran has had no daylight saving since 1401, so a fixed offset is the whole of
 * the conversion — there is no zone table to consult and no hour of the year
 * where this is wrong.
 *
 * The two edges are not symmetric on purpose. `start` is midnight, and `end` is
 * the last second of the day rather than the next midnight: a range that stops
 * at 00:00 excludes the day the operator asked for, so «تا ۱۴۰۵/۰۵/۱۳» would
 * quietly leave out everything that happened on the thirteenth.
 */
const TEHRAN_OFFSET = '+03:30';

export function instantFor(isoDate: string, edge: 'start' | 'end'): string | undefined {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(isoDate)) return undefined;
  return `${isoDate}T${edge === 'start' ? '00:00:00' : '23:59:59'}${TEHRAN_OFFSET}`;
}
