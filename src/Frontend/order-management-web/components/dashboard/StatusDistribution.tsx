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
      <p className="rounded-lg border border-dashed border-border px-4 py-8 text-center text-sm text-muted-foreground">
        Nenhum pedido ainda.
      </p>
    );
  }

  const share = (status: OrderStatus) =>
    ((byStatus.get(status)?.count ?? 0) / total) * 100;

  return (
    <div className="flex h-full flex-col gap-4">
      <div
        className="channel flex h-3 gap-0.5 overflow-hidden rounded-full"
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

      <dl className="grid flex-1 content-between gap-x-6 gap-y-2.5 border-t border-border pt-3">
        {ALL_STATUSES.map((status) => {
          const slice = byStatus.get(status);
          const count = slice?.count ?? 0;
          return (
            <div key={status} className="flex items-baseline justify-between gap-3">
              <dt className="flex items-center gap-2 truncate text-sm text-muted-foreground">
                <StageMark status={status} />
                {status}
              </dt>
              <dd className="flex items-baseline gap-3">
                <span className="figure text-sm text-foreground">{count}</span>
                <span className="figure w-20 text-right text-xs text-muted-foreground">
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
