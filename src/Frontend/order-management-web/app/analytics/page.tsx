import { KeyRound } from "lucide-react";
import { fetchAnalyticsStatus } from "@/lib/api";
import { AnalyticsChat } from "@/components/analytics/AnalyticsChat";
import { PageHeader } from "@/components/app-shell";
import { Panel, PanelBody, PanelHeader } from "@/components/ui/panel";

export default async function AnalyticsPage() {
  // The module is optional, so an unreachable or unconfigured API degrades to
  // the "disabled" explanation rather than an error page.
  const { enabled, provider } = await fetchAnalyticsStatus().catch(() => ({
    enabled: false,
    provider: "Desativado",
  }));

  return (
    <div className="mx-auto w-full max-w-2xl px-5 py-6 sm:px-8 sm:py-8">
      <div className="space-y-8">
        <PageHeader
          title="Perguntar"
          description="Perguntas em linguagem natural, respondidas com os dados reais do banco."
        >
          {enabled && <span className="figure text-xs text-muted-foreground">{provider}</span>}
        </PageHeader>

        {enabled ? (
          <AnalyticsChat />
        ) : (
          <Panel>
            <PanelHeader title="Nenhum provedor configurado" />
            <PanelBody className="flex items-start gap-3">
              <KeyRound className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
              <div className="space-y-3">
                <p className="text-sm text-muted-foreground">
                  O módulo é opcional — o resto do sistema funciona sem ele. Há dois
                  caminhos para ativá-lo:
                </p>
                <dl className="space-y-2 text-sm">
                  <div>
                    <dt className="font-medium">Claude</dt>
                    <dd className="text-muted-foreground">
                      Defina <code className="font-mono text-xs">ANTHROPIC_API_KEY</code> e
                      reinicie a API.
                    </dd>
                  </div>
                  <div>
                    <dt className="font-medium">Modelo local</dt>
                    <dd className="text-muted-foreground">
                      Use <code className="font-mono text-xs">ANALYTICS_PROVIDER=Ollama</code> e
                      suba com <code className="font-mono text-xs">--profile local-ai</code>. Sem
                      custo e sem conta.
                    </dd>
                  </div>
                </dl>
              </div>
            </PanelBody>
          </Panel>
        )}
      </div>
    </div>
  );
}
