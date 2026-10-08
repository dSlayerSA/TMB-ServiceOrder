import Link from "next/link";
import { OrderForm } from "@/components/orders/OrderForm";

export default function NewOrderPage() {
  return (
    <main className="mx-auto max-w-md px-4 py-10 sm:px-6">
      <Link href="/orders" className="text-sm text-gray-500 hover:underline dark:text-gray-400">
        &larr; Voltar para pedidos
      </Link>
      <h1 className="mt-2 text-2xl font-semibold text-gray-900 dark:text-gray-50">Novo pedido</h1>
      <div className="mt-6">
        <OrderForm />
      </div>
    </main>
  );
}
