import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ArrowLeft, ArrowRight, CircleAlert, Clock3, Filter, Search, SlidersHorizontal } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SeverityBadge, StatusBadge, categoryLabel } from "@/components/risk-badges";
import { clients } from "@/lib/api";
import { apiCall } from "@/lib/api-error";
import { formatDate } from "@/lib/utils";
import { RiskAlertCategory, RiskCaseStatus, RiskSeverity } from "@/generated/riskcase-api";

export type QueueMode = "all" | "assigned" | "review" | "resolved";

const pageSize = 25;

const modeCopy: Record<QueueMode, { title: string; description: string }> = {
  all: { title: "风险告警队列", description: "查看上游风险监测系统提交的告警，按事实线索开展人工调查。" },
  assigned: { title: "分配给我", description: "继续处理由你负责的调查档案，或完成待复核前的补充工作。" },
  review: { title: "等待复核", description: "独立复核人员可在这里核对评估依据并作出处置决定。" },
  resolved: { title: "已结案记录", description: "查看已完成的风险处置与审计历史。结案档案保持只读。" },
};

export function QueuePage({ mode }: { mode: QueueMode }) {
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [severity, setSeverity] = useState("");
  const [page, setPage] = useState(1);
  const deferredSearch = useDeferredValue(search.trim());
  const status = mode === "review" ? RiskCaseStatus.Reviewing : mode === "resolved" ? RiskCaseStatus.Resolved : undefined;
  const queue = useQuery({
    queryKey: ["cases", mode, status, category, severity, deferredSearch, page],
    queryFn: () => apiCall(() => clients.cases.listRiskCases(
      status,
      category || undefined,
      severity || undefined,
      deferredSearch || undefined,
      mode === "assigned",
      page,
      pageSize,
    )),
    placeholderData: (previous) => previous,
  });

  const copy = modeCopy[mode];
  const items = queue.data?.items ?? [];
  const totalPages = Math.max(queue.data?.totalPages ?? 0, 1);

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 xl:flex-row xl:items-end">
        <div>
          <div className="mb-2 inline-flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.14em] text-teal-800"><span className="size-2 rounded-full bg-teal-500" />调查与复核</div>
          <h1 className="text-2xl font-semibold tracking-tight md:text-[28px]">{copy.title}</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">{copy.description}</p>
        </div>
        <div className="flex items-center gap-3 rounded-lg border bg-card px-4 py-3 text-sm shadow-sm">
          <div className="grid size-9 place-items-center rounded-md bg-teal-50 text-teal-800"><CircleAlert className="size-4" /></div>
          <div><div className="font-semibold">{queue.data?.totalItems ?? "—"} 条档案</div><div className="text-xs text-muted-foreground">当前筛选结果</div></div>
          <div className="ml-3 border-l pl-4 text-right"><div className="font-semibold">第 {page}/{totalPages} 页</div><div className="text-xs text-muted-foreground">每页 {pageSize} 条</div></div>
        </div>
      </div>

      <div className="grid gap-3 rounded-xl border bg-card p-3 shadow-sm md:grid-cols-[minmax(220px,1fr)_200px_170px_auto] md:p-4">
        <div className="relative"><Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" /><Input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} className="pl-9" placeholder="搜索客户、账户、告警或信号" aria-label="搜索案件" /></div>
        <select className="h-10 rounded-md border border-input bg-background px-3 text-sm" aria-label="告警类别" value={category} onChange={(event) => { setCategory(event.target.value); setPage(1); }}>
          <option value="">全部类别</option>
          <option value={RiskAlertCategory.TransactionRisk}>交易风险</option>
          <option value={RiskAlertCategory.Fraud}>欺诈风险</option>
          <option value={RiskAlertCategory.AmlIndicator}>反洗钱线索</option>
        </select>
        <select className="h-10 rounded-md border border-input bg-background px-3 text-sm" aria-label="告警严重度" value={severity} onChange={(event) => { setSeverity(event.target.value); setPage(1); }}>
          <option value="">全部严重度</option>
          <option value={RiskSeverity.Critical}>紧急</option>
          <option value={RiskSeverity.High}>高</option>
          <option value={RiskSeverity.Medium}>中</option>
          <option value={RiskSeverity.Low}>低</option>
        </select>
        <div className="flex items-center justify-end gap-2 text-xs text-muted-foreground"><SlidersHorizontal className="size-4" />按严重度和时间排序</div>
      </div>

      <Card className="overflow-hidden">
        <div className="flex items-center justify-between border-b px-5 py-4">
          <div><h2 className="text-sm font-semibold">案件清单</h2><p className="mt-1 text-xs text-muted-foreground">显示由监测系统提供的调查线索，不代表已确认的风险事实。</p></div>
          <div className="hidden items-center gap-1 text-xs text-muted-foreground sm:flex"><Filter className="size-3.5" />服务端筛选</div>
        </div>
        {queue.isError ? (
          <div className="p-8 text-center"><p className="font-medium">暂时无法加载案件</p><p className="mt-1 text-sm text-muted-foreground">请检查服务连接后重试。</p><Button variant="outline" className="mt-4" onClick={() => void queue.refetch()}>重新加载</Button></div>
        ) : queue.isPending ? (
          <div className="space-y-3 p-5">{Array.from({ length: 6 }, (_, index) => <div key={index} className="h-14 animate-pulse rounded-md bg-muted" />)}</div>
        ) : items.length === 0 ? (
          <div className="grid min-h-64 place-items-center p-8 text-center"><div><div className="mx-auto grid size-12 place-items-center rounded-full bg-teal-50 text-teal-800"><Filter className="size-5" /></div><p className="mt-3 font-semibold">没有符合条件的档案</p><p className="mt-1 text-sm text-muted-foreground">清除筛选条件或切换工作队列。</p></div></div>
        ) : (
          <>
            <div className="hidden overflow-x-auto md:block">
              <table className="w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs text-muted-foreground"><tr><th className="px-5 py-3 font-medium">风险告警</th><th className="px-4 py-3 font-medium">客户 / 账户</th><th className="px-4 py-3 font-medium">类别</th><th className="px-4 py-3 font-medium">严重度</th><th className="px-4 py-3 font-medium">状态 / 经办</th><th className="px-4 py-3 font-medium">检测时间</th></tr></thead>
                <tbody className="divide-y">
                  {items.map((item) => <QueueRow key={item.id} item={item} />)}
                </tbody>
              </table>
            </div>
            <div className="divide-y md:hidden">{items.map((item) => <QueueCard key={item.id} item={item} />)}</div>
          </>
        )}
        <div className="flex items-center justify-between border-t px-4 py-3 md:px-5">
          <p className="text-xs text-muted-foreground">{queue.data ? `显示 ${queue.data.totalItems === 0 ? 0 : (page - 1) * pageSize + 1}–${Math.min(page * pageSize, queue.data.totalItems)} / ${queue.data.totalItems}` : "正在读取档案…"}</p>
          <div className="flex items-center gap-2"><Button variant="outline" size="sm" disabled={page <= 1 || queue.isFetching} onClick={() => setPage((value) => Math.max(1, value - 1))}><ArrowLeft className="size-3.5" /><span className="hidden sm:inline">上一页</span></Button><Button variant="outline" size="sm" disabled={page >= totalPages || queue.isFetching} onClick={() => setPage((value) => Math.min(totalPages, value + 1))}><span className="hidden sm:inline">下一页</span><ArrowRight className="size-3.5" /></Button></div>
        </div>
      </Card>

      <div className="flex items-start gap-2 rounded-lg border border-sky-200 bg-sky-50 px-4 py-3 text-xs leading-5 text-sky-950"><Clock3 className="mt-0.5 size-4 shrink-0" /><p>每条记录都保留来源系统、检测时间与信号解释。风险等级用于排定人工核查顺序，不可单独作为账户处置结论。</p></div>
    </div>
  );
}

function QueueRow({ item }: { item: import("@/generated/riskcase-api").RiskCaseQueueItem }) {
  return <tr className="group transition-colors hover:bg-slate-50/80">
    <td className="min-w-64 px-5 py-4"><Link className="font-semibold text-slate-900 hover:text-teal-800" to={`/cases/${item.id}`}>{item.alert.externalAlertId}</Link><div className="mt-1 max-w-80 truncate text-xs text-muted-foreground">{item.signalSummary}</div><div className="mt-1 text-[11px] text-slate-400">{item.alert.sourceSystem}</div></td>
    <td className="min-w-48 px-4 py-4"><div className="font-medium">{item.customerName}</div><div className="mt-1 text-xs text-muted-foreground">{item.customerNumber} · {item.accountMaskedNumber}</div></td>
    <td className="min-w-24 whitespace-nowrap px-4 py-4 text-xs text-slate-600">{categoryLabel(item.alert.category)}</td>
    <td className="px-4 py-4"><SeverityBadge severity={item.alert.severity} /></td>
    <td className="min-w-40 px-4 py-4"><StatusBadge status={item.status} /><div className="mt-1.5 max-w-36 truncate text-[11px] text-muted-foreground">{item.assignedAnalystId ? "已分配经办人" : "等待领取"}</div></td>
    <td className="min-w-36 px-4 py-4 text-xs text-slate-600">{formatDate(item.alert.detectedAt)}</td>
  </tr>;
}

function QueueCard({ item }: { item: import("@/generated/riskcase-api").RiskCaseQueueItem }) {
  return <Link to={`/cases/${item.id}`} className="block p-4 transition-colors hover:bg-slate-50">
    <div className="flex items-start justify-between gap-3"><div className="min-w-0"><div className="truncate font-semibold">{item.customerName}</div><div className="mt-1 text-xs text-muted-foreground">{item.alert.externalAlertId} · {categoryLabel(item.alert.category)}</div></div><SeverityBadge severity={item.alert.severity} /></div>
    <div className="mt-3 line-clamp-2 text-xs text-slate-600">{item.signalSummary}</div><div className="mt-3 flex items-center justify-between"><StatusBadge status={item.status} /><span className="text-[11px] text-muted-foreground">{formatDate(item.alert.detectedAt)}</span></div>
  </Link>;
}
