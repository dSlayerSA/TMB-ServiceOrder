import { cn } from "@/lib/utils";
import type { StatusSlice } from "@/types/dashboard";
import type { OrderStatus } from "@/types/order";
import { ALL_STATUSES, StageMark, STAGE_BG } from "@/components/orders/StageMark";

const currency = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
  maximumFractionDigits: 0,
});

/**
 * Part-to-whole across the five states, as one proportional bar. Segments are
 * separated by a 2px gap in the surface colour rather than a stroke, and the
 * legend carries counts and totals in text tokens with a colour swatch beside
 * them - the text itself never wears the series colour.
 */
export function StatusDistribution({ slices }: { slices: StatusSlice[] }) {
  const byStatus = new Map(slices.map((s) => [s.status, s]));
  const present = ALL_STATUSES.filter((status) => (byStatus.get(status)?.count ?? 0) > 0);
  const total = slices.reduce((sum, s) => sum + s.count, 0);

  if (total === 0) {
    return (
      <p className="rounded-lg border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">
        Nenhum pedido ainda.
      </p>
    );
  }

  const share = (status: OrderStatus) =>
    ((byStatus.get(status)?.count ?? 0) / total) * 100;

  return (
    <div className="space-y-4">
      <div
        className="flex h-2.5 gap-0.5 overflow-hidden rounded-full bg-muted"
        role="img"
        aria-label={present
          .map((s) => `${byStatus.get(s)!.count} ${s} (${share(s).toFixed(0)}%)`)
          .join(", ")}
      >
        {present.map((status) => (
          <div
            key={status}
            className={cn("h-full first:rounded-l-full last:rounded-r-full", STAGE_BG[status])}
            style={{ width: `${share(status)}%` }}
          />
        ))}
      </div>

      <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
        {ALL_STATUSES.map((status) => {
          const slice = byStatus.get(status);
          const count = slice?.count ?? 0;
          return (
            <div key={status} className="flex items-baseline justify-between gap-3">
              <dt className="flex items-center gap-2 text-sm text-muted-foreground">
                <StageMark status={status} />
                {status}
              </dt>
              <dd className="flex items-baseline gap-3">
                <span className="tabular font-mono text-sm text-foreground">{count}</span>
                <span className="tabular w-20 text-right font-mono text-xs text-muted-foreground">
                  {currency.format(slice?.totalAmount ?? 0)}
                </span>
              </dd>
            </div>
          );
        })}
      </dl>
    </div>
  );
}
