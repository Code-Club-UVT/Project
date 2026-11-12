import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

// Assets are referenced relatively and index.html carries a <base href> that the container fills in at start,
// so one build works under /dev/, /preprod/ and /.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, ".", "");
  const target = env.VITE_PROXY_TARGET || "http://localhost:8080";
  return {
    base: "./",
    plugins: [react(), tailwindcss()],
    server: {
      port: 5173,
      proxy: {
        // Same origin for the browser, so the SameSite=Strict refresh cookie works. The cookie path is rewritten for the
        // dev server, whose cookie path contains the environment prefix (/dev/api/identity/auth).
        "/api": {
          target,
          changeOrigin: true,
          cookiePathRewrite: { "*": "/" },
          cookieDomainRewrite: "",
        },
        // The projects' frontends are served by the gateway through the same door.
        "/apps": { target, changeOrigin: true },
      },
    },
  };
});
