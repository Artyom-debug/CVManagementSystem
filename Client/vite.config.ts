import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, ".", "");
  return {
    plugins: [react()],
    server: {
      proxy: {
        "/api": {
          target: env.API_TARGET || "https://localhost:7240",
          changeOrigin: true,
          secure: false,
        },
      },
    },
  };
});
