import { cn } from "@/lib/utils";
import { LocalDateTime } from "@/components/LocalDateTime";
import type { OrderStatus, OrderStatusHistoryItem } from "@/types/order";
import {
  isTerminal,
  StageMark,
  STAGE_COLOR,
  STAGE_LINE,
} from "@/components/orders/StageMark";


function elapsed(fromIso: string, toIso: string) {
  const seconds = (new Date(toIso).getTime() - new Date(fromIso).getTime()) / 1000;
  return seconds < 60
    ? `${seconds.toFixed(1).replace(".", ",")} s`
    : `${Math.floor(seconds / 60)} min ${Math.round(seconds % 60)} s`;
}

/**
 * The whole route, not only the steps already taken: a stage the order has not
 * reached is still listed, so the view answers "what happens next" as well as
 * "what happened". The last row is whichever ending applies - Finalizado on the
 * happy path, or the Falhou/Cancelado the order actually hit.
 */
export function OrderStatusHistoryTimeline({
  history,
  status,
}: {
  history: OrderStatusHistoryItem[];
  status: OrderStatus;
}) {
  const reached = new Map(history.map((item) => [item.status, item]));

  const ending: OrderStatus =
    history.find((item) => item.status === "Falhou" || item.status === "Cancelado")?.status ??
    "Finalizado";

  const stages: OrderStatus[] = ["Pendente", "Processando", ending];
  const settled = isTerminal(status);

  return (
    <ol>
      {stages.map((stage, index) => {
        const item = reached.get(stage);
        const previous = index > 0 ? reached.get(stages[index - 1]) : undefined;
        const isLast = index === stages.length - 1;
        const nextReached = !isLast && reached.has(stages[index + 1]);

        return (
          <li key={stage} className="flex gap-3">
            <div className="flex flex-col items-center pt-1">
              <StageMark status={stage} muted={!item} className="size-3.5" />
              {!isLast && (
                <span
                  aria-hidden="true"
                  className={cn(
                    "mt-1.5 w-px flex-1",
                    // Solid where the order has already travelled, dotted for the
                    // step it has not taken yet.
                    nextReached
                      ? STAGE_LINE[stage]
                      : "bg-[linear-gradient(to_bottom,currentColor_50%,transparent_50%)] bg-[length:1px_6px] text-muted-foreground/40"
                  )}
                />
              )}
            </div>

            <div className={cn("min-w-0 flex-1", isLast ? "pb-0" : "pb-6")}>
              <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <span className={cn("text-sm", item ? STAGE_COLOR[stage] : "text-muted-foreground/60")}>
                  {stage}
                </span>
                {item ? (
                  <LocalDateTime
                    value={item.changedAt}
                    format="time"
                    className="tabular font-mono text-xs text-muted-foreground"
                  />
                ) : (
                  <span className="text-xs text-muted-foreground/60">
                    {/* Once the order has settled, a skipped stage is never coming. */}
                    {settled ? "não ocorreu" : "aguardando"}
                  </span>
                )}
                {item && previous && (
                  <span className="tabular font-mono text-xs text-muted-foreground/70">
                    +{elapsed(previous.changedAt, item.changedAt)}
                  </span>
                )}
              </div>
              {item?.note && <p className="mt-1 text-sm text-muted-foreground">{item.note}</p>}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
