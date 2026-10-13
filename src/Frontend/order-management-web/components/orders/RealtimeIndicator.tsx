import { cn } from "@/lib/utils";
import type { RealtimeMode } from "@/hooks/useOrderRealtime";

const LABELS: Record<RealtimeMode, string> = {
  connecting: "Conectando",
  realtime: "Ao vivo",
  polling: "Atualizando a cada 3s",
};

export function RealtimeIndicator({ mode }: { mode: RealtimeMode }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
      <span
        className={cn(
          "size-1.5 rounded-full",
          mode === "realtime" && "bg-stage-processing stage-breathe",
          mode === "polling" && "bg-stage-pending",
          mode === "connecting" && "bg-muted-foreground/40"
        )}
      />
      {LABELS[mode]}
    </span>
  );
}
