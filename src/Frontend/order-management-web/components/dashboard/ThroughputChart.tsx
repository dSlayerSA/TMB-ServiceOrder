"use client";

import { useEffect, useState } from "react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ThroughputPoint } from "@/types/dashboard";
import type { OrderStatus } from "@/types/order";
import { cn } from "@/lib/utils";
import { StageMark } from "@/components/orders/StageMark";

const clock = new Intl.DateTimeFormat("pt-BR", { hour: "2-digit", minute: "2-digit" });

export const WINDOWS = [
  { hours: 1, label: "1h", bucket: "5 min" },
  { hours: 6, label: "6h", bucket: "30 min" },
  { hours: 24, label: "24h", bucket: "1 hora" },
] as const;

export type WindowHours = (typeof WINDOWS)[number]["hours"];

/**
 * Stack order follows the pipeline, so a column reads bottom-up the same way an
 * order travels. Fills come from the chart ramp, which is validated separately
 * from the UI text colours.
 */
const SERIES: { key: keyof ThroughputPoint; status: OrderStatus; fill: string }[] = [
  { key: "pendente", status: "Pendente", fill: "var(--chart-pendente)" },
  { key: "processando", status: "Processando", fill: "var(--chart-processando)" },
  { key: "finalizado", status: "Finalizado", fill: "var(--chart-finalizado)" },
  { key: "falhou", status: "Falhou", fill: "var(--chart-falhou)" },
  { key: "cancelado", status: "Cancelado", fill: "var(--chart-cancelado)" },
];

type Row = Record<string, number | string | boolean> & { label: string; isCurrent: boolean };

/**
 * Orders created per bucket, stacked by the status each one is in now. Column
 * height answers "how many were created", the stack answers "and where did they
 * end up". The window is adjustable because a whole day in hourly buckets
 * collapses a burst from the last few minutes into a single column.
 */
export function ThroughputChart({
  data,
  windowHours,
  onWindowChange,
}: {
  data: ThroughputPoint[];
  windowHours: WindowHours;
  onWindowChange: (hours: WindowHours) => void;
}) {
  // Labels are formatted in the viewer timezone, which the server does not know;
  // rendering them during SSR produces a hydration mismatch. The chart also
  // needs a measured box before ResponsiveContainer can lay it out.
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  const rows: Row[] = data.map((point) => ({
    label: point.isCurrent ? "agora" : clock.format(new Date(point.bucketUtc)),
    exact: clock.format(new Date(point.bucketUtc)),
    isCurrent: point.isCurrent,
    total: point.total,
    pendente: point.pendente,
    processando: point.processando,
    finalizado: point.finalizado,
    falhou: point.falhou,
    cancelado: point.cancelado,
  }));

  const peak = Math.max(...rows.map((r) => Number(r.total)), 1);
  const isEmpty = rows.every((r) => Number(r.total) === 0);
  const activeWindow = WINDOWS.find((w) => w.hours === windowHours) ?? WINDOWS[2];

  return (
    <div className="space-y-4">
      {/* Filters sit in one row above the chart. */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-muted-foreground">
          Uma coluna por {activeWindow.bucket}
        </p>
        <div className="flex gap-1" role="group" aria-label="Janela de tempo">
          {WINDOWS.map((option) => (
            <button
              key={option.hours}
              type="button"
              onClick={() => onWindowChange(option.hours)}
              aria-pressed={option.hours === windowHours}
              className={cn(
                "rounded-md px-2.5 py-1 text-xs transition-colors",
                option.hours === windowHours
                  ? "bg-secondary font-medium text-foreground"
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      {!mounted ? (
        <div className="h-52 w-full" aria-hidden="true" />
      ) : (
        <div className="h-52 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -24 }} barCategoryGap="18%">
              <CartesianGrid stroke="var(--chart-grid)" strokeWidth={1} vertical={false} />

              <XAxis
                dataKey="label"
                tickLine={false}
                axisLine={false}
                interval="preserveStartEnd"
                minTickGap={36}
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
              />
              <YAxis
                allowDecimals={false}
                domain={[0, Math.max(peak, 2)]}
                width={44}
                tickLine={false}
                axisLine={false}
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
              />

              <Tooltip
                cursor={{ fill: "var(--muted)", opacity: 0.5 }}
                content={({ active, payload }) => {
                  if (!active || !payload?.length) return null;
                  const row = payload[0].payload as Row;
                  const present = SERIES.filter((s) => Number(row[s.key]) > 0);

                  return (
                    <div className="min-w-52 rounded-md border bg-popover px-3 py-2 shadow-md">
                      <div className="flex items-baseline justify-between gap-4 border-b pb-1.5">
                        <span className="tabular font-mono text-xs text-muted-foreground">
                          {row.exact}
                        </span>
                        <span className="tabular font-mono text-xs text-popover-foreground">
                          {row.total} {Number(row.total) === 1 ? "pedido" : "pedidos"}
                        </span>
                      </div>

                      {present.length === 0 ? (
                        <p className="pt-1.5 text-xs text-muted-foreground">
                          nenhum pedido neste intervalo
                        </p>
                      ) : (
                        <dl className="space-y-1 pt-1.5">
                          {present.map((series) => (
                            <div
                              key={series.key}
                              className="flex items-baseline justify-between gap-4 text-xs"
                            >
                              <dt className="flex items-center gap-1.5 text-muted-foreground">
                                <StageMark status={series.status} className="size-2.5" />
                                {series.status}
                              </dt>
                              <dd className="tabular font-mono text-popover-foreground">
                                {row[series.key]}
                              </dd>
                            </div>
                          ))}
                        </dl>
                      )}

                      {row.isCurrent && (
                        <p className="mt-1.5 border-t pt-1.5 text-xs text-muted-foreground">
                          Intervalo em curso, ainda enchendo.
                        </p>
                      )}
                    </div>
                  );
                }}
              />

              {SERIES.map((series, index) => (
                <Bar
                  key={series.key}
                  dataKey={series.key}
                  stackId="created"
                  fill={series.fill}
                  // A hairline in the surface colour separates touching segments
                  // with space rather than with a data-ink border.
                  stroke="var(--card)"
                  strokeWidth={1}
                  maxBarSize={24}
                  // Only the topmost segment carries the rounded data-end.
                  radius={index === SERIES.length - 1 ? [3, 3, 0, 0] : 0}
                  isAnimationActive={false}
                />
              ))}
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* Five series, so a legend is mandatory - identity is never colour alone. */}
      <ul className="flex flex-wrap gap-x-5 gap-y-2">
        {SERIES.map((series) => (
          <li key={series.key} className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <span
              aria-hidden="true"
              className="size-2.5 rounded-[2px]"
              style={{ background: series.fill }}
            />
            {series.status}
          </li>
        ))}
      </ul>

      {isEmpty && (
        <p className="text-xs text-muted-foreground">
          Nenhum pedido criado nesta janela. Tente uma janela maior.
        </p>
      )}
    </div>
  );
}
