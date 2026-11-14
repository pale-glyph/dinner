import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // Next 16.4 cannot parse TypeScript 5.9's showConfig output in this project.
  // `npm run build` runs tsc explicitly before asking Next to bundle.
  typescript: { ignoreBuildErrors: true }
};

export default nextConfig;
