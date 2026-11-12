import { type AppClient, createAppClient } from "platform-kit-web/sdk";
import "./style.css";

const root = document.getElementById("app");
if (!root) throw new Error("#app is missing");
const app: HTMLElement = root;

function element<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  text?: string,
  className?: string,
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (text !== undefined) node.textContent = text;
  if (className) node.className = className;
  return node;
}

/** A button that calls the mock API through the harness and prints the status and body. */
function callButton(client: AppClient, path: string, output: HTMLElement): HTMLButtonElement {
  const button = element("button", `GET ${path}`);
  button.addEventListener("click", async () => {
    output.textContent = "…";
    try {
      const response = await client.api.fetch(path);
      output.textContent = `${response.status}\n${await response.text()}`;
    } catch (error) {
      output.textContent = String(error);
    }
  });
  return button;
}

function homePage(client: AppClient, role: string | null): HTMLElement {
  const page = element("section");
  page.append(
    element("p", `Hello ${client.init.user.username}`),
    Object.assign(element("p", `Role (a hint, the API decides): ${role ?? "none"}`), {
      id: "role",
    }),
  );
  const output = element("pre", "Press a button to call the mock API through the harness.");
  output.id = "output";
  const buttons = element("p", undefined, "buttons");
  buttons.append(callButton(client, "whoami", output), callButton(client, "whoami/admin", output));
  page.append(buttons, output);
  return page;
}

function aboutPage(): HTMLElement {
  const page = element("section");
  page.append(
    element("p", "A throw-away app to test the harness: tabs, iframe, bridge, deep links."),
  );
  return page;
}

async function main() {
  const client = await createAppClient();
  let role = client.init.role;

  const setTheme = (theme: unknown) => {
    document.documentElement.dataset.theme = theme === "dark" ? "dark" : "light";
  };
  setTheme(client.init.theme);
  client.on("theme", setTheme);

  const nav = element("nav");
  const home = element("a", "Home");
  home.href = "#/";
  const about = element("a", "About");
  about.href = "#/about";
  nav.append(home, about);

  const view = element("div");
  app.replaceChildren(element("h1", "Mock web"), nav, view);

  const render = () => {
    const path = location.hash.replace(/^#/, "") || "/";
    view.replaceChildren(path === "/about" ? aboutPage() : homePage(client, role));
    client.reportPath(path);
  };

  client.on("role-changed", (payload) => {
    const change = payload as { slug?: string; role?: string | null } | undefined;
    if (change?.slug !== client.init.slug) return;
    role = change.role ?? null;
    const label = document.getElementById("role");
    if (label) label.textContent = `Role (a hint, the API decides): ${role ?? "none"}`;
  });
  client.on("logout", () => app.replaceChildren(element("p", "Logged out.")));

  window.addEventListener("hashchange", render);
  render();
}

main().catch((error: unknown) => {
  app.replaceChildren(element("p", `Open this page from the harness. (${String(error)})`));
});
