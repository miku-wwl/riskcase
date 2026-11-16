import {
  AlertIngestionClient,
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

export async function refreshAntiForgeryToken(): Promise<void> {
  const token = await apiCall(() => clients.security.issueAntiforgeryToken());
  requestToken = token.requestToken;
}

export async function signIn(email: string, password: string): Promise<void> {
  await refreshAntiForgeryToken();
  await apiCall(() => clients.users.login(true, false, new LoginRequest({ email, password })));
  await refreshAntiForgeryToken();
}

export async function signOut(): Promise<void> {
  await refreshAntiForgeryToken();
  await apiCall(() => clients.users.logout({}));
  requestToken = null;
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
  caseId: string,
  operation: string,
  expectedVersion: number,
  payload: unknown,
  request: (key: string, ifMatch: string) => Promise<RiskCaseView>,
): Promise<RiskCaseView> {
  if (!requestToken) await refreshAntiForgeryToken();
  const slot = `riskcase:retry:${caseId}:${operation}:${expectedVersion}:${stableJson(payload)}`;
  const key = sessionStorage.getItem(slot) ?? crypto.randomUUID();
  sessionStorage.setItem(slot, key);
  const result = await apiCall(() => request(key, `"${expectedVersion}"`));
  sessionStorage.removeItem(slot);
  return result;
}

export function unauthorized(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
