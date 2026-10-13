// Liveness probe for the container orchestrator: proves the Next.js server
// process is up and able to serve requests. It deliberately does not probe the
// Api - that dependency is already expressed by `depends_on` in the compose
// file, and folding it in here would make the frontend report unhealthy (and
// get restarted) for a failure it cannot fix.
export const dynamic = "force-dynamic";

export function GET() {
  return Response.json({ status: "ok" });
}
