"use client";

import { useEffect, useRef, useState } from "react";
import { createOrdersHubConnection } from "@/lib/signalr";

export type RealtimeMode = "connecting" | "realtime" | "polling";

interface OrderStatusChangedPayload {
  orderId: string;
  status: string;
}

/**
 * Subscribes to SignalR's "OrderStatusChanged" event. If the connection can't
 * be established (or drops), it falls back to polling via `onUpdate` on an
 * interval, so the UI keeps refreshing even without a live socket.
 *
 * @param orderId   When provided, only events for this order trigger `onUpdate`.
 *                  When omitted, every status change triggers it (list view).
 * @param onUpdate  Called whenever a relevant update should be fetched.
 * @param pollIntervalMs Fallback polling interval while not connected live.
 */
export function useOrderRealtime(
  orderId: string | undefined,
  onUpdate: () => void,
  pollIntervalMs = 3000
): RealtimeMode {
  const [mode, setMode] = useState<RealtimeMode>("connecting");
  const onUpdateRef = useRef(onUpdate);
  onUpdateRef.current = onUpdate;

  useEffect(() => {
    const connection = createOrdersHubConnection();
    let pollTimer: ReturnType<typeof setInterval> | null = null;
    let stopped = false;

    const startPolling = () => {
      if (pollTimer) return;
      setMode("polling");
      pollTimer = setInterval(() => onUpdateRef.current(), pollIntervalMs);
    };

    const stopPolling = () => {
      if (pollTimer) {
        clearInterval(pollTimer);
        pollTimer = null;
      }
    };

    connection.on("OrderStatusChanged", (payload: OrderStatusChangedPayload) => {
      if (!orderId || payload.orderId === orderId) {
        onUpdateRef.current();
      }
    });

    connection.onreconnecting(() => startPolling());
    connection.onreconnected(() => {
      stopPolling();
      setMode("realtime");
    });
    connection.onclose(() => {
      if (!stopped) startPolling();
    });

    connection
      .start()
      .then(() => {
        if (stopped) return;
        stopPolling();
        setMode("realtime");
      })
      .catch(() => {
        if (!stopped) startPolling();
      });

    return () => {
      stopped = true;
      stopPolling();
      connection.stop().catch(() => {
        /* ignore errors during teardown */
      });
    };
  }, [orderId, pollIntervalMs]);

  return mode;
}
