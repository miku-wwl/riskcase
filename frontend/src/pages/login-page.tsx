import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Activity, ArrowRight, Eye, EyeOff, LockKeyhole, ShieldCheck } from "lucide-react";
import type { SessionView } from "@/generated/riskcase-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError, errorMessage } from "@/lib/api-error";
import { apiCall, clients, signIn } from "@/lib/api";

export function LoginPage({ onSignedIn }: { onSignedIn: (session: SessionView) => void }) {
  const [email, setEmail] = useState("analyst@riskcase.local");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [credentialHint, setCredentialHint] = useState("");
  const login = useMutation({
    mutationFn: async () => {
      await signIn(email, password);
      return apiCall(() => clients.session.getSession());
    },
    onSuccess: onSignedIn,
  });

  const demoAccounts = import.meta.env.DEV ? [
    { label: "分析员", email: "analyst@riskcase.local", password: "RiskAnalyst!2026" },
    { label: "主管", email: "supervisor@riskcase.local", password: "RiskSupervisor!2026" },
    { label: "审计只读", email: "auditor@riskcase.local", password: "RiskAuditor!2026" },
  ] : [];

  return (
    <main className="grid min-h-screen bg-[radial-gradient(ellipse_at_top_left,_#dff7f2,_transparent_44%),linear-gradient(135deg,#f8fafc_0%,#edf2f7_100%)] lg:grid-cols-[minmax(0,1.1fr)_minmax(430px,0.9fr)]">
      <section className="hidden flex-col justify-between p-12 lg:flex xl:p-20">
        <div className="flex items-center gap-3"><div className="grid size-11 place-items-center rounded-xl bg-slate-900 text-teal-300"><Activity /></div><div><div className="text-lg font-bold tracking-wide">RiskCase</div><div className="text-xs text-slate-500">金融风险调查工作台</div></div></div>
        <div className="max-w-xl">
          <div className="mb-5 inline-flex items-center gap-2 rounded-full border border-teal-200 bg-white/80 px-3 py-1.5 text-xs font-semibold text-teal-800"><ShieldCheck className="size-3.5" />决策留痕 · 信号可解释 · 独立复核</div>
          <h1 className="text-4xl font-semibold leading-tight tracking-tight text-slate-900 xl:text-5xl">把风险线索，<br /><span className="text-teal-700">交给有依据的调查。</span></h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-slate-600">整理告警、客户账户与历史交易关系，记录调查依据，并由独立复核人员完成处置。</p>
          <div className="mt-10 grid grid-cols-3 gap-3">
            {["解释每个信号", "记录每次决定", "保护并发修改"].map((item, i) => <div key={item} className="rounded-lg border border-white bg-white/70 p-4 shadow-sm"><div className="text-xs text-slate-400">0{i + 1}</div><div className="mt-2 text-sm font-semibold text-slate-700">{item}</div></div>)}
          </div>
        </div>
        <p className="text-xs text-slate-500">RiskCase 使用合成演示数据 · 不连接真实银行系统</p>
      </section>
      <section className="flex items-center justify-center p-5 sm:p-8">
        <div className="w-full max-w-md rounded-2xl border bg-card p-7 shadow-xl shadow-slate-900/5 sm:p-9">
          <div className="mb-8 flex items-center gap-3 lg:hidden"><div className="grid size-10 place-items-center rounded-lg bg-slate-900 text-teal-300"><Activity /></div><div className="text-lg font-bold">RiskCase</div></div>
          <div className="mb-7">
            <div className="text-xs font-semibold uppercase tracking-[0.16em] text-teal-700">SECURE WORKSPACE</div>
            <h2 className="mt-2 text-2xl font-semibold tracking-tight">登录风控工作台</h2>
            <p className="mt-2 text-sm text-muted-foreground">使用已分配权限的内部账户继续。</p>
          </div>
          <form className="space-y-5" onSubmit={(event) => { event.preventDefault(); setCredentialHint(""); login.mutate(); }}>
            <div className="space-y-2"><Label htmlFor="email">工作邮箱</Label><Input id="email" type="email" autoComplete="username" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
            <div className="space-y-2"><Label htmlFor="password">密码</Label><div className="relative"><Input id="password" type={showPassword ? "text" : "password"} className="pr-11" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required /><button className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground" type="button" onClick={() => setShowPassword((value) => !value)} aria-label={showPassword ? "隐藏密码" : "显示密码"}>{showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}</button></div></div>
            {(login.isError || credentialHint) && <p className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800" role="alert">{credentialHint || (login.error instanceof ApiError && login.error.status === 401 ? "邮箱或密码不正确。" : errorMessage(login.error))}</p>}
            <Button className="h-11 w-full" disabled={login.isPending || !email || !password} type="submit">
              {login.isPending ? <><LockKeyhole className="size-4 animate-pulse" />正在验证</> : <>安全登录<ArrowRight className="size-4" /></>}
            </Button>
          </form>
          {demoAccounts.length > 0 && <div className="mt-7 border-t pt-5"><div className="mb-2 text-xs font-semibold text-muted-foreground">本地演示账户</div><div className="grid grid-cols-3 gap-2">{demoAccounts.map((account) => <Button key={account.email} size="sm" variant="outline" onClick={() => { setEmail(account.email); setPassword(account.password); setCredentialHint(""); }}>{account.label}</Button>)}</div><p className="mt-2 text-[11px] text-muted-foreground">演示账户仅在 Development 环境创建。</p></div>}
          <div className="mt-7 flex items-center justify-center gap-2 text-[11px] text-muted-foreground"><LockKeyhole className="size-3.5" />会话使用安全 Cookie 与防跨站请求令牌</div>
        </div>
      </section>
    </main>
  );
}
