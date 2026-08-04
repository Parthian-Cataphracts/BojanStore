import { Badge, Card, Icon, toPersianDigits } from '@bojan/ui';
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

interface Metric {
  label: string;
  value: string;
  icon: string;
}

/**
 * Screen 92's server-status card. Every number comes from the request the
 * dashboard just made to the API process — nothing here is cached or
 * simulated, so the card is only as fresh as the last page load.
 */
export function ServerStatusCard({ status }: { status: ServerStatusDto }) {
  const diskUsedPercent =
    status.totalDiskBytes && status.freeDiskBytes !== undefined
      ? Math.round(((status.totalDiskBytes - status.freeDiskBytes) / status.totalDiskBytes) * 100)
      : null;

  const metrics: Metric[] = [
    { label: 'زمان فعالیت سرور', value: formatUptime(status.uptimeSeconds), icon: 'schedule' },
    {
      label: 'استفاده از پردازنده',
      value: status.cpuLoadPercent === undefined ? '—' : `${toPersianDigits(status.cpuLoadPercent)}%`,
      icon: 'memory',
    },
    { label: 'حافظه مصرفی فرآیند', value: formatBytes(status.workingSetBytes), icon: 'developer_board' },
    {
      label: 'فضای دیسک',
      value: diskUsedPercent === null ? '—' : `${toPersianDigits(diskUsedPercent)}٪ استفاده‌شده`,
      icon: 'storage',
    },
  ];

  return (
    <Card surface="plain" className="overflow-hidden">
      <header className="flex flex-wrap items-center justify-between gap-sm border-b border-outline-variant/40 px-lg py-md">
        <h2 className="font-headline text-card-title text-primary md:text-section-title">وضعیت سرور</h2>
        <div className="flex items-center gap-sm">
          <Badge tone={status.databaseHealthy ? 'mint' : 'warning'}>
            <Icon name={status.databaseHealthy ? 'check_circle' : 'warning'} size={16} />
            {status.databaseHealthy ? 'پایگاه‌داده سالم' : 'پایگاه‌داده در دسترس نیست'}
          </Badge>
          <span className="text-caption text-on-surface-variant">
            {status.environment} · .NET {status.dotnetVersion}
          </span>
        </div>
      </header>

      <div className="grid grid-cols-2 gap-md p-lg md:grid-cols-4">
        {metrics.map((metric) => (
          <div key={metric.label} className="flex flex-col gap-xs">
            <span className="flex h-9 w-9 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
              <Icon name={metric.icon} size={18} />
            </span>
            <span className="text-caption text-on-surface-variant">{metric.label}</span>
            <span className="tabular text-body-md font-label-md text-primary">{metric.value}</span>
          </div>
        ))}
      </div>

      <p className="border-t border-outline-variant/30 px-lg py-sm text-caption text-on-surface-variant">
        {status.operatingSystem} · {toPersianDigits(status.processorCount)} هسته · {toPersianDigits(status.threadCount)} ترد فعال
      </p>
    </Card>
  );
}
