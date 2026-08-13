/**
 * The application's own log, read from the panel.
 *
 * Everything the API said about itself used to go to stdout and nowhere else,
 * so the only way to read why something failed was to reach the host and run
 * `docker logs`. That is a fine thing to ask of an engineer and an impossible
 * one to ask of the person whose shop it is — and it is why a fault that hit
 * every category page was found by a customer rather than on a screen.
 */

import { api, useMockData } from './client';

export interface LogFileDto {
  name: string;
  sizeBytes: number;
  modifiedAtUtc: string;
}

export interface LogTailDto {
  name: string;
  /** Newest first — a log is read backwards. */
  lines: string[];
  /** Whether the ceiling was reached, so the screen can say "the last N". */
  truncated: boolean;
}

/**
 * No fixture.
 *
 * The other screens mock so a fresh clone renders before anyone has a database,
 * and inventing log lines would be the one case where that is actively
 * misleading: a made-up stack trace on a screen whose entire purpose is to say
 * what really happened. In mock mode the screen says there is nothing to read.
 */
export async function getLogFiles(): Promise<LogFileDto[]> {
  if (useMockData) return [];
  return api.get<LogFileDto[]>('/logs', { auth: true }).catch(() => []);
}

export async function getLogTail(
  name: string,
  options: { limit?: number; q?: string } = {},
): Promise<LogTailDto | null> {
  if (useMockData) return null;

  return api
    .get<LogTailDto>(`/logs/${encodeURIComponent(name)}`, {
      query: { limit: options.limit ?? 300, q: options.q || undefined },
      auth: true,
    })
    .catch(() => null);
}
