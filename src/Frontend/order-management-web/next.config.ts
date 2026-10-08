import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* Orders are always fetched fresh from the API; this app has no cached
   * routes, so Cache Components (which requires Suspense boundaries around
   * every dynamic fetch) isn't a fit here. */
  output: "standalone",
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
