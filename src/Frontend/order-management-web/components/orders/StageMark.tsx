import { cn } from "@/lib/utils";
import type { OrderStatus } from "@/types/order";

/** The mandatory happy path, in order. Drives the timeline and the pipeline bar. */
export const PIPELINE_STAGES: OrderStatus[] = ["Pendente", "Processando", "Finalizado"];

/** Endings that replace Finalizado when the worker does not complete the order. */
export const ALTERNATIVE_ENDINGS: OrderStatus[] = ["Falhou", "Cancelado"];

export const ALL_STATUSES: OrderStatus[] = [...PIPELINE_STAGES, ...ALTERNATIVE_ENDINGS];

export const TERMINAL_STATUSES: OrderStatus[] = ["Finalizado", "Falhou", "Cancelado"];

export const isTerminal = (status: OrderStatus) => TERMINAL_STATUSES.includes(status);

export const STAGE_COLOR: Record<OrderStatus, string> = {
  Pendente: "text-stage-pending",
  Processando: "text-stage-processing",
  Finalizado: "text-stage-done",
  Falhou: "text-stage-failed",
  Cancelado: "text-stage-cancelled",
};

export const STAGE_BG: Record<OrderStatus, string> = {
  Pendente: "bg-stage-pending",
  Processando: "bg-stage-processing",
  Finalizado: "bg-stage-done",
  Falhou: "bg-stage-failed",
  Cancelado: "bg-stage-cancelled",
};

/**
 * The raw token, for the one case that needs a value rather than a class: the
 * wash a row gets when its status changes under the viewer.
 */
export const STAGE_VAR: Record<OrderStatus, string> = {
  Pendente: "var(--stage-pending)",
  Processando: "var(--stage-processing)",
  Finalizado: "var(--stage-done)",
  Falhou: "var(--stage-failed)",
  Cancelado: "var(--stage-cancelled)",
};

/** Travelled segments of the timeline, dimmed so the marks stay dominant. */
export const STAGE_LINE: Record<OrderStatus, string> = {
  Pendente: "bg-stage-pending/50",
  Processando: "bg-stage-processing/50",
  Finalizado: "bg-stage-done/50",
  Falhou: "bg-stage-failed/50",
  Cancelado: "bg-stage-cancelled/50",
};

/**
 * The shape carries the meaning, so the five states stay distinguishable
 * without relying on colour alone: hollow (waiting), cored (in flight), solid
 * (done), crossed (failed), barred (cancelled). Only "Processando" breathes,
 * because it is the only state where work is actually happening.
 */
export function StageMark({
  status,
  className,
  muted = false,
}: {
  status: OrderStatus;
  className?: string;
  muted?: boolean;
}) {
  return (
    <svg
      viewBox="0 0 12 12"
      aria-hidden="true"
      className={cn(
        "size-3 shrink-0",
        muted ? "text-muted-foreground/40" : STAGE_COLOR[status],
        status === "Processando" && !muted && "stage-breathe",
        className
      )}
    >
      <circle cx="6" cy="6" r="4.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
      {status === "Processando" && <circle cx="6" cy="6" r="2" fill="currentColor" />}
      {status === "Finalizado" && <circle cx="6" cy="6" r="4.5" fill="currentColor" />}
      {status === "Falhou" && (
        <path
          d="M4.2 4.2 L7.8 7.8 M7.8 4.2 L4.2 7.8"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
        />
      )}
      {status === "Cancelado" && (
        <path d="M3.9 6 H8.1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      )}
    </svg>
  );
}
