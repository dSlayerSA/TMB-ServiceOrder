import { cn } from "@/lib/utils";

/**
 * The one surface in the app. A panel is told apart from the page by its value
 * and a hairline, never by a shadow - that is reserved for things genuinely
 * floating, which is only the dialog.
 */
export function Panel({ className, ...props }: React.ComponentProps<"section">) {
  return (
    <section
      className={cn("surface rounded-lg border border-border bg-card", className)}
      {...props}
    />
  );
}

/** Title band. The rule under it is what makes the panel read as an instrument. */
export function PanelHeader({
  title,
  meta,
  children,
  className,
}: {
  title: string;
  /** One short line of orientation, e.g. what a column covers. */
  meta?: string;
  /** Controls that act on this panel only. */
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-border px-5 py-3.5",
        className
      )}
    >
      <div className="min-w-0">
        <h2 className="font-condensed text-sm leading-tight font-semibold tracking-tight">
          {title}
        </h2>
        {meta && <p className="mt-0.5 text-xs text-muted-foreground">{meta}</p>}
      </div>
      {children && <div className="flex shrink-0 items-center gap-2">{children}</div>}
    </div>
  );
}

export function PanelBody({ className, ...props }: React.ComponentProps<"div">) {
  return <div className={cn("p-5", className)} {...props} />;
}

/** A row of readings, each its own plate, with room to breathe between them. */
export function Cluster({ className, ...props }: React.ComponentProps<"div">) {
  return <div className={cn("grid grid-cols-1 gap-4 sm:grid-cols-2", className)} {...props} />;
}

/** Empty state. Dashed, never a panel: there is nothing to frame yet. */
export function EmptyNote({
  title,
  hint,
  className,
}: {
  title: string;
  hint?: string;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "rounded-lg border border-dashed border-border px-5 py-12 text-center",
        className
      )}
    >
      <p className="text-sm font-medium">{title}</p>
      {hint && <p className="mt-1 text-sm text-muted-foreground">{hint}</p>}
    </div>
  );
}
