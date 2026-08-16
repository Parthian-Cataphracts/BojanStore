import { Badge, Card, Icon } from '@bojan/ui';

export interface Kpi {
  label: string;
  value: string;
  icon: string;
  /** Change vs the previous period, e.g. `+۱۲٪`. */
  delta?: string;
  up?: boolean;
}

/** The KPI tile row that opens the dashboard and the report screens. */
export function KpiRow({ items }: { items: Kpi[] }) {
  return (
    <section className="grid grid-cols-1 gap-md sm:grid-cols-2 xl:grid-cols-4">
      {items.map((kpi) => (
        <Card key={kpi.label} className="flex flex-col gap-sm p-lg">
          <div className="flex items-center justify-between">
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
              <Icon name={kpi.icon} size={22} />
            </span>
            {kpi.delta && <Badge tone={kpi.up ? 'mint' : 'warning'}>{kpi.delta}</Badge>}
          </div>

          <span className="text-caption text-on-surface-variant">{kpi.label}</span>
          <span className="tabular text-kpi text-primary">{kpi.value}</span>
        </Card>
      ))}
    </section>
  );
}
