#!/usr/bin/env node
// Creates local-deploy/.env (gitignored) from .env.example with random secrets, and sets up the SMTP account the
// emails are really sent through. No dependencies: only Node's own modules.
//
//   node init.mjs            first time: creates .env (asks for the SMTP username and password)
//   node init.mjs --smtp     later: changes only the SMTP settings in the existing .env
//   node init.mjs --force    replaces .env (new secrets: also delete the database volume, see README.md)
//
// Settings can also come from the environment, so nothing is asked:
//   SMTP_USERNAME, SMTP_PASSWORD, SMTP_HOST, SMTP_PORT, SMTP_FROM, ADMIN_EMAIL, LOCAL_HTTP_PORT, LOCAL_PG_PORT
import { randomBytes } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { createInterface } from "node:readline";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const envPath = join(here, ".env");
const examplePath = join(here, ".env.example");
const args = new Set(process.argv.slice(2));

const rand = () => randomBytes(24).toString("hex");

function ask(question, { secret = false, fallback = "" } = {}) {
  if (!process.stdin.isTTY) return Promise.resolve(fallback);
  return new Promise((resolve) => {
    const rl = createInterface({ input: process.stdin, output: process.stdout, terminal: true });
    if (secret) {
      // Show the question but not what is typed.
      rl._writeToOutput = (text) => {
        if (text.includes(question)) process.stdout.write(text);
      };
    }
    rl.question(question, (answer) => {
      rl.close();
      if (secret) process.stdout.write("\n");
      resolve(answer.trim() || fallback);
    });
  });
}

async function smtpSettings() {
  const username =
    process.env.SMTP_USERNAME ||
    (await ask("SMTP username [codeclub@e-uvt.ro]: ", { fallback: "codeclub@e-uvt.ro" }));
  const password =
    process.env.SMTP_PASSWORD ||
    (await ask("SMTP password (app password; empty = do not send, only log): ", { secret: true }));
  const enabled = password !== "";
  return {
    SMTP_ENABLED: String(enabled),
    SMTP_HOST: process.env.SMTP_HOST || "smtp.gmail.com",
    SMTP_PORT: process.env.SMTP_PORT || "587",
    SMTP_STARTTLS: "true",
    SMTP_USERNAME: username,
    SMTP_PASSWORD: enabled ? password : "CHANGE-ME-smtp-password",
    SMTP_FROM: process.env.SMTP_FROM || username,
  };
}

/** Replaces KEY=value lines in a .env text; keys that are missing are appended. */
function applyValues(text, values) {
  const lines = text.split(/\r?\n/);
  const seen = new Set();
  const out = lines.map((line) => {
    const match = /^([A-Z0-9_]+)=/.exec(line);
    if (match && match[1] in values) {
      seen.add(match[1]);
      return `${match[1]}=${values[match[1]]}`;
    }
    return line;
  });
  for (const [key, value] of Object.entries(values)) if (!seen.has(key)) out.push(`${key}=${value}`);
  return out.join("\n");
}

if (args.has("--smtp")) {
  if (!existsSync(envPath)) {
    console.error("There is no .env yet: run `node init.mjs` first.");
    process.exit(1);
  }
  const smtp = await smtpSettings();
  writeFileSync(envPath, applyValues(readFileSync(envPath, "utf8"), smtp));
  console.log(
    smtp.SMTP_ENABLED === "true"
      ? "SMTP updated: emails will be sent. Apply it with: docker compose up -d email-api"
      : "SMTP disabled: emails are only written to the email-api log.",
  );
  process.exit(0);
}

if (existsSync(envPath) && !args.has("--force")) {
  console.log(".env already exists: left alone (use --smtp to change the SMTP settings, --force to replace it).");
  process.exit(0);
}

const smtp = await smtpSettings();
const values = {
  LOCAL_HTTP_PORT: process.env.LOCAL_HTTP_PORT || "8080",
  LOCAL_PG_PORT: process.env.LOCAL_PG_PORT || "5432",
  PG_ADMIN_PASSWORD: rand(),
  PG_IDENTITY_PASSWORD: rand(),
  PG_CORE_PASSWORD: rand(),
  PG_EMAIL_PASSWORD: rand(),
  PG_GATEWAY_PASSWORD: rand(),
  KEY_GATEWAY_IDENTITY: rand() + rand(),
  KEY_IDENTITY_CORE: rand() + rand(),
  KEY_IDENTITY_EMAIL: rand() + rand(),
  ADMIN_USERNAME: "admin",
  ADMIN_EMAIL: process.env.ADMIN_EMAIL || "admin@example.com",
  ADMIN_PASSWORD: rand(),
  ...smtp,
};

writeFileSync(envPath, applyValues(readFileSync(examplePath, "utf8"), values));
console.log("Created .env with random secrets.");
console.log(`The admin login (username ${values.ADMIN_USERNAME}) and its password are in .env (ADMIN_PASSWORD).`);
if (smtp.SMTP_ENABLED !== "true") {
  console.log("SMTP is off: emails are only logged. Run `node init.mjs --smtp` to send real email.");
}
console.log("Next: docker compose up -d --build");
