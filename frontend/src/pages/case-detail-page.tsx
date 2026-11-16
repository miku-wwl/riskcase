import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { Background, Controls, Handle, MiniMap, Position, ReactFlow, type Edge, type Node, type NodeProps, type NodeTypes, MarkerType } from "@xyflow/react";
import { toast } from "sonner";
import { Activity, ArrowLeft, ArrowRight, BadgeCheck, Banknote, BookOpenCheck, CircleHelp, Clock3, FileCheck2, Link2, LoaderCircle, LockKeyhole, MessageSquareText, Plus, RefreshCw, ShieldAlert, UserRound, UserRoundCheck, Waypoints } from "lucide-react";
import {
  AssessmentRequest,
  AssignRequest,
  CaseLinkKind,
  DecisionRequest,
  LinkRequest,
  NoteRequest,
  RiskAssessmentLevel,
  RiskCaseStatus,
  RiskDisposition,
  type GraphNodeView,
  type RiskCaseView,
  type SessionView,
} from "@/generated/riskcase-api";
import { ApiFailure } from "@/components/api-failure";
import { SeverityBadge, StatusBadge, categoryLabel, dispositionLabel } from "@/components/risk-badges";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { useDraft, draftKey } from "@/lib/drafts";
import { apiCall, clients, runIdempotentCaseMutation } from "@/lib/api";
import { ApiError } from "@/lib/api-error";
import { formatDate, formatMoney } from "@/lib/utils";

type MutationIntent = {
  operation: string;
  expectedVersion: number;
  payload: unknown;
  perform: (key: string, ifMatch: string) => Promise<RiskCaseView>;
  successMessage: string;
  clearDraft?: () => void;
};

export function CaseDetailPage() {
  const { caseId } = useParams();
  const id = caseId ?? "";
  const queryClient = useQueryClient();
  const session = queryClient.getQueryData<SessionView>(["session"]);
  const caseQuery = useQuery({
    queryKey: ["case", id],
    queryFn: () => apiCall(() => clients.cases.getRiskCase(id)),
    enabled: Boolean(id),
    retry: false,
  });
  const isSupervisor = Boolean(session?.roles.includes("RiskSupervisor"));
  const investigator = Boolean(session?.roles.some((role) => ["RiskAnalyst", "SeniorAnalyst", "RiskSupervisor"].includes(role)));
  const analystsQuery = useQuery({
    queryKey: ["analysts"],
    queryFn: () => apiCall(() => clients.analysts.listAnalysts()),
    enabled: isSupervisor,
    staleTime: 60_000,
  });
  const current = caseQuery.data;
  const userId = session?.id ?? "unknown";
  const noteDraft = useDraft(draftKey(id, userId, "note"));
  const assessmentDraft = useDraft(draftKey(id, userId, "assessment"));
  const decisionDraft = useDraft(draftKey(id, userId, "decision"));
  const linkDraft = useDraft(draftKey(id, userId, "link"));
  const [assessmentLevel, setAssessmentLevel] = useState<RiskAssessmentLevel>(RiskAssessmentLevel.Medium);
  const [disposition, setDisposition] = useState<RiskDisposition>(RiskDisposition.Monitor);
  const [assigneeId, setAssigneeId] = useState("");
  const [linkSource, setLinkSource] = useState("");
  const [linkTarget, setLinkTarget] = useState("");
  const [activeTab, setActiveTab] = useState("facts");
  const [confirmOpen, setConfirmOpen] = useState(false);

  const action = useMutation<RiskCaseView, unknown, MutationIntent>({
    mutationFn: (intent) => runIdempotentCaseMutation(
      id,
      intent.operation,
      intent.expectedVersion,
      intent.payload,
      intent.perform,
    ),
    onSuccess: (result, intent) => {
      queryClient.setQueryData(["case", id], result);
      void queryClient.invalidateQueries({ queryKey: ["cases"] });
      intent.clearDraft?.();
      toast.success(intent.successMessage);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 412) void caseQuery.refetch();
    },
  });

  const runAction = (intent: Omit<MutationIntent, "expectedVersion">) => {
    if (!current) return;
    action.mutate({ ...intent, expectedVersion: current.version });
  };

  if (caseQuery.isPending) return <div className="grid min-h-96 place-items-center text-sm text-muted-foreground"><span className="flex items-center gap-2"><LoaderCircle className="size-4 animate-spin" />正在读取调查资料…</span></div>;
  if (caseQuery.isError || !current || !session) {
    return <div className="space-y-4"><Link className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground" to="/"><ArrowLeft className="size-4" />返回队列</Link><ApiFailure error={caseQuery.error} onRetry={() => void caseQuery.refetch()} /></div>;
  }

  const assignedToMe = current.assignedAnalystId === session.id;
  const isResolved = current.status === RiskCaseStatus.Resolved;
  const canInvestigate = investigator && assignedToMe && !isResolved;
  const canClaim = investigator && !current.assignedAnalystId && current.status === RiskCaseStatus.New;
  const canReview = isSupervisor && current.status === RiskCaseStatus.Reviewing && !assignedToMe;
  const request = (operation: string, payload: unknown, perform: MutationIntent["perform"], successMessage: string, clearDraft?: () => void) =>
    runAction({ operation, payload, perform, successMessage, clearDraft });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground" to="/"><ArrowLeft className="size-4" />返回风险队列</Link>
        <div className="flex items-center gap-2 text-xs text-muted-foreground"><Clock3 className="size-3.5" />最后更新 {formatDate(current.lastModified)}<span className="rounded bg-muted px-2 py-1 font-mono">v{current.version}</span></div>
      </div>

      <section className="rounded-xl border bg-card p-5 shadow-sm md:p-6">
        <div className="flex flex-col justify-between gap-5 xl:flex-row xl:items-center">
          <div className="min-w-0">
            <div className="mb-3 flex flex-wrap items-center gap-2"><StatusBadge status={current.status} /><SeverityBadge severity={current.alert.severity} /><span className="text-xs text-muted-foreground">{categoryLabel(current.alert.category)}</span></div>
            <h1 className="text-2xl font-semibold tracking-tight">{current.customer.displayName}<span className="ml-2 text-base font-normal text-muted-foreground">· {current.alert.externalAlertId}</span></h1>
            <p className="mt-2 text-sm text-muted-foreground">客户号 {current.customer.customerNumber} · 账户 {current.account.maskedNumber} · 来源 {current.alert.sourceSystem} · 检测于 {formatDate(current.alert.detectedAt)}</p>
          </div>
          <div className="grid grid-cols-2 gap-3 sm:min-w-72">
            <div className="rounded-lg bg-slate-50 px-4 py-3"><div className="text-[11px] text-muted-foreground">上游风险分</div><div className="mt-1 flex items-baseline gap-1"><span className="text-2xl font-semibold tabular-nums">{current.alert.riskScore ?? "—"}</span><span className="text-xs text-muted-foreground">/ 100</span></div></div>
            <div className="rounded-lg bg-slate-50 px-4 py-3"><div className="text-[11px] text-muted-foreground">可解释信号</div><div className="mt-1 flex items-baseline gap-1"><span className="text-2xl font-semibold tabular-nums">{current.signals.length}</span><span className="text-xs text-muted-foreground">条</span></div></div>
          </div>
        </div>
        <div className="mt-4 flex items-start gap-2 rounded-md border border-sky-200 bg-sky-50 px-3 py-2.5 text-xs leading-5 text-sky-950"><CircleHelp className="mt-0.5 size-3.5 shrink-0" /><p>告警评分和信号用于安排核查优先级。它们不能单独证明欺诈或洗钱，也不触发冻结资金或支付操作。</p></div>
      </section>

      {action.isError && <ApiFailure error={action.error} onRetry={action.variables ? () => action.mutate(action.variables!) : undefined} />}

      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
        <section className="min-w-0">
          <Tabs value={activeTab} onValueChange={setActiveTab}>
            <TabsList className="h-auto max-w-full flex-wrap justify-start">
              <TabsTrigger value="facts">调查事实</TabsTrigger>
              <TabsTrigger value="signals">风险信号 <span className="ml-1 text-[10px]">{current.signals.length}</span></TabsTrigger>
              <TabsTrigger value="graph">关系图谱</TabsTrigger>
              <TabsTrigger value="audit">审计记录</TabsTrigger>
            </TabsList>
            <TabsContent value="facts" className="space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <Card><CardHeader className="pb-3"><CardTitle className="flex items-center gap-2 text-sm"><UserRound className="size-4 text-teal-700" />客户资料</CardTitle></CardHeader><CardContent><div className="grid grid-cols-2 gap-y-3 text-sm"><Fact label="客户名称" value={current.customer.displayName} /><Fact label="客户编号" value={current.customer.customerNumber} /><Fact label="客户分层" value={current.customer.segment} /><Fact label="国家 / 地区" value={current.customer.countryCode} /><Fact label="风险层级" value={current.customer.riskTier} /><Fact label="案件负责人" value={current.assignedAnalystId ? "已分配" : "等待领取"} /></div></CardContent></Card>
                <Card><CardHeader className="pb-3"><CardTitle className="flex items-center gap-2 text-sm"><Banknote className="size-4 text-teal-700" />关联账户</CardTitle></CardHeader><CardContent><div className="grid grid-cols-2 gap-y-3 text-sm"><Fact label="账户号码" value={current.account.maskedNumber} /><Fact label="账户类型" value={current.account.accountType} /><Fact label="币种" value={current.account.currency} /><Fact label="状态" value={current.account.status} /><Fact label="开户日期" value={formatDate(current.account.openedOn)} /><Fact label="已记录交易" value={`${current.transactions.length} 笔`} /></div></CardContent></Card>
              </div>
              <Card className="overflow-hidden"><CardHeader className="pb-3"><div className="flex items-center justify-between"><div><CardTitle className="text-sm">关联交易记录</CardTitle><p className="mt-1 text-xs text-muted-foreground">只读历史观察数据，不是账户余额或账本。</p></div><BadgeCheck className="size-4 text-teal-700" /></div></CardHeader>
                {current.transactions.length === 0 ? <CardContent className="text-sm text-muted-foreground">当前告警没有附带交易记录。</CardContent> : <div className="overflow-x-auto"><table className="w-full min-w-[680px] text-left text-xs"><thead className="border-y bg-slate-50 text-muted-foreground"><tr><th className="px-5 py-2.5 font-medium">时间 / 引用</th><th className="px-3 py-2.5 font-medium">方向</th><th className="px-3 py-2.5 font-medium">金额</th><th className="px-3 py-2.5 font-medium">交易对手</th><th className="px-5 py-2.5 font-medium">账户 / 渠道</th></tr></thead><tbody className="divide-y">{current.transactions.map((transaction) => <tr key={transaction.id}><td className="px-5 py-3"><div className="font-medium text-slate-900">{formatDate(transaction.occurredAt)}</div><div className="mt-1 font-mono text-[10px] text-muted-foreground">{transaction.externalReference}</div></td><td className="px-3 py-3">{transaction.direction === "incoming" ? "转入" : "转出"}</td><td className="px-3 py-3 font-medium tabular-nums">{formatMoney(transaction.amount, transaction.currency)}</td><td className="px-3 py-3"><div>{transaction.counterpartyLabel}</div><div className="mt-1 text-[10px] text-muted-foreground">{transaction.counterpartyReference}</div></td><td className="px-5 py-3"><div>{transaction.accountMaskedNumber}</div><div className="mt-1 text-muted-foreground">{transaction.channel}</div></td></tr>)}</tbody></table></div>}
              </Card>
            </TabsContent>

            <TabsContent value="signals" className="space-y-3">
              <div className="rounded-md border bg-slate-50 p-4 text-xs leading-5 text-muted-foreground">来源系统提供这些可解释线索。分析员应核对涉及的交易与历史行为，并记录支持或反驳线索的证据。</div>
              {current.signals.map((signal) => <Card key={signal.code}><CardContent className="flex gap-4 p-5"><div className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-lg bg-amber-50 text-amber-700"><ShieldAlert className="size-4" /></div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><h3 className="text-sm font-semibold">{signal.title}</h3><SeverityBadge severity={signal.severity} /></div><p className="mt-2 text-sm leading-6 text-slate-600">{signal.explanation}</p><div className="mt-3 flex flex-wrap items-center gap-1.5">{signal.transactionReferences.map((reference) => <span key={reference} className="rounded bg-muted px-2 py-1 font-mono text-[10px] text-muted-foreground">{reference}</span>)}</div><div className="mt-3 font-mono text-[10px] tracking-wide text-slate-400">{signal.code}</div></div></CardContent></Card>)}
            </TabsContent>

            <TabsContent value="graph" className="space-y-4">
              <Card className="overflow-hidden"><CardHeader className="pb-3"><div className="flex items-center justify-between"><div><CardTitle className="text-sm">实体关系调查图</CardTitle><p className="mt-1 text-xs text-muted-foreground">实线表示记录中的账户持有、交易活动和交易对手关系；紫色边表示人工调查关联。</p></div><Waypoints className="size-4 text-teal-700" /></div></CardHeader><CardContent className="p-0"><RiskGraph caseView={current} /></CardContent></Card>
              {canInvestigate && !isResolved && current.graphNodes.length > 1 && <Card><CardHeader className="pb-3"><CardTitle className="flex items-center gap-2 text-sm"><Link2 className="size-4" />记录调查关系</CardTitle><p className="text-xs text-muted-foreground">新关系会写入案件并出现在审计记录中。解释需说明它支持、反驳或关联哪项事实。</p></CardHeader><CardContent><div className="grid gap-3 md:grid-cols-2"><div className="space-y-1.5"><Label>起点实体</Label><select className="h-10 w-full rounded-md border bg-background px-3 text-sm" value={linkSource || current.graphNodes[0]?.id} onChange={(event) => setLinkSource(event.target.value)}>{current.graphNodes.map((node) => <option key={node.id} value={node.id}>{node.label} · {node.type}</option>)}</select></div><div className="space-y-1.5"><Label>终点实体</Label><select className="h-10 w-full rounded-md border bg-background px-3 text-sm" value={linkTarget || current.graphNodes[1]?.id} onChange={(event) => setLinkTarget(event.target.value)}>{current.graphNodes.map((node) => <option key={node.id} value={node.id}>{node.label} · {node.type}</option>)}</select></div><div className="space-y-1.5 md:col-span-2"><Label>关系判断</Label><select className="h-10 w-full rounded-md border bg-background px-3 text-sm" defaultValue={CaseLinkKind.Related} id="link-kind"><option value={CaseLinkKind.Related}>相关线索</option><option value={CaseLinkKind.Supports}>支持当前判断</option><option value={CaseLinkKind.Contradicts}>反驳当前判断</option></select></div><div className="space-y-1.5 md:col-span-2"><Label htmlFor="link-rationale">事实说明</Label><Textarea id="link-rationale" value={linkDraft.value} onChange={(event) => linkDraft.setValue(event.target.value)} placeholder="描述这两个实体之间可观察到的关系，以及目前无法据此得出的结论。" maxLength={500} /></div><div className="flex items-center justify-between md:col-span-2"><span className="text-xs text-muted-foreground">至少 20 个字符 · 草稿保存在当前浏览器</span><Button disabled={action.isPending || linkDraft.value.trim().length < 20} onClick={() => { const kind = (document.getElementById("link-kind") as HTMLSelectElement).value as CaseLinkKind; const source = linkSource || current.graphNodes[0]?.id; const target = linkTarget || current.graphNodes[1]?.id; const rationale = linkDraft.value; request("relationship", { source, target, kind, rationale }, (key, tag) => clients.cases.links(id, key, tag, new LinkRequest({ sourceNodeId: source, targetNodeId: target, kind, rationale })), "关系线索已记录", () => linkDraft.clearIfUnchanged(rationale)); }}><Plus className="size-4" />记录关系</Button></div></div></CardContent></Card>}
            </TabsContent>

            <TabsContent value="audit" className="space-y-3">
              <Card><CardHeader className="pb-3"><CardTitle className="text-sm">案件审计时间线</CardTitle><p className="text-xs text-muted-foreground">每个版本对应一次持久化业务变化；事件与案件更新在同一事务内写入。</p></CardHeader><CardContent className="pt-0"><ol className="space-y-0">{[...current.auditHistory].reverse().map((event, index) => <li key={`${event.version}-${event.operation}`} className="relative flex gap-4 pb-5 last:pb-0"><div className="relative flex w-5 shrink-0 justify-center"><span className="z-10 mt-1 size-2.5 rounded-full border-2 border-teal-600 bg-white" />{index < current.auditHistory.length - 1 && <span className="absolute top-4 h-full w-px bg-border" />}</div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center justify-between gap-2"><div className="font-medium">{auditLabel(event.operation)}</div><span className="font-mono text-[10px] text-muted-foreground">版本 {event.version}</span></div><div className="mt-1 text-xs text-muted-foreground">{formatDate(event.occurredAt)} · {event.actorId.startsWith("upstream:") ? event.actorId : `操作人 ${event.actorId.slice(0, 8)}`}</div></div></li>)}</ol></CardContent></Card>
              {current.decisions.length > 0 && <Card><CardHeader className="pb-3"><CardTitle className="text-sm">最终处置</CardTitle></CardHeader><CardContent className="space-y-3">{current.decisions.map((decision) => <div key={decision.id} className="rounded-md border border-emerald-200 bg-emerald-50/60 p-4"><div className="flex flex-wrap items-center justify-between gap-2"><span className="font-semibold text-emerald-900">{dispositionLabel(decision.disposition)}</span><span className="text-xs text-muted-foreground">{formatDate(decision.decidedAt)}</span></div><p className="mt-2 text-sm leading-6 text-slate-700">{decision.rationale}</p><p className="mt-2 text-[11px] text-muted-foreground">独立复核 · 版本 {decision.caseVersion}</p></div>)}</CardContent></Card>}
            </TabsContent>
          </Tabs>
        </section>

        <aside className="space-y-4">
          <Card><CardHeader className="pb-3"><CardTitle className="flex items-center gap-2 text-sm"><UserRoundCheck className="size-4 text-teal-700" />案件负责人</CardTitle></CardHeader><CardContent className="space-y-3">
            <div className="rounded-md bg-slate-50 p-3"><div className="text-xs text-muted-foreground">当前分配</div><div className="mt-1 truncate text-sm font-medium">{current.assignedAnalystId ? (assignedToMe ? `${session.email}（你）` : "已分配给其他分析员") : "未分配"}</div></div>
            {isSupervisor && !isResolved && <div className="space-y-2"><Label htmlFor="assignee">分配 / 转交给分析员</Label><select id="assignee" className="h-10 w-full rounded-md border bg-background px-3 text-sm" value={assigneeId} onChange={(event) => setAssigneeId(event.target.value)}><option value="">选择分析员</option>{(analystsQuery.data ?? []).map((analyst) => <option key={analyst.id} value={analyst.id}>{analyst.email}</option>)}</select><Button variant="outline" className="w-full" disabled={!assigneeId || assigneeId === current.assignedAnalystId || action.isPending} onClick={() => request("assignment", { analystId: assigneeId }, (key, tag) => clients.cases.assignment(id, key, tag, new AssignRequest({ analystId: assigneeId })), "案件负责人已更新")}><UserRoundCheck className="size-4" />更新负责人</Button></div>}
            {canClaim && <Button className="w-full" disabled={action.isPending} onClick={() => request("assignment", { analystId: session.id }, (key, tag) => clients.cases.assignment(id, key, tag, new AssignRequest({ analystId: session.id })), "案件已分配给你")}><UserRoundCheck className="size-4" />领取案件</Button>}
            {canInvestigate && current.status === RiskCaseStatus.New && <Button className="w-full" disabled={action.isPending} onClick={() => request("start", null, (key, tag) => clients.cases.start(id, key, tag), "调查已开始")}><Activity className="size-4" />开始调查</Button>}
            {!isResolved && current.assignedAnalystId && !assignedToMe && !isSupervisor && <p className="text-xs leading-5 text-muted-foreground">此案件由其他分析员负责。你可以查看调查事实与审计历史。</p>}
          </CardContent></Card>

          <Card><CardHeader className="pb-3"><CardTitle className="flex items-center gap-2 text-sm"><BookOpenCheck className="size-4 text-teal-700" />调查记录</CardTitle></CardHeader><CardContent className="space-y-4">
            {current.assessmentLevel && <div className="rounded-md border bg-slate-50 p-3"><div className="flex items-center justify-between text-xs"><span className="text-muted-foreground">当前风险评估</span><span className="font-semibold text-foreground">{assessmentLabel(current.assessmentLevel)}</span></div><p className="mt-2 text-xs leading-5 text-slate-600">{current.assessmentRationale}</p><p className="mt-2 text-[10px] text-muted-foreground">保存时间 {formatDate(current.assessedAt)}</p></div>}
            {canInvestigate && current.status === RiskCaseStatus.Investigating && <div className="space-y-3">
              <div className="space-y-1.5"><Label htmlFor="note">调查笔记</Label><Textarea id="note" value={noteDraft.value} onChange={(event) => noteDraft.setValue(event.target.value)} placeholder="写下已核对的事实、未解决的问题或下一步调查计划。" maxLength={2000} /></div>
              <div className="flex items-center justify-between"><span className="text-[11px] text-muted-foreground">{noteDraft.value.length}/2000 · 自动保存在浏览器</span><Button size="sm" variant="outline" disabled={action.isPending || !noteDraft.value.trim()} onClick={() => { const text = noteDraft.value; request("note", { text }, (key, tag) => clients.cases.notes(id, key, tag, new NoteRequest({ text })), "调查笔记已保存", () => noteDraft.clearIfUnchanged(text)); }}><MessageSquareText className="size-3.5" />保存笔记</Button></div>
              <div className="border-t pt-3" />
              <div className="space-y-1.5"><Label htmlFor="assessment-level">评估风险等级</Label><select id="assessment-level" value={assessmentLevel} onChange={(event) => setAssessmentLevel(event.target.value as RiskAssessmentLevel)} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value={RiskAssessmentLevel.Low}>低</option><option value={RiskAssessmentLevel.Medium}>中</option><option value={RiskAssessmentLevel.High}>高</option><option value={RiskAssessmentLevel.Critical}>紧急</option></select></div>
              <div className="space-y-1.5"><Label htmlFor="assessment-rationale">评估依据</Label><Textarea id="assessment-rationale" value={assessmentDraft.value} onChange={(event) => assessmentDraft.setValue(event.target.value)} placeholder="结合信号和已核实事实解释等级；说明还存在哪些不确定性。" maxLength={1500} /></div>
              <Button className="w-full" disabled={action.isPending || assessmentDraft.value.trim().length < 20} onClick={() => { const rationale = assessmentDraft.value; request("assessment", { level: assessmentLevel, rationale }, (key, tag) => clients.cases.assessment(id, key, tag, new AssessmentRequest({ level: assessmentLevel, rationale })), "风险评估已保存", () => assessmentDraft.clearIfUnchanged(rationale)); }}><FileCheck2 className="size-4" />保存风险评估</Button>
              <p className="text-[11px] leading-4 text-muted-foreground">评估依据至少 20 个字符。提交复核前必须保存评估。</p>
              {current.assessmentLevel && <Button variant="outline" className="w-full" disabled={action.isPending} onClick={() => request("review", null, (key, tag) => clients.cases.review(id, key, tag), "案件已提交独立复核")}><ArrowRight className="size-4" />提交独立复核</Button>}
            </div>}
            {canInvestigate && current.status === RiskCaseStatus.Reviewing && <div className="space-y-3"><div className="rounded-md border border-violet-200 bg-violet-50 p-3 text-xs leading-5 text-violet-900">案件已提交复核。主管可独立作出处置；如需补充调查，可退回调查阶段。</div><Button variant="outline" className="w-full" disabled={action.isPending} onClick={() => request("resume", null, (key, tag) => clients.cases.resume(id, key, tag), "案件已退回调查")}><RefreshCw className="size-4" />退回调查</Button></div>}
            {canReview && <div className="space-y-3"><div className="rounded-md border border-violet-200 bg-violet-50 p-3 text-xs leading-5 text-violet-900">你不是当前经办人。复核结论将与调查人员分离记录。</div><div className="space-y-1.5"><Label htmlFor="disposition">处置建议</Label><select id="disposition" value={disposition} onChange={(event) => setDisposition(event.target.value as RiskDisposition)} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value={RiskDisposition.Escalate}>升级调查</option><option value={RiskDisposition.Monitor}>继续监测</option><option value={RiskDisposition.CloseFalsePositive}>误报结案</option><option value={RiskDisposition.ReferForAmlReview}>转交 AML 复核</option></select></div><div className="space-y-1.5"><Label htmlFor="decision-rationale">处置依据</Label><Textarea id="decision-rationale" value={decisionDraft.value} onChange={(event) => decisionDraft.setValue(event.target.value)} placeholder="引用调查结论、支持与反驳的事实，以及后续处理理由。" maxLength={2000} /></div><Button className="w-full" disabled={action.isPending || decisionDraft.value.trim().length < 20} onClick={() => setConfirmOpen(true)}><BadgeCheck className="size-4" />提交处置决定</Button><p className="text-[11px] leading-4 text-muted-foreground">处置不会执行资金冻结、支付或账户余额修改。</p></div>}
            {current.status === RiskCaseStatus.Resolved && <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-xs leading-5 text-emerald-900"><LockKeyhole className="mr-1 inline size-3.5" />案件已结案并锁定。调查与复核历史只读保存。</div>}
            {!canInvestigate && !canReview && !canClaim && !isResolved && <p className="text-xs leading-5 text-muted-foreground">当前角色或案件分配状态不允许写入。可查看资料与事件历史。</p>}
          </CardContent></Card>

          <Card><CardHeader className="pb-3"><CardTitle className="text-sm">近期笔记</CardTitle></CardHeader><CardContent>{current.notes.length === 0 ? <p className="text-xs text-muted-foreground">尚无调查笔记。</p> : <div className="space-y-3">{[...current.notes].reverse().slice(0, 3).map((note) => <div key={note.id} className="border-l-2 border-teal-300 pl-3"><p className="text-xs leading-5 text-slate-700">{note.text}</p><p className="mt-1 text-[10px] text-muted-foreground">{formatDate(note.occurredAt)} · v{note.caseVersion}</p></div>)}</div>}</CardContent></Card>
        </aside>
      </div>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>确认提交独立处置</DialogTitle><DialogDescription>此操作会关闭案件并写入不可静默修改的审计事件。请检查处置内容。</DialogDescription></DialogHeader>
          <div className="rounded-md border bg-slate-50 p-4"><div className="text-sm font-semibold">{dispositionLabel(disposition)}</div><p className="mt-2 text-sm leading-6 text-slate-600">{decisionDraft.value}</p></div>
          <DialogFooter><Button variant="outline" onClick={() => setConfirmOpen(false)}>返回检查</Button><Button disabled={action.isPending} onClick={() => { const rationale = decisionDraft.value; request("decision", { disposition, rationale }, (key, tag) => clients.cases.decision(id, key, tag, new DecisionRequest({ disposition, rationale })), "处置决定已记录", () => decisionDraft.clearIfUnchanged(rationale)); setConfirmOpen(false); }}>确认并记录处置</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div><div className="text-[11px] text-muted-foreground">{label}</div><div className="mt-1 break-words text-xs font-medium">{value}</div></div>;
}

function RiskGraph({ caseView }: { caseView: RiskCaseView }) {
  const nodes = useMemo<Node<GraphNodeView, "riskNode">[]>(() => {
    const col: Record<string, number> = { customer: 0, account: 1, transaction: 2, counterparty: 3 };
    const rowCount: Record<string, number> = {};
    return caseView.graphNodes.map((item) => {
      const row = rowCount[item.type] ?? 0;
      rowCount[item.type] = row + 1;
      return { id: item.id, type: "riskNode", position: { x: (col[item.type] ?? 1) * 260 + 20, y: row * 125 + 24 }, data: item, draggable: false };
    });
  }, [caseView.graphNodes]);
  const edges = useMemo<Edge[]>(() => caseView.graphEdges.map((edge) => ({
    id: edge.id,
    source: edge.source,
    target: edge.target,
    label: edge.label,
    type: "smoothstep",
    markerEnd: { type: MarkerType.ArrowClosed, color: edge.rationale ? "#8b5cf6" : "#94a3b8" },
    style: { stroke: edge.rationale ? "#8b5cf6" : "#94a3b8", strokeWidth: edge.rationale ? 2 : 1.5 },
    labelStyle: { fill: edge.rationale ? "#6d28d9" : "#475569", fontSize: 10 },
    labelBgStyle: { fill: "#ffffff", fillOpacity: 0.88 },
    title: edge.rationale ?? undefined,
  })), [caseView.graphEdges]);
  const nodeTypes = useMemo<NodeTypes>(() => ({ riskNode: RiskGraphNode }), []);
  const maxRows = Math.max(1, ...Object.values(nodes.reduce<Record<string, number>>((counts, node) => { const kind = node.data.type; counts[kind] = (counts[kind] ?? 0) + 1; return counts; }, {})));
  return <div className="h-[560px] w-full bg-[radial-gradient(#e2e8f0_1px,transparent_1px)] [background-size:18px_18px]">
    {nodes.length === 0 ? <div className="grid h-full place-items-center text-sm text-muted-foreground">当前告警没有可展示的关系数据。</div> : <ReactFlow nodes={nodes} edges={edges} nodeTypes={nodeTypes} nodesDraggable={false} nodesConnectable={false} elementsSelectable fitView fitViewOptions={{ padding: 0.18, maxZoom: 1.2 }} minZoom={0.3} maxZoom={1.5} proOptions={{ hideAttribution: true }}>
      <Background color="#cbd5e1" gap={18} size={1} /><Controls position="bottom-left" showInteractive={false} /><MiniMap position="bottom-right" pannable zoomable nodeStrokeWidth={3} />
    </ReactFlow>}
    <div className="pointer-events-none sr-only">图谱共 {nodes.length} 个实体节点和 {edges.length} 条关系，最多 {maxRows} 个同类实体。</div>
  </div>;
}

type RiskGraphNode = Node<GraphNodeView, "riskNode">;
function RiskGraphNode({ data }: NodeProps<RiskGraphNode>) {
  const typeName = ({ customer: "客户", account: "账户", transaction: "交易", counterparty: "交易对手" } as Record<string, string>)[data.type] ?? data.type;
  return <div className={`w-[188px] rounded-lg border bg-white p-3 shadow-md ${data.isAlertSubject ? "border-teal-400 ring-2 ring-teal-100" : "border-slate-200"}`}>
    <Handle type="target" position={Position.Left} className="!size-2 !border-white !bg-slate-400" />
    <div className="mb-1 flex items-center justify-between"><span className="text-[9px] font-semibold uppercase tracking-[0.12em] text-teal-800">{typeName}</span>{data.isAlertSubject && <span className="rounded-full bg-teal-50 px-1.5 py-0.5 text-[8px] font-semibold text-teal-800">告警主体</span>}</div>
    <div className="truncate text-xs font-semibold text-slate-900" title={data.label}>{data.label}</div>
    <div className="mt-1 line-clamp-2 text-[9px] leading-4 text-slate-500" title={data.detail}>{data.detail}</div>
    <Handle type="source" position={Position.Right} className="!size-2 !border-white !bg-slate-400" />
  </div>;
}

function assessmentLabel(level: string) {
  return ({ low: "低", medium: "中", high: "高", critical: "紧急" } as Record<string, string>)[level] ?? level;
}

function auditLabel(operation: string) {
  return ({
    AlertIngested: "上游告警已接收",
    AnalystAssigned: "案件负责人已更新",
    InvestigationStarted: "调查已开始",
    NoteAdded: "调查笔记已保存",
    RelationshipRecorded: "实体关系已记录",
    AssessmentRecorded: "风险评估已保存",
    ReviewRequested: "案件已提交复核",
    InvestigationResumed: "案件已退回调查",
    DispositionRecorded: "独立处置已记录",
  } as Record<string, string>)[operation] ?? operation;
}
