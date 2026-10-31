import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

// The API runs on its launch profile's port; set HOSPITALITYOS_API_URL (e.g. in .env.local) to call another.
export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    port: 5175,
    strictPort: true,
    proxy: { "/api": loadEnv(mode, ".", "HOSPITALITYOS_").HOSPITALITYOS_API_URL ?? "http://localhost:5080" },
  },
}));
