"use client";

import Image from "next/image";
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
      <header className="sticky top-0 z-30 flex shrink-0 items-center gap-4 border-b border-sidebar-border bg-sidebar px-4 py-2.5 md:static md:w-56 md:flex-col md:items-stretch md:gap-0 md:border-r md:border-b-0 md:px-0 md:py-0">
        <Link
          href="/"
          className="flex items-center gap-3 outline-none focus-visible:ring-2 focus-visible:ring-ring md:h-auto md:shrink-0 md:flex-col md:items-start md:gap-1.5 md:border-b md:border-sidebar-border md:px-4 md:py-4"
        >
          {/* The mark ships as pure white on transparent, so light mode inverts
            * it to pure black rather than shipping a second file. */}
          <Image
            src="/tmb.webp"
            alt="TMB"
            width={105}
            height={48}
            priority
            className="h-[22px] w-auto invert dark:invert-0"
          />
          <span className="font-condensed text-[0.9rem] leading-none font-semibold tracking-tight text-sidebar-foreground">
            Gestão de Pedidos
          </span>
        </Link>

        <nav className="ml-auto flex gap-0.5 md:ml-0 md:flex-1 md:flex-col md:gap-0 md:py-2">
          {NAV.map((item) => (
            <NavLink key={item.href} {...item} active={isActive(item.href)} />
          ))}
        </nav>

        <div className="flex items-center justify-between gap-2 md:shrink-0 md:border-t md:border-sidebar-border md:px-3 md:py-3">
          <span className="label hidden md:block">Operações</span>
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
}: {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  active: boolean;
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={cn(
        // The active item is marked by a rule in the margin, not by a filled
        // pill: nothing in the chrome is allowed to carry a colour.
        "relative flex items-center gap-2 px-2.5 py-2 text-sm transition-colors outline-none",
        "before:absolute before:inset-y-1 before:left-0 before:w-[2px] before:bg-foreground before:transition-opacity",
        "md:px-4 md:py-2 focus-visible:bg-sidebar-accent",
        active
          ? "font-medium text-sidebar-foreground before:opacity-100"
          : "text-muted-foreground before:opacity-0 hover:bg-sidebar-accent/60 hover:text-sidebar-foreground"
      )}
    >
      <Icon className="size-4" />
      {label}
    </Link>
  );
}

/**
 * Every page opens the same way: title, one line of orientation, and the
 * controls that act on the whole page. Kept here so the three pages cannot
 * drift apart.
 */
export function PageHeader({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children?: React.ReactNode;
}) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3 border-b pb-5">
      <div className="min-w-0 space-y-1">
        <h1 className="font-condensed text-2xl leading-none font-semibold tracking-tight">{title}</h1>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {children && <div className="flex shrink-0 items-center gap-2.5">{children}</div>}
    </header>
  );
}
