import { Badge } from "@/components/ui/badge";
import { RiskCaseStatus, RiskSeverity } from "@/generated/riskcase-api";

const severityLabel: Record<RiskSeverity, string> = {
  [RiskSeverity.Low]: "低",
  [RiskSeverity.Medium]: "中",
  [RiskSeverity.High]: "高",
  [RiskSeverity.Critical]: "紧急",
};

const statusLabel: Record<RiskCaseStatus, string> = {
  [RiskCaseStatus.New]: "新告警",
  [RiskCaseStatus.Investigating]: "调查中",
  [RiskCaseStatus.Reviewing]: "待复核",
  [RiskCaseStatus.Resolved]: "已结案",
};

export function SeverityBadge({ severity }: { severity: RiskSeverity }) {
  const colors: Record<RiskSeverity, string> = {
    [RiskSeverity.Low]: "bg-slate-100 text-slate-700",
    [RiskSeverity.Medium]: "bg-sky-100 text-sky-800",
    [RiskSeverity.High]: "bg-amber-100 text-amber-800",
    [RiskSeverity.Critical]: "bg-rose-100 text-rose-800",
  };
  return <Badge variant="muted" className={colors[severity]}><span className="size-1.5 rounded-full bg-current" />{severityLabel[severity]}</Badge>;
}

export function StatusBadge({ status }: { status: RiskCaseStatus }) {
  const colors: Record<RiskCaseStatus, string> = {
    [RiskCaseStatus.New]: "border-slate-200 bg-slate-100 text-slate-700",
    [RiskCaseStatus.Investigating]: "border-cyan-200 bg-cyan-50 text-cyan-800",
    [RiskCaseStatus.Reviewing]: "border-violet-200 bg-violet-50 text-violet-800",
    [RiskCaseStatus.Resolved]: "border-emerald-200 bg-emerald-50 text-emerald-800",
  };
  return <Badge variant="outline" className={colors[status]}>{statusLabel[status]}</Badge>;
}

export function categoryLabel(category: string) {
  return ({ transactionRisk: "交易风险", fraud: "欺诈风险", amlIndicator: "反洗钱线索" } as Record<string, string>)[category] ?? category;
}

export function dispositionLabel(disposition: string) {
  return ({
    escalate: "升级调查",
    monitor: "继续监测",
    closeFalsePositive: "误报结案",
    referForAmlReview: "转交 AML 复核",
  } as Record<string, string>)[disposition] ?? disposition;
}
