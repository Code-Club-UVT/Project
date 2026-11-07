import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { toProblem } from "../api/problem";

export function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <h2 className="mb-4 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  );
}

export function Field({
  label,
  ...props
}: { label: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block text-slate-600 dark:text-slate-400">{label}</span>
      <input
        className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/30 dark:border-slate-700 dark:bg-slate-950"
        {...props}
      />
    </label>
  );
}

export function Button({
  variant = "primary",
  ...props
}: { variant?: "primary" | "secondary" } & ButtonHTMLAttributes<HTMLButtonElement>) {
  const style =
    variant === "primary"
      ? "bg-indigo-600 text-white hover:bg-indigo-500"
      : "border border-slate-300 hover:bg-slate-100 dark:border-slate-700 dark:hover:bg-slate-800";
  return (
    <button
      type="button"
      className={`rounded-md px-4 py-2 text-sm font-medium transition disabled:opacity-50 ${style}`}
      {...props}
    />
  );
}

/** Shows an API error translated by its backend code, falling back to the HTTP status and then to the server's text. */
export function ErrorNote({ error }: { error: unknown }) {
  const { t, i18n } = useTranslation();
  if (!error) return null;
  const problem = toProblem(error);
  const keys = [`errors.${problem.code}`, `errors.Http.${problem.status}`];
  const key = keys.find((k) => i18n.exists(k));
  return (
    <p role="alert" className="mt-3 text-sm text-red-600 dark:text-red-400">
      {key ? t(key) : (problem.detail ?? t("errors.Unknown"))}
      <span className="ml-2 font-mono text-xs opacity-60">{problem.code}</span>
    </p>
  );
}

export function JsonBox({ data }: { data: unknown }) {
  return (
    <pre className="mt-3 max-h-64 overflow-auto rounded-md bg-slate-100 p-3 text-xs dark:bg-slate-950">
      {JSON.stringify(data, null, 2)}
    </pre>
  );
}
