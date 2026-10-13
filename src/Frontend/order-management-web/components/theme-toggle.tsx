"use client";

import { useEffect, useState } from "react";
import { useTheme } from "next-themes";
import { Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  // The server has no way to know the resolved theme, so the icon is only
  // decided on the client. Rendering a fixed placeholder avoids a flash of the
  // wrong icon on hydration.
  useEffect(() => setMounted(true), []);

  const isDark = resolvedTheme === "dark";

  return (
    <Button
      variant="ghost"
      size="sm"
      className="w-full justify-start gap-2 px-2 text-muted-foreground hover:text-foreground"
      onClick={() => setTheme(isDark ? "light" : "dark")}
      aria-label={isDark ? "Usar tema claro" : "Usar tema escuro"}
    >
      {mounted && isDark ? <Sun className="size-4" /> : <Moon className="size-4" />}
      <span>{mounted ? (isDark ? "Tema claro" : "Tema escuro") : "Tema"}</span>
    </Button>
  );
}
