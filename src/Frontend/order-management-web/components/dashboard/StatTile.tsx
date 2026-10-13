import { cn } from "@/lib/utils";

/**
 * One reading in the cluster. Deliberately not a chart: a single current value
 * reads faster as a figure than as a one-bar chart, and the figure is where
 * this page spends all of its weight.
 */
export function StatTile({
  label,
  value,
  unit,
  meta,
  accent,
  className,
}: {
  label: string;
  value: string;
  /** Rendered small and quiet next to the figure, so the figure stays a figure. */
  unit?: string;
  meta?: string;
  /** A colour token for the figure, when the value carries a status meaning. */
  accent?: string;
  className?: string;
}) {
  return (
    <div className={cn("flex min-w-0 flex-col justify-between gap-3 bg-card px-4 py-3.5", className)}>
      <p className="label truncate">{label}</p>
      <div>
        <p className="flex items-baseline gap-1">
          <span className={cn("figure text-[1.6rem] leading-none", accent ?? "text-foreground")}>
            {value}
          </span>
          {unit && <span className="text-sm text-muted-foreground">{unit}</span>}
        </p>
        {meta && <p className="mt-1.5 truncate text-xs text-muted-foreground">{meta}</p>}
      </div>
    </div>
  );
}
