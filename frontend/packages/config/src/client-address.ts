/**
 * Who the caller is, for anything that counts requests per client.
 *
 * Shared by both apps, in `@bojan/config` rather than copied into each — it was
 * copied into each, character for character, and it is now read by four callers
 * instead of two: each app's own rate limiter, and each app's API client, which
 * forwards this address to the backend so its limits can partition per shopper
 * as well. Four copies of a rule about which end of a header to trust is four
 * chances to trust the wrong end.
 *
 * `X-Forwarded-For` is a list, and every proxy *appends* to it — nginx's
 * `$proxy_add_x_forwarded_for` included. So the left-most entry is not the
 * client's address, it is whatever the client sent, and reading it made every
 * limit a formality: a different value per request bought a fresh window each
 * time, and with it unlimited sign-in codes, coupon guesses and tracking
 * lookups.
 *
 * The trustworthy entry is the one your own proxy wrote, counting back from the
 * right by however many proxies stand in front of this app. One by default,
 * which is the topology the compose file describes — everything published on
 * loopback with a single reverse proxy in front. Set `TRUSTED_PROXY_HOPS` if
 * there are more (a CDN in front of nginx is two).
 */

/**
 * Read per call rather than captured at module load, so a test can set it and
 * so the value cannot be frozen by whichever module happened to import this
 * first.
 */
function trustedProxyHops(): number {
  return Math.max(1, Number(process.env.TRUSTED_PROXY_HOPS ?? 1) || 1);
}

/**
 * The client's address, or null when nothing in the request names one.
 *
 * Null rather than a placeholder: a caller counting requests wants one shared
 * bucket for the unattributable ones, and a caller forwarding the address wants
 * to send no header at all rather than a header saying "unknown".
 */
export function clientAddress(headers: Headers): string | null {
  const chain = (headers.get('x-forwarded-for') ?? '')
    .split(',')
    .map((entry) => entry.trim())
    .filter(Boolean);

  // Counting from the right: the last entry was written by the nearest proxy,
  // the one before it by the proxy in front of that, and so on. Anything the
  // client supplied sits to the left of all of them and is never chosen.
  const hops = trustedProxyHops();
  const trusted = chain.length >= hops ? chain[chain.length - hops] : undefined;

  return trusted || headers.get('x-real-ip')?.trim() || null;
}
