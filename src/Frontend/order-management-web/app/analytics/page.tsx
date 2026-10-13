import { KeyRound } from "lucide-react";
import { fetchAnalyticsStatus } from "@/lib/api";
import { AnalyticsChat } from "@/components/analytics/AnalyticsChat";

export default async function AnalyticsPage() {
  // The module is optional, so an unreachable or unconfigured API degrades to
  // the "disabled" explanation rather than an error page.
  const { enabled, provider } = await fetchAnalyticsStatus().catch(() => ({
    enabled: false,
    provider: "Desativado",
  }));

  return (
    <div className="mx-auto w-full max-w-2xl px-5 py-8 sm:px-8 sm:py-10">
      <header className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight">Perguntar</h1>
        <p className="text-sm text-muted-foreground">
          Perguntas em linguagem natural, respondidas com os dados reais do banco.
        </p>
      </header>

      <div className="mt-8">
        {enabled ? (
          <>
            <p className="mb-4 text-xs text-muted-foreground">
              Respondendo com <span className="font-mono">{provider}</span>
            </p>
            <AnalyticsChat />
          </>
        ) : (
          <div className="rounded-lg border bg-card p-5">
            <div className="flex items-start gap-3">
              <KeyRound className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
              <div className="space-y-3">
                <div className="space-y-1">
                  <p className="text-sm font-medium">Nenhum provedor configurado</p>
                  <p className="text-sm text-muted-foreground">
                    O módulo é opcional — o resto do sistema funciona sem ele. Há dois
                    caminhos para ativá-lo:
                  </p>
                </div>
                <ul className="space-y-2 text-sm text-muted-foreground">
                  <li>
                    <span className="text-foreground">Claude</span> — defina{" "}
                    <code className="font-mono text-xs">ANTHROPIC_API_KEY</code> e reinicie a API.
                  </li>
                  <li>
                    <span className="text-foreground">Modelo local</span> — use{" "}
                    <code className="font-mono text-xs">ANALYTICS_PROVIDER=Ollama</code> e suba com{" "}
                    <code className="font-mono text-xs">--profile local-ai</code>. Sem custo e sem conta.
                  </li>
                </ul>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
