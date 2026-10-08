"use client";

import { useCallback, useState } from "react";
import Link from "next/link";
import { fetchOrders } from "@/lib/api";
import type { Order } from "@/types/order";
import { OrderStatusBadge } from "@/components/orders/OrderStatusBadge";
import { RealtimeIndicator } from "@/components/orders/RealtimeIndicator";
import { useOrderRealtime } from "@/hooks/useOrderRealtime";

const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const dateFormatter = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" });

export function OrdersTable({ initialOrders }: { initialOrders: Order[] }) {
  const [orders, setOrders] = useState(initialOrders);

  const refresh = useCallback(() => {
    fetchOrders()
      .then(setOrders)
      .catch(() => {
        /* keep showing the last known list on transient errors */
      });
  }, []);

  const mode = useOrderRealtime(undefined, refresh);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <RealtimeIndicator mode={mode} />
        <Link
          href="/orders/new"
          className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700 dark:bg-gray-100 dark:text-gray-900 dark:hover:bg-white"
        >
          Novo pedido
        </Link>
      </div>

      {orders.length === 0 ? (
        <p className="rounded-md border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
          Nenhum pedido ainda. Crie o primeiro pedido para começar.
        </p>
      ) : (
        <>
          {/* Desktop: table */}
          <div className="hidden overflow-x-auto rounded-lg border border-gray-200 sm:block dark:border-gray-800">
            <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-800">
              <thead className="bg-gray-50 dark:bg-gray-900">
                <tr>
                  <Th>Cliente</Th>
                  <Th>Produto</Th>
                  <Th>Valor</Th>
                  <Th>Status</Th>
                  <Th>Criado em</Th>
                  <Th>&nbsp;</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white dark:divide-gray-800 dark:bg-gray-950">
                {orders.map((order) => (
                  <tr key={order.id} className="transition-colors hover:bg-gray-50 dark:hover:bg-gray-900">
                    <Td>{order.customer}</Td>
                    <Td>{order.product}</Td>
                    <Td>{currencyFormatter.format(order.amount)}</Td>
                    <Td><OrderStatusBadge status={order.status} /></Td>
                    <Td>{dateFormatter.format(new Date(order.createdAt))}</Td>
                    <Td>
                      <Link href={`/orders/${order.id}`} className="font-medium text-blue-600 hover:underline dark:text-blue-400">
                        Detalhes
                      </Link>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile: cards */}
          <ul className="space-y-3 sm:hidden">
            {orders.map((order) => (
              <li key={order.id} className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{order.customer}</span>
                  <OrderStatusBadge status={order.status} />
                </div>
                <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{order.product}</p>
                <div className="mt-2 flex items-center justify-between text-sm">
                  <span>{currencyFormatter.format(order.amount)}</span>
                  <Link href={`/orders/${order.id}`} className="font-medium text-blue-600 hover:underline dark:text-blue-400">
                    Detalhes
                  </Link>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function Th({ children }: { children: React.ReactNode }) {
  return (
    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
      {children}
    </th>
  );
}

function Td({ children }: { children: React.ReactNode }) {
  return <td className="px-4 py-3 text-sm text-gray-900 dark:text-gray-100">{children}</td>;
}
