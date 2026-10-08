"use client";

import { useCallback, useState } from "react";
import { fetchOrderById } from "@/lib/api";
import type { OrderDetail } from "@/types/order";
import { OrderStatusBadge } from "@/components/orders/OrderStatusBadge";
import { OrderStatusHistoryTimeline } from "@/components/orders/OrderStatusHistoryTimeline";
import { RealtimeIndicator } from "@/components/orders/RealtimeIndicator";
import { useOrderRealtime } from "@/hooks/useOrderRealtime";

const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

export function OrderDetailView({ initialOrder }: { initialOrder: OrderDetail }) {
  const [order, setOrder] = useState(initialOrder);
  const [justUpdated, setJustUpdated] = useState(false);

  const refresh = useCallback(() => {
    fetchOrderById(initialOrder.id)
      .then((updated) => {
        setOrder(updated);
        setJustUpdated(true);
        setTimeout(() => setJustUpdated(false), 1500);
      })
      .catch(() => {
        /* keep showing the last known state on transient errors */
      });
  }, [initialOrder.id]);

  const mode = useOrderRealtime(order.id, refresh);

  return (
    <div className="space-y-6">
      <div
        className={`rounded-lg border p-5 transition-colors ${
          justUpdated ? "border-blue-400 bg-blue-50 dark:border-blue-500 dark:bg-blue-500/10" : "border-gray-200 dark:border-gray-800"
        }`}
      >
        <div className="flex items-start justify-between">
          <div>
            <h2 className="text-lg font-medium text-gray-900 dark:text-gray-50">{order.customer}</h2>
            <p className="text-sm text-gray-500 dark:text-gray-400">{order.product}</p>
          </div>
          <OrderStatusBadge status={order.status} />
        </div>
        <dl className="mt-4 grid grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-gray-500 dark:text-gray-400">Valor</dt>
            <dd className="font-medium text-gray-900 dark:text-gray-50">{currencyFormatter.format(order.amount)}</dd>
          </div>
          <div>
            <dt className="text-gray-500 dark:text-gray-400">Pedido</dt>
            <dd className="font-mono text-xs text-gray-700 dark:text-gray-300">{order.id}</dd>
          </div>
        </dl>
        <div className="mt-4">
          <RealtimeIndicator mode={mode} />
        </div>
      </div>

      <div>
        <h3 className="mb-3 text-sm font-semibold text-gray-700 dark:text-gray-300">Histórico de status</h3>
        <OrderStatusHistoryTimeline history={order.history} />
      </div>
    </div>
  );
}
