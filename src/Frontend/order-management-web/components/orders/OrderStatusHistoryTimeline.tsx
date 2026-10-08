import type { OrderStatusHistoryItem } from "@/types/order";
import { OrderStatusBadge } from "@/components/orders/OrderStatusBadge";

const dateFormatter = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "medium" });

export function OrderStatusHistoryTimeline({ history }: { history: OrderStatusHistoryItem[] }) {
  return (
    <ol className="space-y-4">
      {history.map((item, index) => (
        <li key={`${item.status}-${item.changedAt}`} className="flex gap-3">
          <div className="flex flex-col items-center">
            <span className={`h-2.5 w-2.5 rounded-full ${index === history.length - 1 ? "bg-gray-900 dark:bg-gray-100" : "bg-gray-300 dark:bg-gray-700"}`} />
            {index < history.length - 1 && <span className="mt-1 h-full w-px bg-gray-200 dark:bg-gray-800" />}
          </div>
          <div className="pb-4">
            <OrderStatusBadge status={item.status} />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{dateFormatter.format(new Date(item.changedAt))}</p>
            {item.note && <p className="mt-0.5 text-sm text-gray-600 dark:text-gray-300">{item.note}</p>}
          </div>
        </li>
      ))}
    </ol>
  );
}
