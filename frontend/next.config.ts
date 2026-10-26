import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  // Standalone output: the Docker image only needs server.js + static assets.
  output: "standalone",
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
