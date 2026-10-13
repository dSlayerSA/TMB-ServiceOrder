export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface OrdersQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
  sort?: "CreatedAt" | "Amount" | "Customer" | "Status";
  ascending?: boolean;
}
