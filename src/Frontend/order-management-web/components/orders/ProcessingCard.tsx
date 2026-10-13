"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";
import type { Order, OrderStatus, ProcessingOutcome } from "@/types/order";
import { StageMark, STAGE_COLOR } from "@/components/orders/StageMark";

const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

/** The status each planned outcome is heading for. */
const OUTCOME_STATUS: Record<ProcessingOutcome, OrderStatus> = {
  Concluir: "Finalizado",
  Falhar: "Falhou",
  Cancelar: "Cancelado",
};

const seconds = (value: number) => `${value.toFixed(1).replace(".", ",")}s`;

export function ProcessingCard({ order, now }: { order: Order; now: number }) {
  const isProcessing = order.status === "Processando";

  // Measured from the moment the order entered Processando, not from when it
  // was created - an order can sit in Pendente for a while if the worker is down.
  const elapsed = isProcessing ? Math.max(0, (now - new Date(order.updatedAt).getTime()) / 1000) : 0;
  const total = order.processingSeconds;
  const progress = Math.min(100, (elapsed / total) * 100);
  const overdue = isProcessing && elapsed > total + 1;

  const planned = OUTCOME_STATUS[order.outcome];

  return (
    <Link
      href={`/orders/${order.id}`}
      className="block rounded border border-border bg-card p-4 transition-colors outline-none hover:border-foreground/35 focus-visible:border-foreground/35 focus-visible:ring-2 focus-visible:ring-ring/40"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate font-medium">{order.product}</p>
          <p className="truncate text-sm text-muted-foreground">{order.customer}</p>
        </div>
        <span className={cn("flex items-center gap-1.5 whitespace-nowrap text-sm", STAGE_COLOR[order.status])}>
          <StageMark status={order.status} />
          {order.status}
        </span>
      </div>

      <div className="mt-4 space-y-1.5">
        <div
          className="channel h-2 overflow-hidden rounded-[2px]"
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={isProcessing ? Math.round(progress) : 0}
          aria-label={`Progresso do processamento de ${order.product}`}
        >
          {isProcessing ? (
            <div
              className="h-full bg-stage-processing transition-[width] duration-100 ease-linear"
              style={{ width: `${progress}%` }}
            />
          ) : (
            // Pendente: nothing is running yet, so an empty track is the honest
            // picture rather than a bar that pretends to advance.
            <div className="h-full w-0" />
          )}
        </div>

        <div className="flex items-baseline justify-between gap-2 text-xs">
          <span className="figure text-muted-foreground">
            {isProcessing ? `${seconds(elapsed)} / ${seconds(total)}` : `aguardando o worker · ${seconds(total)}`}
          </span>
          {overdue && <span className="text-stage-cancelled">além do previsto</span>}
        </div>
      </div>

      <div className="mt-4 flex items-baseline justify-between gap-3 border-t border-border pt-3">
        <span className="figure text-sm">{currency.format(order.amount)}</span>
        <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
          termina em
          <StageMark status={planned} className="size-2.5" />
          <span className={STAGE_COLOR[planned]}>{planned}</span>
        </span>
      </div>
    </Link>
  );
}
