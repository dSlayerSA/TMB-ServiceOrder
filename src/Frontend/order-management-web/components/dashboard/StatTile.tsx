import { cn } from "@/lib/utils";

/**
 * A headline number. Deliberately not a chart: a single current value reads
 * faster as a figure than as a one-bar chart.
 */
export function StatTile({
  label,
  value,
  meta,
  accent,
  className,
}: {
  label: string;
  value: string;
  meta?: string;
  /** A colour token for the figure, when the value carries a status meaning. */
  accent?: string;
  className?: string;
}) {
  return (
    <div className={cn("rounded-lg border bg-card p-4", className)}>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={cn("tabular mt-2 font-mono text-2xl leading-none", accent ?? "text-foreground")}>
        {value}
      </p>
      {meta && <p className="mt-2 text-xs text-muted-foreground">{meta}</p>}
    </div>
  );
}
