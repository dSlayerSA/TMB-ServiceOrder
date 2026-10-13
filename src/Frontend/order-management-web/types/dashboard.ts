import type { Order, OrderStatus } from "@/types/order";

export interface StatusSlice {
  status: OrderStatus;
  count: number;
  totalAmount: number;
}

export interface ThroughputPoint {
  bucketUtc: string;
  /** The bucket still being filled - not comparable to the closed ones. */
  isCurrent: boolean;
  total: number;
  pendente: number;
  processando: number;
  finalizado: number;
  falhou: number;
  cancelado: number;
}

export interface DashboardSnapshot {
  inFlight: number;
  createdToday: number;
  amountInFlight: number;
  averageProcessingSeconds: number | null;
  /** Share of settled orders that reached Finalizado. Null when nothing has settled. */
  completionRate: number | null;
  statusBreakdown: StatusSlice[];
  throughput: ThroughputPoint[];
  inFlightOrders: Order[];
}
