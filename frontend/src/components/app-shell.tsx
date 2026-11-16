import { useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { Activity, Archive, ClipboardCheck, FileClock, LogOut, Menu, ShieldCheck, Siren, X } from "lucide-react";
import type { SessionView } from "@/generated/riskcase-api";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";

const navigation = [
  { to: "/", label: "风险告警队列", icon: Siren, end: true },
  { to: "/assigned", label: "分配给我", icon: ClipboardCheck },
  { to: "/review", label: "等待复核", icon: FileClock },
  { to: "/resolved", label: "已结案记录", icon: Archive },
];

export function AppShell({ session, csrfReady, signingOut, onSignOut }: {
  session: SessionView;
  csrfReady: boolean;
  signingOut: boolean;
  onSignOut: () => void;
}) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const sectionTitle = location.pathname.startsWith("/cases/") ? "案件调查" : navigation.find((item) => item.to === location.pathname)?.label ?? "风险告警队列";
  return (
    <div className="min-h-screen bg-background text-foreground">
      <aside className={cn("fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-slate-800 bg-sidebar text-sidebar-foreground transition-transform md:translate-x-0", mobileOpen ? "translate-x-0" : "-translate-x-full")}>
        <div className="flex h-16 items-center gap-3 border-b border-slate-800 px-5">
          <div className="grid size-9 place-items-center rounded-lg bg-teal-400 text-slate-950"><Activity className="size-5" /></div>
          <div><div className="text-sm font-bold tracking-wide">RiskCase</div><div className="text-[11px] text-slate-400">FINANCIAL RISK DESK</div></div>
          <Button className="ml-auto text-slate-300 hover:bg-slate-800 md:hidden" variant="ghost" size="icon" onClick={() => setMobileOpen(false)} aria-label="关闭导航"><X /></Button>
        </div>
        <div className="px-4 pb-2 pt-6 text-[10px] font-semibold uppercase tracking-[0.18em] text-slate-500">工作区</div>
        <nav className="space-y-1 px-3" aria-label="主导航">
          {navigation.map(({ to, label, icon: Icon, end }) => (
            <NavLink key={to} to={to} end={end} onClick={() => setMobileOpen(false)} className={({ isActive }) => cn(
              "flex h-10 items-center gap-3 rounded-md px-3 text-sm transition-colors",
              isActive ? "bg-slate-800 font-semibold text-white" : "text-slate-300 hover:bg-slate-800/70 hover:text-white",
            )}>
              <Icon className="size-4" />{label}
            </NavLink>
          ))}
        </nav>
        <div className="mt-auto p-4">
          <div className="rounded-lg border border-slate-800 bg-slate-900/50 p-3">
            <div className="flex items-center gap-2 text-xs font-semibold"><ShieldCheck className="size-4 text-teal-300" />仅供内部调查</div>
            <p className="mt-1.5 text-[11px] leading-4 text-slate-400">演示记录为合成数据。告警信号提示核查方向，不构成事实认定。</p>
          </div>
        </div>
      </aside>
      {mobileOpen && <button className="fixed inset-0 z-30 bg-slate-950/40 md:hidden" onClick={() => setMobileOpen(false)} aria-label="关闭导航" />}

      <div className="md:pl-64">
        <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b bg-card/95 px-4 backdrop-blur md:px-8">
          <Button className="md:hidden" variant="ghost" size="icon" onClick={() => setMobileOpen(true)} aria-label="打开导航"><Menu /></Button>
          <div className="min-w-0 flex-1">
            <div className="text-[11px] text-muted-foreground">RiskCase / 风险管理</div>
            <div className="truncate text-sm font-semibold">{sectionTitle}</div>
          </div>
          {!csrfReady && <span className="hidden text-xs text-amber-700 sm:inline">正在准备安全会话…</span>}
          <Separator orientation="vertical" className="hidden h-7 sm:block" />
          <div className="hidden text-right sm:block">
            <div className="max-w-52 truncate text-sm font-medium">{session.email}</div>
            <div className="text-[11px] text-muted-foreground">{session.roles.map(roleLabel).join(" · ")}</div>
          </div>
          <Button variant="ghost" size="sm" disabled={signingOut} onClick={onSignOut} aria-label="退出登录"><LogOut className="size-4" /><span className="hidden lg:inline">退出</span></Button>
        </header>
        <main className="mx-auto max-w-[1500px] px-4 py-6 md:px-8 md:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function roleLabel(role: string) {
  return ({ RiskAnalyst: "风控分析员", SeniorAnalyst: "高级分析员", RiskSupervisor: "风控主管", RiskAuditor: "审计只读" } as Record<string, string>)[role] ?? role;
}
