/**
 * Whether the panel reads its fixtures instead of the backend.
 *
 * Development defaults to on so the panel renders before the API is up.
 * Production defaults to off and must be opted into by name. The rule used to
 * be "on unless the variable says `false`", which meant a deploy that simply
 * forgot the variable showed the operator invented orders, revenue and stock
 * levels, and acknowledged every write without forwarding one — with nothing
 * on screen to say so.
 *
 * It lives alone in this file, rather than beside the fetch wrapper that first
 * needed it, because the sign-in form reads it too. `client.ts` reaches
 * `lib/auth/server` for the operator header, that module imports
 * `next/headers`, and importing the flag from there put all of it in the
 * browser bundle — which does not build. A constant folded from an environment
 * variable has no business dragging the session reader along with it.
 */
export const useMockData =
  process.env.NODE_ENV === 'production'
    ? process.env.NEXT_PUBLIC_USE_MOCK_DATA === 'true'
    : process.env.NEXT_PUBLIC_USE_MOCK_DATA !== 'false';
