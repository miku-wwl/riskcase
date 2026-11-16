import type { RiskCaseView } from "@/generated/riskcase-api";

export const caseKey = (userId: string, caseId: string) => ["case", userId, caseId] as const;

export function newestCase(previous: unknown, incoming: unknown): RiskCaseView {
  const old = previous as RiskCaseView | undefined;
  const next = incoming as RiskCaseView;
  return old && old.id === next.id && old.version > next.version ? old : next;
}
