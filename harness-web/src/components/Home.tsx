import { LoginCard } from "./LoginCard";
import { RegisterCard } from "./RegisterCard";
import { SessionCard } from "./SessionCard";

/** The account cards: register, log in, and the session with its gateway test calls. */
export function Home() {
  return (
    <div className="grid gap-6 md:grid-cols-3">
      <RegisterCard />
      <LoginCard />
      <SessionCard />
    </div>
  );
}
