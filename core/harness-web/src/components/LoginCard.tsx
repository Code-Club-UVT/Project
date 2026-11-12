import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { useLogin } from "../auth/useAuth";
import { Button, Card, ErrorNote, Field } from "./ui";

export function LoginCard() {
  const { t } = useTranslation();
  const login = useLogin();
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");

  const submit = (e: FormEvent) => {
    e.preventDefault();
    login.mutate({ identifier, password });
  };

  return (
    <Card title={t("login.title")}>
      <form onSubmit={submit} className="space-y-3">
        <Field
          label={t("login.identifier")}
          value={identifier}
          onChange={(e) => setIdentifier(e.target.value)}
          required
          autoComplete="username"
        />
        <Field
          label={t("login.password")}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          autoComplete="current-password"
        />
        <Button type="submit" disabled={login.isPending}>
          {login.isPending ? t("common.working") : t("login.submit")}
        </Button>
      </form>
      <ErrorNote error={login.error} />
    </Card>
  );
}
