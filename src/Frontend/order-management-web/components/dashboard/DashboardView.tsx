"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchDashboard } from "@/lib/api";
import type { DashboardSnapshot } from "@/types/dashboard";
import { useOrderRealtime } from "@/hooks/useOrderRealtime";
import { PageHeader } from "@/components/app-shell";
import { NewOrderDialog } from "@/components/orders/NewOrderDialog";
import { ProcessingBoard } from "@/components/orders/ProcessingBoard";
import { RealtimeIndicator } from "@/components/orders/RealtimeIndicator";
import { Cluster, EmptyNote, Panel, PanelBody, PanelHeader } from "@/components/ui/panel";
import { StatTile } from "@/components/dashboard/StatTile";
import { StatusDistribution } from "@/components/dashboard/StatusDistribution";
import { ThroughputChart, WINDOWS, type WindowHours } from "@/components/dashboard/ThroughputChart";
import { cn } from "@/lib/utils";

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
      <div className="space-y-8">
        <PageHeader title="Painel" />
        <EmptyNote
          title="O painel não carregou"
          hint="Verifique se a API está no ar e recarregue a página."
        />
      </div>
    );
  }

  const settled = snapshot.statusBreakdown
    .filter((s) => s.status === "Finalizado" || s.status === "Falhou" || s.status === "Cancelado")
    .reduce((sum, s) => sum + s.count, 0);

  const activeWindow = WINDOWS.find((w) => w.hours === windowHours) ?? WINDOWS[2];

  return (
    <div className="space-y-8">
      <PageHeader
        title="Painel"
        description="O estado do pipeline agora, e como os pedidos vêm terminando."
      >
        <RealtimeIndicator mode={mode} />
        <NewOrderDialog onCreated={refresh} />
      </PageHeader>

      <Cluster className="lg:grid-cols-4">
        <StatTile
          label="Em andamento"
          value={String(snapshot.inFlight)}
          meta={`${currency.format(snapshot.amountInFlight)} em jogo`}
          accent={snapshot.inFlight > 0 ? "text-stage-processing" : undefined}
        />
        <StatTile
          label="Criados hoje"
          value={String(snapshot.createdToday)}
          meta="desde a meia-noite"
        />
        <StatTile
          label="Tempo médio até finalizar"
          value={
            snapshot.averageProcessingSeconds === null
              ? "—"
              : snapshot.averageProcessingSeconds.toFixed(1).replace(".", ",")
          }
          unit={snapshot.averageProcessingSeconds === null ? undefined : "s"}
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
      </Cluster>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1.55fr)_minmax(0,1fr)]">
        <Panel>
          <PanelHeader title="Pedidos criados" meta={`uma coluna por ${activeWindow.bucket}`}>
            <div
              className="surface flex overflow-hidden rounded-lg border border-border bg-card"
              role="group"
              aria-label="Janela de tempo"
            >
              {WINDOWS.map((option) => (
                <button
                  key={option.hours}
                  type="button"
                  onClick={() => setWindowHours(option.hours)}
                  aria-pressed={option.hours === windowHours}
                  className={cn(
                    "px-3 py-1.5 text-xs transition-colors not-first:border-l not-first:border-border",
                    option.hours === windowHours
                      ? "bg-foreground font-medium text-background"
                      : "text-muted-foreground hover:bg-accent hover:text-foreground"
                  )}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </PanelHeader>
          <PanelBody>
            <ThroughputChart data={snapshot.throughput} />
          </PanelBody>
        </Panel>

        <Panel className="flex flex-col">
          <PanelHeader
            title="Distribuição por status"
            meta="todos os pedidos, não só a janela acima"
          />
          <PanelBody className="flex-1">
            <StatusDistribution slices={snapshot.statusBreakdown} />
          </PanelBody>
        </Panel>
      </div>

      <section className="space-y-4">
        <div className="flex items-baseline justify-between gap-4 border-b pb-3">
          <h2 className="font-condensed text-base leading-none font-semibold tracking-tight">
            Em andamento agora
          </h2>
          <span className="figure text-xs text-muted-foreground">
            {snapshot.inFlightOrders.length}
          </span>
        </div>
        <ProcessingBoard orders={snapshot.inFlightOrders} />
      </section>
    </div>
  );
}
