import type { Metadata } from "next";
import { IBM_Plex_Mono, IBM_Plex_Sans, IBM_Plex_Sans_Condensed } from "next/font/google";
import "./globals.css";
import { ThemeProvider } from "@/components/theme-provider";
import { AppShell } from "@/components/app-shell";

const plexSans = IBM_Plex_Sans({
  variable: "--font-plex-sans",
  subsets: ["latin"],
  weight: ["400", "500", "600"],
});

// Structural text - column headers, field labels, the rail - takes the
// condensed width. It reads as the labelling on an instrument rather than as
// body copy set small, and it buys back horizontal room in a dense table.
const plexCondensed = IBM_Plex_Sans_Condensed({
  variable: "--font-plex-condensed",
  subsets: ["latin"],
  weight: ["400", "500", "600"],
});

// Carries every figure in the app: amounts, timestamps and ids line up when
// they share a fixed advance width, which is what makes a changing table scannable.
const plexMono = IBM_Plex_Mono({
  variable: "--font-plex-mono",
  subsets: ["latin"],
  weight: ["400", "500"],
});

export const metadata: Metadata = {
  title: "Gestão de Pedidos",
  description: "Painel de operações para pedidos com processamento assíncrono",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="pt-BR"
      suppressHydrationWarning
      className={`${plexSans.variable} ${plexCondensed.variable} ${plexMono.variable} h-full antialiased`}
    >
      <body className="min-h-full bg-background">
        <ThemeProvider>
          <AppShell>{children}</AppShell>
        </ThemeProvider>
      </body>
    </html>
  );
}
