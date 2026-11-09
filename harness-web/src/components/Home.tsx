import type { MyProject } from "../api/projects";
import { AppMenu } from "../apps/AppMenu";
import { LoginCard } from "./LoginCard";
import { RegisterCard } from "./RegisterCard";
import { SessionCard } from "./SessionCard";

/** The menu of apps, then the account cards: register, log in, and the session with its gateway test calls. */
export function Home({ apps }: { apps: MyProject[] }) {
  return (
    <div className="space-y-8">
      <AppMenu apps={apps} />
      <div className="grid gap-6 md:grid-cols-3">
        <RegisterCard />
        <LoginCard />
        <SessionCard />
      </div>
    </div>
  );
}
