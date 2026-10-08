import { fetchOrders } from "@/lib/api";
import { OrdersTable } from "@/components/orders/OrdersTable";

export default async function OrdersPage() {
  const orders = await fetchOrders().catch(() => []);

  return (
    <main className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-50">Pedidos</h1>
      <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Acompanhe seus pedidos e o processamento assíncrono em tempo real.
      </p>
      <div className="mt-6">
        <OrdersTable initialOrders={orders} />
      </div>
    </main>
  );
}
