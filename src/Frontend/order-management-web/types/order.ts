export type OrderStatus = "Pendente" | "Processando" | "Finalizado" | "Falhou" | "Cancelado";

/** What the worker should do when an order's processing window elapses. */
export type ProcessingOutcome = "Concluir" | "Falhar" | "Cancelar";

export interface Order {
  id: string;
  customer: string;
  product: string;
  amount: number;
  status: OrderStatus;
  createdAt: string;
  updatedAt: string;
  processingSeconds: number;
  outcome: ProcessingOutcome;
}

export interface OrderStatusHistoryItem {
  status: OrderStatus;
  changedAt: string;
  note: string | null;
}

export interface OrderDetail extends Order {
  history: OrderStatusHistoryItem[];
}

export interface CreateOrderInput {
  customer: string;
  productId: string;
  processingSeconds?: number;
  outcome?: ProcessingOutcome;
}
