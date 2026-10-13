"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchOrderById } from "@/lib/api";
import type { OrderDetail } from "@/types/order";
import { cn } from "@/lib/utils";
import { LocalDateTime } from "@/components/LocalDateTime";
import { Panel } from "@/components/ui/panel";
import { OrderStatusBadge } from "@/components/orders/OrderStatusBadge";
import { OrderStatusHistoryTimeline } from "@/components/orders/OrderStatusHistoryTimeline";
import { RealtimeIndicator } from "@/components/orders/RealtimeIndicator";
import { useOrderRealtime } from "@/hooks/useOrderRealtime";

const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

export function OrderDetailView({ initialOrder }: { initialOrder: OrderDetail }) {
  const [order, setOrder] = useState(initialOrder);
  const [justUpdated, setJustUpdated] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const refresh = useCallback(() => {
    fetchOrderById(initialOrder.id)
      .then((updated) => {
        setOrder(updated);
        // A brief ring is the one place motion is justified here: it points at
        // a change the user did not trigger.
        setJustUpdated(true);
        if (timer.current) clearTimeout(timer.current);
        timer.current = setTimeout(() => setJustUpdated(false), 1200);
      })
      .catch(() => {
        /* keep showing the last known state on transient errors */
      });
  }, [initialOrder.id]);

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  const mode = useOrderRealtime(order.id, refresh);

  return (
    <div className="space-y-8">
      <Panel
        className={cn(
          "overflow-hidden transition-shadow",
          justUpdated && "ring-2 ring-foreground/25"
        )}
      >
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-border px-4 py-3.5">
          <div className="min-w-0">
            <h1 className="font-condensed truncate text-xl leading-tight font-semibold tracking-tight">
              {order.product}
            </h1>
            <p className="truncate text-sm text-muted-foreground">{order.customer}</p>
          </div>
          <OrderStatusBadge status={order.status} />
        </div>

        <dl className="grid grid-cols-1 gap-px bg-border sm:grid-cols-2 lg:grid-cols-4">
          <div className="bg-card px-4 py-3">
            <dt className="label">Valor</dt>
            <dd className="figure mt-1 text-lg leading-none">{currency.format(order.amount)}</dd>
          </div>
          <div className="bg-card px-4 py-3">
            <dt className="label">Criado em</dt>
            <dd className="figure mt-1 text-sm">
              <LocalDateTime value={order.createdAt} format="long" />
            </dd>
          </div>
          <div className="bg-card px-4 py-3">
            <dt className="label">Processamento</dt>
            <dd className="figure mt-1 text-sm">
              {order.processingSeconds}s · {order.outcome}
            </dd>
          </div>
          <div className="min-w-0 bg-card px-4 py-3">
            <dt className="label">Identificador</dt>
            <dd className="mt-1 truncate font-mono text-sm text-muted-foreground" title={order.id}>
              {order.id}
            </dd>
          </div>
        </dl>
      </Panel>

      <section className="space-y-5">
        <div className="flex items-baseline justify-between gap-4 border-b pb-3">
          <h2 className="font-condensed text-base leading-none font-semibold tracking-tight">
            Percurso do pedido
          </h2>
          <RealtimeIndicator mode={mode} />
        </div>
        <OrderStatusHistoryTimeline history={order.history} status={order.status} />
      </section>
    </div>
  );
}
