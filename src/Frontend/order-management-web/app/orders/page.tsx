import { fetchOrders } from "@/lib/api";
import type { Order } from "@/types/order";
import type { Paged } from "@/types/paged";
import { OrdersTable } from "@/components/orders/OrdersTable";

const EMPTY: Paged<Order> = { items: [], page: 1, pageSize: 20, total: 0, totalPages: 1 };

export default async function OrdersPage() {
  const initial = await fetchOrders({ page: 1, pageSize: 20 }).catch(() => EMPTY);

  return (
    <div className="mx-auto w-full max-w-6xl px-5 py-6 sm:px-8 sm:py-8">
      <OrdersTable initial={initial} />
    </div>
  );
}
