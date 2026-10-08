import type { CreateOrderInput, Order, OrderDetail } from "@/types/order";

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

export async function fetchOrders(): Promise<Order[]> {
  const response = await fetch(`${API_URL}/orders`, { cache: "no-store" });
  return handleResponse<Order[]>(response);
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

export { API_URL };
