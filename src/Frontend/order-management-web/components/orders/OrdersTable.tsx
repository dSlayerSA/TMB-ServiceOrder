"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Search } from "lucide-react";
import { fetchOrders } from "@/lib/api";
import type { Order, OrderStatus } from "@/types/order";
import type { OrdersQuery, Paged } from "@/types/paged";
import { cn } from "@/lib/utils";
import { LocalDateTime } from "@/components/LocalDateTime";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ALL_STATUSES, StageMark, STAGE_COLOR } from "@/components/orders/StageMark";
import { NewOrderDialog } from "@/components/orders/NewOrderDialog";
import { RealtimeIndicator } from "@/components/orders/RealtimeIndicator";
import { useOrderRealtime } from "@/hooks/useOrderRealtime";

const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

type SortField = NonNullable<OrdersQuery["sort"]>;

const COLUMNS: { field: SortField | null; label: string; align?: "right" }[] = [
  { field: "Customer", label: "Cliente" },
  { field: null, label: "Produto" },
  { field: "Amount", label: "Valor", align: "right" },
  { field: "Status", label: "Status" },
  { field: "CreatedAt", label: "Criado em", align: "right" },
];

const PAGE_SIZES = [10, 20, 50];

export function OrdersTable({ initial }: { initial: Paged<Order> }) {
  const router = useRouter();
  const [page, setPage] = useState(initial);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [status, setStatus] = useState<OrderStatus | "Todos">("Todos");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [sort, setSort] = useState<SortField>("CreatedAt");
  const [ascending, setAscending] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  // Typing should not fire a request per keystroke.
  useEffect(() => {
    const id = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(id);
  }, [searchInput]);

  const query = useMemo<OrdersQuery>(
    () => ({
      page: pageNumber,
      pageSize,
      status: status === "Todos" ? undefined : status,
      search: search || undefined,
      sort,
      ascending,
    }),
    [pageNumber, pageSize, status, search, sort, ascending]
  );

  // Kept in a ref so the realtime refresh always reads the current filters
  // without resubscribing to the hub whenever they change.
  const queryRef = useRef(query);
  queryRef.current = query;

  const load = useCallback(() => {
    setIsLoading(true);
    fetchOrders(queryRef.current)
      .then(setPage)
      .catch(() => {
        /* keep the last good page on a transient error */
      })
      .finally(() => setIsLoading(false));
  }, []);

  const isFirstRender = useRef(true);
  useEffect(() => {
    // The first page came from the server, so it is not refetched on mount.
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    load();
  }, [query, load]);

  const mode = useOrderRealtime(undefined, load);

  const toggleSort = (field: SortField) => {
    if (sort === field) {
      setAscending((previous) => !previous);
    } else {
      setSort(field);
      setAscending(false);
    }
    setPageNumber(1);
  };

  const clearFilters = () => {
    setSearchInput("");
    setSearch("");
    setStatus("Todos");
    setPageNumber(1);
  };

  const from = page.total === 0 ? 0 : (page.page - 1) * page.pageSize + 1;
  const to = Math.min(page.page * page.pageSize, page.total);
  const hasFilters = status !== "Todos" || search.length > 0;

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-xl font-semibold tracking-tight">Pedidos</h1>
          <p className="text-sm text-muted-foreground">
            Cada pedido percorre o pipeline sozinho, movido pelo worker.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <RealtimeIndicator mode={mode} />
          <NewOrderDialog onCreated={load} />
        </div>
      </header>

      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-52 flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={searchInput}
            onChange={(event) => {
              setSearchInput(event.target.value);
              setPageNumber(1);
            }}
            placeholder="Buscar por cliente ou produto"
            aria-label="Buscar pedidos"
            className="pl-8"
          />
        </div>

        <Select
          items={[
            { value: "Todos", label: "Todos os status" },
            ...ALL_STATUSES.map((s) => ({ value: s, label: s })),
          ]}
          value={status}
          onValueChange={(value) => {
            setStatus((value as OrderStatus | "Todos") ?? "Todos");
            setPageNumber(1);
          }}
        >
          <SelectTrigger aria-label="Filtrar por status" className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Todos">Todos os status</SelectItem>
            {ALL_STATUSES.map((option) => (
              <SelectItem key={option} value={option}>
                <StageMark status={option} className="size-2.5" />
                <span className="flex-1">{option}</span>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {hasFilters && (
          <Button variant="ghost" size="sm" onClick={clearFilters}>
            Limpar
          </Button>
        )}
      </div>

      {page.items.length === 0 ? (
        <div className="rounded-lg border border-dashed p-10 text-center">
          <p className="text-sm font-medium">
            {hasFilters ? "Nenhum pedido corresponde ao filtro" : "Nenhum pedido ainda"}
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters
              ? "Ajuste a busca ou o status para ver mais resultados."
              : "Crie um pedido para ver o pipeline em movimento."}
          </p>
        </div>
      ) : (
        <>
          <div
            className={cn(
              "hidden overflow-hidden rounded-lg border bg-card transition-opacity sm:block",
              isLoading && "opacity-60"
            )}
          >
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  {COLUMNS.map((column) => (
                    <TableHead
                      key={column.label}
                      className={column.align === "right" ? "text-right" : undefined}
                    >
                      {column.field ? (
                        <button
                          type="button"
                          onClick={() => toggleSort(column.field!)}
                          className={cn(
                            "inline-flex items-center gap-1 transition-colors hover:text-foreground",
                            sort === column.field && "text-foreground"
                          )}
                          aria-label={`Ordenar por ${column.label}`}
                        >
                          {column.label}
                          {sort === column.field &&
                            (ascending ? (
                              <ArrowUp className="size-3" />
                            ) : (
                              <ArrowDown className="size-3" />
                            ))}
                        </button>
                      ) : (
                        column.label
                      )}
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {page.items.map((order) => (
                  <TableRow
                    key={order.id}
                    onClick={() => router.push(`/orders/${order.id}`)}
                    className="cursor-pointer"
                  >
                    <TableCell className="font-medium">{order.customer}</TableCell>
                    <TableCell className="text-muted-foreground">{order.product}</TableCell>
                    <TableCell className="tabular text-right font-mono">
                      {currency.format(order.amount)}
                    </TableCell>
                    <TableCell>
                      <span
                        className={cn(
                          "inline-flex items-center gap-1.5 whitespace-nowrap text-sm",
                          STAGE_COLOR[order.status]
                        )}
                      >
                        <StageMark status={order.status} />
                        {order.status}
                      </span>
                    </TableCell>
                    <TableCell className="tabular text-right font-mono text-muted-foreground">
                      <LocalDateTime value={order.createdAt} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <ul className={cn("space-y-2 sm:hidden", isLoading && "opacity-60")}>
            {page.items.map((order) => (
              <li key={order.id}>
                <Link
                  href={`/orders/${order.id}`}
                  className="block rounded-lg border bg-card p-4 transition-colors hover:bg-accent/40"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate font-medium">{order.customer}</p>
                      <p className="truncate text-sm text-muted-foreground">{order.product}</p>
                    </div>
                    <span
                      className={cn(
                        "flex items-center gap-1.5 whitespace-nowrap text-xs",
                        STAGE_COLOR[order.status]
                      )}
                    >
                      <StageMark status={order.status} />
                      {order.status}
                    </span>
                  </div>
                  <div className="mt-3 flex items-baseline justify-between gap-3">
                    <span className="tabular font-mono text-sm">{currency.format(order.amount)}</span>
                    <LocalDateTime
                      value={order.createdAt}
                      className="tabular font-mono text-xs text-muted-foreground"
                    />
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          <nav
            className="flex flex-wrap items-center justify-between gap-3"
            aria-label="Paginação dos pedidos"
          >
            <p className="tabular font-mono text-xs text-muted-foreground">
              {from}–{to} de {page.total}
            </p>

            <div className="flex items-center gap-2">
              <Select
                items={PAGE_SIZES.map((size) => ({ value: String(size), label: `${size} por página` }))}
                value={String(pageSize)}
                onValueChange={(value) => {
                  setPageSize(Number(value ?? 20));
                  setPageNumber(1);
                }}
              >
                <SelectTrigger aria-label="Itens por página" className="w-36">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAGE_SIZES.map((size) => (
                    <SelectItem key={size} value={String(size)}>
                      {size} por página
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Button
                variant="outline"
                size="sm"
                disabled={page.page <= 1}
                onClick={() => setPageNumber((n) => Math.max(1, n - 1))}
                aria-label="Página anterior"
              >
                <ChevronLeft className="size-4" />
              </Button>
              <span className="tabular font-mono text-xs text-muted-foreground">
                {page.page} / {page.totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page.page >= page.totalPages}
                onClick={() => setPageNumber((n) => n + 1)}
                aria-label="Próxima página"
              >
                <ChevronRight className="size-4" />
              </Button>
            </div>
          </nav>
        </>
      )}
    </div>
  );
}
