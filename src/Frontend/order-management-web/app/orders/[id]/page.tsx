import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, fetchOrderById } from "@/lib/api";
import { OrderDetailView } from "@/components/orders/OrderDetailView";

export default async function OrderDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  let order;
  try {
    order = await fetchOrderById(id);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }
    throw error;
  }

  return (
    <main className="mx-auto max-w-2xl px-4 py-10 sm:px-6">
      <Link href="/orders" className="text-sm text-gray-500 hover:underline dark:text-gray-400">
        &larr; Voltar para pedidos
      </Link>
      <h1 className="mt-2 text-2xl font-semibold text-gray-900 dark:text-gray-50">Detalhes do pedido</h1>
      <div className="mt-6">
        <OrderDetailView initialOrder={order} />
      </div>
    </main>
  );
}
