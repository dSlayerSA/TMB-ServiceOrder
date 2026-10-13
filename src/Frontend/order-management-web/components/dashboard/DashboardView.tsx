"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchDashboard } from "@/lib/api";
import type { DashboardSnapshot } from "@/types/dashboard";
import { useOrderRealtime } from "@/hooks/useOrderRealtime";
import { NewOrderDialog } from "@/components/orders/NewOrderDialog";
import { ProcessingBoard } from "@/components/orders/ProcessingBoard";
import { RealtimeIndicator } from "@/components/orders/RealtimeIndicator";
import { StatTile } from "@/components/dashboard/StatTile";
import { StatusDistribution } from "@/components/dashboard/StatusDistribution";
import { ThroughputChart, type WindowHours } from "@/components/dashboard/ThroughputChart";

const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const percent = new Intl.NumberFormat("pt-BR", { style: "percent", maximumFractionDigits: 0 });

export function DashboardView({ initial }: { initial: DashboardSnapshot | null }) {
  const [snapshot, setSnapshot] = useState(initial);
  const [windowHours, setWindowHours] = useState<WindowHours>(24);

  const refresh = useCallback(() => {
    fetchDashboard(windowHours)
      .then(setSnapshot)
      .catch(() => {
        /* keep the last good snapshot on a transient error */
      });
  }, [windowHours]);

  const isFirstRender = useRef(true);
  useEffect(() => {
    // The initial snapshot came from the server at the default window.
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    refresh();
  }, [refresh]);

  const mode = useOrderRealtime(undefined, refresh);

  if (!snapshot) {
    return (
      <p className="rounded-lg border border-dashed px-4 py-10 text-center text-sm text-muted-foreground">
        O painel não carregou. Verifique se a API está no ar.
      </p>
    );
  }

  const settled = snapshot.statusBreakdown
    .filter((s) => s.status === "Finalizado" || s.status === "Falhou" || s.status === "Cancelado")
    .reduce((sum, s) => sum + s.count, 0);

  return (
    <div className="space-y-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-xl font-semibold tracking-tight">Painel</h1>
          <p className="text-sm text-muted-foreground">
            O estado do pipeline agora, e como os pedidos vêm terminando.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <RealtimeIndicator mode={mode} />
          <NewOrderDialog onCreated={refresh} />
        </div>
      </header>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          label="Em andamento"
          value={String(snapshot.inFlight)}
          meta={currency.format(snapshot.amountInFlight) + " em jogo"}
          accent={snapshot.inFlight > 0 ? "text-stage-processing" : undefined}
        />
        <StatTile label="Criados hoje" value={String(snapshot.createdToday)} />
        <StatTile
          label="Tempo médio até finalizar"
          value={
            snapshot.averageProcessingSeconds === null
              ? "—"
              : `${snapshot.averageProcessingSeconds.toFixed(1).replace(".", ",")}s`
          }
          meta="somente pedidos finalizados"
        />
        <StatTile
          label="Taxa de conclusão"
          value={snapshot.completionRate === null ? "—" : percent.format(snapshot.completionRate)}
          meta={settled === 0 ? "nada concluído ainda" : `de ${settled} pedidos encerrados`}
          accent={
            snapshot.completionRate !== null && snapshot.completionRate < 0.8
              ? "text-stage-failed"
              : undefined
          }
        />
      </div>

      <section className="space-y-3">
        <h2 className="text-sm font-medium">Pedidos criados</h2>
        <div className="rounded-lg border bg-card p-4">
          <ThroughputChart
            data={snapshot.throughput}
            windowHours={windowHours}
            onWindowChange={setWindowHours}
          />
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-medium">Distribuição por status</h2>
        <div className="rounded-lg border bg-card p-4">
          <StatusDistribution slices={snapshot.statusBreakdown} />
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-medium">Em andamento agora</h2>
        <ProcessingBoard orders={snapshot.inFlightOrders} />
      </section>
    </div>
  );
}
