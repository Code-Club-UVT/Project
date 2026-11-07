// Runs before the app so the page never flashes the wrong theme. An external file, so the CSP can forbid inline scripts.
(() => {
  let theme = "system";
  try {
    theme = localStorage.getItem("harness.theme") || "system";
  } catch {}
  const dark =
    theme === "dark" || (theme === "system" && matchMedia("(prefers-color-scheme: dark)").matches);
  document.documentElement.classList.toggle("dark", dark);
})();
