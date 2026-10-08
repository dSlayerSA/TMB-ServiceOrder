export type OrderStatus = "Pendente" | "Processando" | "Finalizado";

export interface Order {
  id: string;
  customer: string;
  product: string;
  amount: number;
  status: OrderStatus;
  createdAt: string;
}

export interface OrderStatusHistoryItem {
  status: OrderStatus;
  changedAt: string;
  note: string | null;
}

export interface OrderDetail extends Order {
  updatedAt: string;
  history: OrderStatusHistoryItem[];
}

export interface CreateOrderInput {
  customer: string;
  product: string;
  amount: number;
}
