'use client';

import { useEffect, useState } from 'react';
import { Badge, Card, Icon, cn, toPersianDigits } from '@bojan/ui';
import type { ServerStatusDto } from '@/lib/api/types';

function formatBytes(bytes: number): string {
  if (bytes <= 0) return '۰ B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  const value = bytes / 1024 ** exponent;
  return `${toPersianDigits(value.toFixed(value >= 10 ? 0 : 1))} ${units[exponent]}`;
}

function formatUptime(totalSeconds: number): string {
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

  const parts: string[] = [];
  if (days > 0) parts.push(`${toPersianDigits(days)} روز`);
  if (hours > 0 || days > 0) parts.push(`${toPersianDigits(hours)} ساعت`);
  parts.push(`${toPersianDigits(minutes)} دقیقه`);

  return parts.join(' و ');
}

/**
 * A dial: a ring open at the foot, with the reading in the middle of it.
 *
 * The gap is what makes it read as an instrument rather than a pie — a closed
 * circle has no beginning, so there is nothing to tell you which way "more"
 * goes. Open it at the bottom and the arc has a start on the left, a travel
 * over the top and an end on the right, which is how every gauge on every
 * dashboard has been read since they had needles.
 *
 * Geometry: the drawn portion is 285° of the 360, leaving a 75° gap centred on
 * six o'clock, and the whole thing is turned so the arc begins at the near
 * side of that gap. `strokeDasharray` does the drawing — the first number is
 * how much line to paint and the second is a gap longer than the rest of the
 * circle, so the track is the arc and the fill is the fraction of it.
 */
const GAP_DEGREES = 75;
const ARC_FRACTION = (360 - GAP_DEGREES) / 360;

function Dial({
  percent,
  children,
}: {
  /** 0–100. */
  percent: number;
  /** The reading, drawn in the middle. */
  children: React.ReactNode;
}) {
  const radius = 44;
  const circumference = 2 * Math.PI * radius;
  const arc = circumference * ARC_FRACTION;
  const clamped = Math.min(100, Math.max(0, percent));

  // The colour is the reading, not decoration: a dial that has gone red says
  // what the number says, one glance earlier.
  const tone =
    clamped >= 90
      ? 'stroke-error'
      : clamped >= 75
        ? 'stroke-secondary'
        : 'stroke-primary-container';

  return (
    <div className="relative h-[68px] w-[68px] shrink-0 md:h-[88px] md:w-[88px]">
      <svg viewBox="0 0 100 100" className="h-full w-full" aria-hidden="true">
        <g transform={`rotate(${90 + GAP_DEGREES / 2} 50 50)`}>
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            strokeWidth="7"
            strokeLinecap="round"
            strokeDasharray={`${arc} ${circumference}`}
            className="stroke-outline-variant/50"
          />
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            strokeWidth="7"
            strokeLinecap="round"
            strokeDasharray={`${arc * (clamped / 100)} ${circumference}`}
            className={cn('transition-[stroke-dasharray,stroke] duration-500 ease-out', tone)}
          />
        </g>
      </svg>

      <span className="text-caption text-primary absolute inset-0 flex items-center justify-center font-semibold tabular-nums">
        {children}
      </span>
    </div>
  );
}

/** A reading with no ceiling to measure it against — uptime, a byte count. */
function Reading({ icon, value }: { icon: string; value: string }) {
  return (
    <div className="gap-xs border-outline-variant/50 flex h-[68px] w-[68px] shrink-0 flex-col items-center justify-center rounded-full border md:h-[88px] md:w-[88px]">
      <Icon name={icon} size={20} className="text-primary-container" />
      <span className="text-helper text-primary px-2 text-center font-semibold tabular-nums leading-tight">
        {value}
      </span>
    </div>
  );
}

function Cell({
  label,
  detail,
  children,
}: {
  label: string;
  detail: string;
  children: React.ReactNode;
}) {
  return (
    <div className="gap-sm flex flex-col items-center text-center">
      {children}
      <div className="gap-xs flex min-w-0 flex-col">
        <span className="text-label-md text-primary">{label}</span>
        <span className="text-helper text-on-surface-variant tabular-nums">{detail}</span>
      </div>
    </div>
  );
}

/**
 * Screen 92's server-status card.
 *
 * The same four figures the card has always shown — uptime, CPU, the process's
 * memory and disk — drawn as dials rather than as a row of numbers. Only two of
 * the four are ratios, and only those two get an arc: a dial under a figure
 * with no ceiling would be a picture of nothing, and drawing one for uptime
 * because it would balance the row is how a dashboard starts lying politely.
 * The other two keep the ring as a frame and put the figure inside it, so the
 * row still reads as one instrument panel.
 *
 * Server-rendered once with the page, then kept current by polling
 * `/api/admin/system-status`: this card is the panel's answer to «is anything
 * wrong right now», and an answer as old as the last time somebody pressed F5
 * is not one. Only this card refetches — `router.refresh()` on a timer would
 * re-run the dashboard's other two API calls every tick to move two needles.
 *
 * Polling stops while the tab is hidden and catches up on return, the same
 * contract `AutoRefresh` uses. A failed poll keeps the last reading and says it
 * is stale rather than blanking the card: a lost connection is exactly when the
 * last known figures are worth having.
 */
export function ServerStatusCard({
  status: initial,
  seconds = 3,
}: {
  status: ServerStatusDto;
  /** How often to re-read. Each read costs the API ~200ms of CPU sampling. */
  seconds?: number;
}) {
  const [status, setStatus] = useState(initial);
  const [stale, setStale] = useState(false);

  // A fresh server render — a navigation back to the dashboard — wins over
  // whatever the last poll left behind.
  useEffect(() => {
    setStatus(initial);
    setStale(false);
  }, [initial]);

  useEffect(() => {
    let cancelled = false;

    async function poll() {
      if (document.visibilityState !== 'visible') return;

      try {
        const response = await fetch('/api/admin/system-status', { cache: 'no-store' });
        if (!response.ok) throw new Error('unavailable');

        const next = (await response.json()) as ServerStatusDto;
        if (cancelled) return;
        setStatus(next);
        setStale(false);
      } catch {
        if (!cancelled) setStale(true);
      }
    }

    const timer = window.setInterval(poll, seconds * 1000);
    document.addEventListener('visibilitychange', poll);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', poll);
    };
  }, [seconds]);

  const diskPercent =
    status.totalDiskBytes && status.freeDiskBytes !== undefined
      ? ((status.totalDiskBytes - status.freeDiskBytes) / status.totalDiskBytes) * 100
      : null;

  return (
    <Card surface="plain" className="overflow-hidden">
      <header className="gap-sm border-outline-variant/40 px-lg py-md flex flex-wrap items-center justify-between border-b">
        <div className="gap-sm flex items-center">
          <h2 className="font-headline text-section-title text-primary">
            وضعیت سرور
          </h2>
          {/*
            The live mark: a dot that pulses while readings are arriving and
            goes flat when they stop, so a frozen card is distinguishable from a
            quiet one without reading a timestamp off it.
          */}
          <span className="gap-xs text-helper text-on-surface-variant flex items-center">
            <span
              aria-hidden="true"
              className={cn(
                'h-1.5 w-1.5 rounded-full',
                stale ? 'bg-outline' : 'bg-primary-container animate-pulse',
              )}
            />
            {stale ? 'بدون به‌روزرسانی' : 'زنده'}
          </span>
        </div>

        <div className="gap-sm flex items-center">
          <Badge tone={status.databaseHealthy ? 'mint' : 'warning'}>
            <Icon name={status.databaseHealthy ? 'check_circle' : 'warning'} size={16} />
            {status.databaseHealthy ? 'پایگاه‌داده سالم' : 'پایگاه‌داده در دسترس نیست'}
          </Badge>
          <span className="text-caption text-on-surface-variant">
            {status.environment} · .NET {status.dotnetVersion}
          </span>
        </div>
      </header>

      <div className="gap-lg p-lg grid grid-cols-2 md:grid-cols-4" aria-live="polite">
        <Cell label="زمان فعالیت سرور" detail={formatUptime(status.uptimeSeconds)}>
          <Reading
            icon="schedule"
            value={formatUptime(status.uptimeSeconds).split(' و ')[0] ?? '—'}
          />
        </Cell>

        <Cell
          label="استفاده از پردازنده"
          detail={`${toPersianDigits(status.processorCount)} هسته · سهم این سرویس`}
        >
          {status.cpuLoadPercent === undefined ? (
            <Reading icon="memory" value="—" />
          ) : (
            <Dial percent={status.cpuLoadPercent}>
              {toPersianDigits(status.cpuLoadPercent.toFixed(status.cpuLoadPercent < 10 ? 1 : 0))}٪
            </Dial>
          )}
        </Cell>

        <Cell label="حافظه مصرفی فرآیند" detail={`${toPersianDigits(status.threadCount)} ترد فعال`}>
          <Reading icon="developer_board" value={formatBytes(status.workingSetBytes)} />
        </Cell>

        <Cell
          label="فضای دیسک"
          detail={
            status.totalDiskBytes && status.freeDiskBytes !== undefined
              ? `${formatBytes(status.totalDiskBytes - status.freeDiskBytes)} از ${formatBytes(status.totalDiskBytes)}`
              : 'در دسترس نیست'
          }
        >
          {diskPercent === null ? (
            <Reading icon="storage" value="—" />
          ) : (
            <Dial percent={diskPercent}>{toPersianDigits(diskPercent.toFixed(0))}٪</Dial>
          )}
        </Cell>
      </div>

      <p className="border-outline-variant/30 px-lg py-sm text-caption text-on-surface-variant border-t">
        {status.operatingSystem} · {toPersianDigits(status.processorCount)} هسته ·{' '}
        {toPersianDigits(status.threadCount)} ترد فعال
      </p>
    </Card>
  );
}
