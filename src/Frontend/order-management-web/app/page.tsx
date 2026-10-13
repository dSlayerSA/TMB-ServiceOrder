import { fetchDashboard } from "@/lib/api";
import { DashboardView } from "@/components/dashboard/DashboardView";

export default async function DashboardPage() {
  // Rendered server-side so the first paint already carries the numbers; the
  // client takes over from there and keeps them live.
  const snapshot = await fetchDashboard().catch(() => null);

  return (
    <div className="mx-auto w-full max-w-6xl px-5 py-6 sm:px-8 sm:py-8">
      <DashboardView initial={snapshot} />
    </div>
  );
}
