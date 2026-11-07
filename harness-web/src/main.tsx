import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { AuthBootstrap } from "./auth/AuthBootstrap";
import "./i18n";
import "./index.css";
import { ThemeProvider } from "./theme/ThemeProvider";

// Retries are the axios interceptor's job (it knows which requests are safe to repeat), so react-query does not retry.
const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
});

const root = document.getElementById("root");
if (!root) throw new Error("#root is missing");

createRoot(root).render(
  <StrictMode>
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthBootstrap>
          <App />
        </AuthBootstrap>
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
);
