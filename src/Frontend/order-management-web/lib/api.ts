import type { CreateOrderInput, Order, OrderDetail } from "@/types/order";
import type { AnalyticsAnswer, AnalyticsStatus } from "@/types/analytics";
import type { Product } from "@/types/product";
import type { DashboardSnapshot } from "@/types/dashboard";
import type { OrdersQuery, Paged } from "@/types/paged";

// Server-side rendering runs inside the Docker network and must reach the
// Api container by its service name (API_INTERNAL_URL, e.g. http://api:8080).
// The browser runs on the host and needs the publicly published port
// instead (NEXT_PUBLIC_API_URL, inlined into the client bundle at build
// time). Both default to the same value for local (non-Docker) development.
const API_URL =
  typeof window === "undefined"
    ? process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080"
    : process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(response.status, `Request failed with status ${response.status}`);
  }
  return (await response.json()) as T;
}

export async function fetchOrders(query: OrdersQuery = {}): Promise<Paged<Order>> {
  const params = new URLSearchParams();
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.status) params.set("status", query.status);
  if (query.search) params.set("search", query.search);
  if (query.sort) params.set("sort", query.sort);
  if (query.ascending !== undefined) params.set("ascending", String(query.ascending));

  const qs = params.toString();
  const response = await fetch(`${API_URL}/orders${qs ? `?${qs}` : ""}`, { cache: "no-store" });
  return handleResponse<Paged<Order>>(response);
}

/** @param hours Throughput window: 1, 6 or 24. Shorter windows use finer buckets. */
export async function fetchDashboard(hours = 24): Promise<DashboardSnapshot> {
  const response = await fetch(`${API_URL}/dashboard?hours=${hours}`, { cache: "no-store" });
  return handleResponse<DashboardSnapshot>(response);
}

export async function fetchOrderById(id: string): Promise<OrderDetail> {
  const response = await fetch(`${API_URL}/orders/${id}`, { cache: "no-store" });
  return handleResponse<OrderDetail>(response);
}

export async function createOrder(input: CreateOrderInput): Promise<Order> {
  const response = await fetch(`${API_URL}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  return handleResponse<Order>(response);
}

export async function fetchProducts(): Promise<Product[]> {
  const response = await fetch(`${API_URL}/products`, { cache: "no-store" });
  return handleResponse<Product[]>(response);
}

export async function fetchAnalyticsStatus(): Promise<AnalyticsStatus> {
  const response = await fetch(`${API_URL}/analytics/status`, { cache: "no-store" });
  return handleResponse<AnalyticsStatus>(response);
}

export async function askAnalytics(question: string): Promise<AnalyticsAnswer> {
  const response = await fetch(`${API_URL}/analytics/ask`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question }),
  });
  return handleResponse<AnalyticsAnswer>(response);
}

export { API_URL };
