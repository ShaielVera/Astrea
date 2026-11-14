import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Los E2E compilan en una carpeta aparte (.next-e2e) para no pisar el build normal.
  distDir: process.env.NEXT_DIST_DIR || ".next",
  cacheComponents: true,
  partialPrefetching: true,
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
