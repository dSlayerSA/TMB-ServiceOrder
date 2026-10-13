"use client";

import type { Order } from "@/types/order";
import { useNow } from "@/hooks/useNow";
import { isTerminal } from "@/components/orders/StageMark";
import { ProcessingCard } from "@/components/orders/ProcessingCard";
import { EmptyNote } from "@/components/ui/panel";

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
      <EmptyNote
        title="Nenhum pedido em andamento"
        hint="Crie um pedido para ver o worker trabalhando."
      />
    );
  }

  return (
    <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {inFlight.map((order) => (
        <li key={order.id}>
          <ProcessingCard order={order} now={now} />
        </li>
      ))}
    </ul>
  );
}
