import { useTranslation } from "react-i18next";
import { useLogout, useWhoami } from "../auth/useAuth";
import { useSession } from "../auth/useSession";
import { config } from "../config";
import { Button, Card, ErrorNote, JsonBox } from "./ui";

function WhoamiButton({ kind, label }: { kind: "profile" | "projects" | "mock"; label: string }) {
  const { t } = useTranslation();
  const call = useWhoami(kind);
  return (
    <div>
      <Button variant="secondary" onClick={() => call.mutate()} disabled={call.isPending}>
        {call.isPending ? t("common.working") : label}
      </Button>
      {call.isSuccess && <JsonBox data={call.data} />}
      <ErrorNote error={call.error} />
    </div>
  );
}

export function SessionCard() {
  const { t, i18n } = useTranslation();
  const { status, session } = useSession();
  const logout = useLogout();

  if (status === "loading") {
    return (
      <Card title={t("session.title")}>
        <p className="text-sm">{t("common.loading")}</p>
      </Card>
    );
  }

  if (!session) {
    return (
      <Card title={t("session.title")}>
        <p className="text-sm text-slate-600 dark:text-slate-400">{t("session.anonymous")}</p>
      </Card>
    );
  }

  return (
    <Card title={t("session.title")}>
      <p className="text-sm">
        {t("session.loggedInAs", { username: session.user.username, email: session.user.email })}
      </p>
      <p className="mt-1 text-xs text-slate-600 dark:text-slate-400">
        {t("session.expiresAt", {
          time: new Date(session.expiresAt).toLocaleTimeString(i18n.language),
        })}
      </p>
      <div className="mt-4 space-y-3">
        <p className="text-xs text-slate-600 dark:text-slate-400">{t("session.gatewayHint")}</p>
        <WhoamiButton kind="profile" label={t("session.profile")} />
        <WhoamiButton kind="projects" label={t("session.projects")} />
        <WhoamiButton kind="mock" label={t("session.mock", { slug: config.mockSlug })} />
        <div>
          <Button onClick={() => logout.mutate()} disabled={logout.isPending}>
            {t("session.logout")}
          </Button>
        </div>
      </div>
    </Card>
  );
}
