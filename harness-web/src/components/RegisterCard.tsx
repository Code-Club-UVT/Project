import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { useRegister } from "../auth/useAuth";
import { Button, Card, ErrorNote, Field } from "./ui";

export function RegisterCard() {
  const { t } = useTranslation();
  const register = useRegister();
  const [form, setForm] = useState({
    username: "",
    email: "",
    password: "",
    firstName: "",
    lastName: "",
  });

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((current) => ({ ...current, [key]: e.target.value }));

  const submit = (e: FormEvent) => {
    e.preventDefault();
    register.mutate({
      username: form.username,
      email: form.email,
      password: form.password,
      firstName: form.firstName || undefined,
      lastName: form.lastName || undefined,
    });
  };

  return (
    <Card title={t("register.title")}>
      <form onSubmit={submit} className="space-y-3">
        <Field
          label={t("register.username")}
          value={form.username}
          onChange={set("username")}
          required
          autoComplete="username"
        />
        <Field
          label={t("register.email")}
          type="email"
          value={form.email}
          onChange={set("email")}
          required
          autoComplete="email"
        />
        <Field
          label={t("register.password")}
          type="password"
          value={form.password}
          onChange={set("password")}
          required
          autoComplete="new-password"
        />
        <Field label={t("register.firstName")} value={form.firstName} onChange={set("firstName")} />
        <Field label={t("register.lastName")} value={form.lastName} onChange={set("lastName")} />
        <Button type="submit" disabled={register.isPending}>
          {register.isPending ? t("common.working") : t("register.submit")}
        </Button>
      </form>
      {register.isSuccess && (
        <p className="mt-3 text-sm text-green-700 dark:text-green-400">{t("register.success")}</p>
      )}
      <ErrorNote error={register.error} />
    </Card>
  );
}
