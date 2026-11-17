import {
  AlertIngestionClient,
  AssessmentRequest, AssignRequest, DecisionRequest, LinkRequest, NoteRequest, ResumeRequest,
  AnalystsClient,
  LoginRequest,
  RiskCasesClient,
  SecurityClient,
  SessionClient,
  UsersClient,
  type RiskCaseView,
} from "@/generated/riskcase-api";
import { ApiError, apiCall } from "@/lib/api-error";
export { apiCall } from "@/lib/api-error";

const baseUrl = import.meta.env.VITE_RISKCASE_API_URL ?? "";
let requestToken: string | null = null;
let activeUser: string | null = null;
export function setActiveUser(userId: string | null) { activeUser = userId; }

class RiskCaseTransport {
  async fetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const headers = new Headers(init?.headers);
    const method = (init?.method ?? "GET").toUpperCase();
    if (!["GET", "HEAD", "OPTIONS"].includes(method) && requestToken) {
      headers.set("X-CSRF-TOKEN", requestToken);
    }
    return window.fetch(input, {
      ...init,
      headers,
      credentials: "include",
      cache: "no-store",
    });
  }
}

const transport = new RiskCaseTransport();

export const clients = {
  cases: new RiskCasesClient(baseUrl, transport),
  analysts: new AnalystsClient(baseUrl, transport),
  users: new UsersClient(baseUrl, transport),
  session: new SessionClient(baseUrl, transport),
  security: new SecurityClient(baseUrl, transport),
  ingestion: new AlertIngestionClient(baseUrl, transport),
};

export async function refreshAntiForgeryToken(): Promise<string> {
  const token = await apiCall(() => clients.security.issueAntiforgeryToken());
  requestToken = token.requestToken;
  return token.requestToken;
}

export async function signIn(email: string, password: string): Promise<void> {
  await refreshAntiForgeryToken();
  await apiCall(() => clients.users.login(true, false, new LoginRequest({ email, password })));
  await refreshAntiForgeryToken();
  localStorage.setItem("riskcase:session-change", crypto.randomUUID());
}

export async function signOut(): Promise<void> {
  await refreshAntiForgeryToken();
  await apiCall(() => clients.users.logout({}));
  requestToken = null;
  activeUser = null;
  localStorage.setItem("riskcase:session-change", crypto.randomUUID());
}

function stableJson(value: unknown): string {
  if (Array.isArray(value)) return `[${value.map(stableJson).join(",")}]`;
  if (value && typeof value === "object") {
    const object = value as Record<string, unknown>;
    return `{${Object.keys(object).sort().map((key) => `${JSON.stringify(key)}:${stableJson(object[key])}`).join(",")}}`;
  }
  return JSON.stringify(value) ?? "null";
}

export async function runIdempotentCaseMutation(
  userId: string,
  caseId: string,
  operation: string,
  expectedVersion: number,
  payload: unknown,
  request: (key: string, ifMatch: string) => Promise<RiskCaseView>,
): Promise<RiskCaseView> {
  if (!requestToken) await refreshAntiForgeryToken();
  const actual = await apiCall(() => clients.session.getSession());
  if (actual.id !== userId || activeUser !== userId) throw new ApiError("登录用户已变化，请重新载入工作区。", 401, "session_changed", false);
  const slot = pendingSlot(userId, caseId);
  const prior = pendingCaseMutation(userId, caseId);
  if (prior && (prior.operation !== operation || stableJson(prior.payload) !== stableJson(payload)))
    throw new ApiError("上一次写入结果尚未确认，请先恢复原操作。", 409, "pending_mutation", false);
  const intent: PendingCaseMutation = prior ?? { userId, caseId, operation, expectedVersion, payload, key: crypto.randomUUID() };
  // Persist before transmission; a reload must retain the original key, body and precondition.
  sessionStorage.setItem(slot, JSON.stringify(intent));
  try {
    const result = await apiCall(() => request(intent.key, `"${intent.expectedVersion}"`));
    if (activeUser !== userId) throw new ApiError("登录用户已变化，请重新载入工作区。", 401, "session_changed", false);
    sessionStorage.removeItem(slot);
    return result;
  } catch (error) {
    if (error instanceof ApiError && [400, 409, 412, 428].includes(error.status) && error.code !== "idempotency_key_reused") sessionStorage.removeItem(slot);
    throw error;
  }
}

export type PendingCaseMutation = { userId: string; caseId: string; operation: string; expectedVersion: number; payload: unknown; key: string };
const pendingSlot = (userId: string, caseId: string) => `riskcase:pending:${userId}:${caseId}`;
export function pendingCaseMutation(userId: string, caseId: string): PendingCaseMutation | null {
  try { return JSON.parse(sessionStorage.getItem(pendingSlot(userId, caseId)) ?? "null"); } catch { return null; }
}
export async function recoverCaseMutation(intent: PendingCaseMutation): Promise<RiskCaseView> {
  const { userId, caseId: id, operation, expectedVersion, payload } = intent;
  return runIdempotentCaseMutation(userId, id, operation, expectedVersion, payload, (key, tag) => {
    switch (operation) {
      case "assignment": return clients.cases.assignment(id, key, tag, new AssignRequest(payload as AssignRequest));
      case "start": return clients.cases.start(id, key, tag);
      case "note": return clients.cases.notes(id, key, tag, new NoteRequest(payload as NoteRequest));
      case "relationship": return clients.cases.links(id, key, tag, new LinkRequest(payload as LinkRequest));
      case "assessment": return clients.cases.assessment(id, key, tag, new AssessmentRequest(payload as AssessmentRequest));
      case "review": return clients.cases.review(id, key, tag);
      case "resume": return clients.cases.resume(id, key, tag, payload == null ? undefined : new ResumeRequest(payload as ResumeRequest));
      case "decision": return clients.cases.decision(id, key, tag, new DecisionRequest(payload as DecisionRequest));
      default: throw new ApiError("无法识别待恢复操作。", 400, "unknown_operation", false);
    }
  });
}

export function unauthorized(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
