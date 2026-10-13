import { cn } from "@/lib/utils";
import type { OrderStatus } from "@/types/order";
import { StageMark, STAGE_COLOR } from "@/components/orders/StageMark";

export function OrderStatusBadge({ status, className }: { status: OrderStatus; className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 whitespace-nowrap text-sm",
        STAGE_COLOR[status],
        className
      )}
    >
      <StageMark status={status} />
      {status}
    </span>
  );
}
