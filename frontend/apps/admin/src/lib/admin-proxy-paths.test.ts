import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * Every route under `src/app/api` that talks to the .NET API builds its URL
 * from `API_BASE_URL`, and that value already ends in `/admin` — the panel's
 * `.env.example` and the compose file both set it that way, and the generic
 * `[resource]` proxy, the login route and the mailbox attachment route all
 * treat it so.
 *
 * Three routes did not. Chat reply, operator uploads and backup download each
 * wrote `/admin` again, asking the API for `/api/admin/admin/...` and getting a
 * 404 that each of them reported to the operator as a plain failure: "ارسال
 * پاسخ انجام نشد", "بارگذاری انجام نشد", "این نسخه هنوز فایلی ندارد". None of
 * it appeared in development, because all three return from their mock branch
 * before the fetch — so a developer's machine could never see it and every real
 * deployment always did.
 *
 * A grep rather than a request, for the same reason the icon test reads the
 * generator instead of restating its patterns: the mistake is textual and the
 * check should be too, so it holds for a route nobody has written yet.
 */
describe('admin API proxies', () => {
  const apiRoot = join(__dirname, '..', 'app', 'api');

  function routeFiles(dir: string): string[] {
    return readdirSync(dir).flatMap((entry) => {
      const full = join(dir, entry);
      if (statSync(full).isDirectory()) return routeFiles(full);
      return entry === 'route.ts' ? [full] : [];
    });
  }

  const files = routeFiles(apiRoot);

  it('finds the proxy routes to check', () => {
    expect(files.length).toBeGreaterThan(3);
  });

  it.each(files.map((file) => [file.slice(file.indexOf('app')), file] as const))(
    '%s does not repeat the /admin segment already in the base',
    (_label, file) => {
      const source = readFileSync(file, 'utf8');

      // Any template literal that interpolates the base and then immediately
      // writes `/admin`. `${base.replace(/\/$/, '')}/admin/...` is the shape
      // all three broken ones had.
      const doubled = /\$\{base[^}]*\}\/admin\b/.exec(source);

      expect(
        doubled?.[0],
        'API_BASE_URL already ends in /admin — drop the second one',
      ).toBeUndefined();
    },
  );
});
