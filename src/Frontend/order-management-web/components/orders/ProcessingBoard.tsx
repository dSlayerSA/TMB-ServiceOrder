"use client";

import type { Order } from "@/types/order";
import { useNow } from "@/hooks/useNow";
import { isTerminal } from "@/components/orders/StageMark";
import { ProcessingCard } from "@/components/orders/ProcessingCard";

/**
 * The orders currently in flight, each with its own progress. This is the part
 * of the board worth watching: everything below it has already settled.
 */
export function ProcessingBoard({ orders }: { orders: Order[] }) {
  const inFlight = orders.filter((order) => !isTerminal(order.status));

  // The clock only runs while something is actually being processed.
  const now = useNow(100, inFlight.some((order) => order.status === "Processando"));

  if (inFlight.length === 0) {
    return (
      <p className="rounded-lg border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">
        Nenhum pedido em andamento. Crie um para ver o worker trabalhando.
      </p>
    );
  }

  return (
    <ul className="grid gap-3 sm:grid-cols-2">
      {inFlight.map((order) => (
        <li key={order.id}>
          <ProcessingCard order={order} now={now} />
        </li>
      ))}
    </ul>
  );
}
