import react from "@vitejs/plugin-react";
import { loadEnv } from "vite";
import { defineConfig } from "vitest/config";

// The API runs on its launch profile's port; set HOSPITALITYOS_API_URL (e.g. in .env.local) to call another.
const api = (mode: string) => loadEnv(mode, ".", "HOSPITALITYOS_").HOSPITALITYOS_API_URL ?? "http://localhost:5080";

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      "/api": api(mode),
      // Live updates (SignalR), over WebSockets.
      "/hubs": { target: api(mode), ws: true },
    },
  },
  test: { environment: "happy-dom" },
}));
