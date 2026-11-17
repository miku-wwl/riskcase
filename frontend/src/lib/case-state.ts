import type { RiskCaseView } from "@/generated/riskcase-api";

export const caseKey = (userId: string, caseId: string) => ["case", userId, caseId] as const;

export function newestCase(previous: unknown, incoming: unknown): RiskCaseView {
  const old = previous as RiskCaseView | undefined;
  const next = incoming as RiskCaseView;
  return old && old.id === next.id && old.version >= next.version ? old : next;
}

// A fresh GET may add recovered legacy metadata without advancing the business version.
// Historical mutation receipts still use newestCase, where an equal version keeps the cache.
export function confirmedCase(previous: unknown, incoming: unknown): RiskCaseView {
  const old = previous as RiskCaseView | undefined;
  const next = incoming as RiskCaseView;
  return old && old.id === next.id && old.version > next.version ? old : next;
}
