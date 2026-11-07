import { Header } from "./components/Header";
import { LoginCard } from "./components/LoginCard";
import { RegisterCard } from "./components/RegisterCard";
import { SessionCard } from "./components/SessionCard";

export function App() {
  return (
    <main className="mx-auto max-w-5xl space-y-6 px-4 py-8">
      <Header />
      <div className="grid gap-6 md:grid-cols-3">
        <RegisterCard />
        <LoginCard />
        <SessionCard />
      </div>
    </main>
  );
}
