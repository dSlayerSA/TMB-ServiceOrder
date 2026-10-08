import type { RealtimeMode } from "@/hooks/useOrderRealtime";

const LABELS: Record<RealtimeMode, { text: string; dot: string }> = {
  connecting: { text: "Conectando...", dot: "bg-gray-400" },
  realtime: { text: "Tempo real", dot: "bg-emerald-500" },
  polling: { text: "Atualizando periodicamente", dot: "bg-amber-500" },
};

export function RealtimeIndicator({ mode }: { mode: RealtimeMode }) {
  const { text, dot } = LABELS[mode];
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
      <span className={`h-1.5 w-1.5 rounded-full ${dot} ${mode === "realtime" ? "animate-pulse" : ""}`} />
      {text}
    </span>
  );
}
