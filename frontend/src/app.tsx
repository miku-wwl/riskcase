import { Suspense, lazy, useEffect } from "react";
import { QueryClient, QueryClientProvider, useQuery, useQueryClient, useMutation } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AlertCircle, LoaderCircle, RefreshCw } from "lucide-react";
import { Toaster } from "sonner";
import { AppShell } from "@/components/app-shell";
import { Button } from "@/components/ui/button";
import { LoginPage } from "@/pages/login-page";
import { QueuePage } from "@/pages/queue-page";
import { errorMessage } from "@/lib/api-error";
import { apiCall, clients, refreshAntiForgeryToken, signOut, unauthorized, setActiveUser } from "@/lib/api";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 15_000, refetchOnWindowFocus: false, retry: 1 },
    mutations: { retry: false },
  },
});
const CaseDetailPage = lazy(() => import("@/pages/case-detail-page").then((module) => ({ default: module.CaseDetailPage })));

function SignedInApp() {
  const client = useQueryClient();
  useEffect(() => {
    const listener = (event: StorageEvent) => { if (event.key === "riskcase:session-change") { setActiveUser(null); client.clear(); window.location.reload(); } };
    window.addEventListener("storage", listener);
    return () => window.removeEventListener("storage", listener);
  }, [client]);
  const csrf = useQuery({ queryKey: ["csrf"], queryFn: refreshAntiForgeryToken, retry: false, staleTime: Infinity });
  const session = useQuery({
    queryKey: ["session"],
    queryFn: async () => {
      try {
        return await apiCall(() => clients.session.getSession());
      } catch (error) {
        if (unauthorized(error)) return null;
        throw error;
      }
    },
    retry: false,
  });
  const logout = useMutation({
    mutationFn: async () => { await client.cancelQueries(); await signOut(); },
    onSuccess: () => {
      // Keep the observed session query attached during the login/logout handoff.
      // Removing it with clear() can leave the mounted observer watching a discarded query.
      client.setQueryData(["session"], null);
      client.removeQueries({ predicate: (query) => !["session", "csrf"].includes(String(query.queryKey[0])) });
      client.getMutationCache().clear();
    },
  });

  setActiveUser(session.data?.id ?? null);

  if (session.isPending) return <LoadingScreen label="正在连接风控服务" />;
  if (session.isError) {
    return <ConnectionError message={errorMessage(session.error)} onRetry={() => void session.refetch()} />;
  }
  if (!session.data) return <LoginPage onSignedIn={(value) => client.setQueryData(["session"], value)} />;
  return <Routes>
    <Route element={<AppShell session={session.data} csrfReady={!csrf.isPending && !csrf.isError} signingOut={logout.isPending} onSignOut={() => logout.mutate()} />}>
      <Route index element={<QueuePage mode="all" />} />
      <Route path="assigned" element={<QueuePage mode="assigned" />} />
      <Route path="review" element={<QueuePage mode="review" />} />
      <Route path="resolved" element={<QueuePage mode="resolved" />} />
      <Route path="cases/:caseId" element={<Suspense fallback={<LoadingScreen label="正在打开调查工作区" />}><CaseDetailPage /></Suspense>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Route>
  </Routes>;
}

export function RiskCaseApp() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <SignedInApp />
      </BrowserRouter>
      <Toaster position="top-right" richColors closeButton />
    </QueryClientProvider>
  );
}

function LoadingScreen({ label }: { label: string }) {
  return <main className="grid min-h-screen place-items-center bg-background"><div className="flex items-center gap-3 text-sm text-muted-foreground"><LoaderCircle className="size-5 animate-spin text-primary" />{label}</div></main>;
}

function ConnectionError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <main className="grid min-h-screen place-items-center bg-background p-6">
      <div className="max-w-md rounded-xl border bg-card p-8 text-center shadow-sm">
        <AlertCircle className="mx-auto mb-3 size-9 text-amber-600" />
        <h1 className="text-lg font-semibold">暂时无法连接 RiskCase</h1>
        <p className="mt-2 text-sm leading-6 text-muted-foreground">{message}</p>
        <Button className="mt-5" onClick={onRetry}><RefreshCw className="size-4" />重新连接</Button>
      </div>
    </main>
  );
}
