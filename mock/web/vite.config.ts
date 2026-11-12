import { defineConfig } from "vite";

// The harness serves the app under a path that differs per environment (/dev/apps/mock/, /apps/mock/), so every asset
// URL is relative and the routes are in the hash.
export default defineConfig({ base: "./" });
