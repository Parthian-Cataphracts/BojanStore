/**
 * Prerendering a dynamic route without requiring the API to be up.
 *
 * `generateStaticParams` runs during `next build`, and every one of ours asks
 * the API what exists. That is fine on a machine that can reach it and fatal on
 * one that cannot: the image is built in an isolated container, on a host where
 * the API is not running yet and could not be reached from the build network if
 * it were, so the fetch is refused and the whole build exits non-zero. The
 * shipped Dockerfile builds exactly that way.
 *
 * Returning no params is not a failure. Next renders an unlisted path on first
 * request instead, by which time the API is up — the page is the same page, and
 * the only cost is that the first visitor to each one waits for a render that
 * would otherwise have happened at build time.
 *
 * So the build no longer depends on the API being reachable, while a reachable
 * API still gets the full prerender.
 */
export async function staticParams<T>(
  /** What the route is prerendering — named in the log line when it is skipped. */
  what: string,
  load: () => Promise<T[]>,
): Promise<T[]> {
  try {
    return await load();
  } catch (cause) {
    // Warn rather than swallow: a build that quietly stopped prerendering the
    // catalogue looks identical to one that never could, and the difference
    // matters the next time someone asks why the first hit is slow.
    console.warn(
      `[static-params] ${what}: the API was unreachable, so these paths will render on demand.`,
      cause instanceof Error ? cause.message : cause,
    );
    return [];
  }
}
