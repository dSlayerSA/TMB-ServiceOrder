"use client";

import { useEffect, useState } from "react";

/**
 * A ticking clock shared by every progress bar on screen: one interval drives
 * all of them, instead of each card scheduling its own.
 */
export function useNow(intervalMs = 100, enabled = true) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!enabled) return;
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs, enabled]);

  return now;
}
