"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutDashboard, MessagesSquare, PackageSearch } from "lucide-react";
import { cn } from "@/lib/utils";
import { ThemeToggle } from "@/components/theme-toggle";

const NAV = [
  { href: "/", label: "Painel", icon: LayoutDashboard },
  { href: "/orders", label: "Pedidos", icon: PackageSearch },
  { href: "/analytics", label: "Perguntar", icon: MessagesSquare },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const isActive = (href: string) =>
    href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`);

  return (
    <div className="flex min-h-dvh flex-col md:flex-row">
      <header className="flex shrink-0 flex-col gap-3 border-b border-sidebar-border bg-sidebar px-4 py-3 md:w-60 md:border-r md:border-b-0 md:px-4 md:py-6">
        <div className="flex items-center justify-between gap-3 md:block">
          <Link href="/" className="flex items-baseline gap-2">
            <span className="text-sm font-semibold tracking-tight text-sidebar-foreground">
              Gestão de Pedidos
            </span>
          </Link>
          <p className="hidden text-xs text-muted-foreground md:mt-1 md:block">
            Painel de operações
          </p>

          <nav className="flex gap-1 md:hidden">
            {NAV.map((item) => (
              <NavLink key={item.href} {...item} active={isActive(item.href)} compact />
            ))}
          </nav>
        </div>

        <nav className="hidden flex-1 flex-col gap-1 md:mt-6 md:flex">
          {NAV.map((item) => (
            <NavLink key={item.href} {...item} active={isActive(item.href)} />
          ))}
        </nav>

        <div className="hidden md:block">
          <ThemeToggle />
        </div>
      </header>

      <main className="min-w-0 flex-1">{children}</main>
    </div>
  );
}

function NavLink({
  href,
  label,
  icon: Icon,
  active,
  compact = false,
}: {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  active: boolean;
  compact?: boolean;
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={cn(
        "flex items-center gap-2 rounded-md px-2.5 py-2 text-sm transition-colors",
        compact && "px-2 py-1.5 text-xs",
        active
          ? "bg-sidebar-accent font-medium text-sidebar-accent-foreground"
          : "text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground"
      )}
    >
      <Icon className={compact ? "size-3.5" : "size-4"} />
      {label}
    </Link>
  );
}
