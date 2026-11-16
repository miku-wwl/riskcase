import { SwaggerException } from "@/generated/riskcase-api";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code: string,
    public readonly retryable: boolean,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function normalize(error: unknown): ApiError {
  if (SwaggerException.isSwaggerException(error)) {
    let detail = error.message;
    let code = "request_failed";
    try {
      const problem = JSON.parse(error.response) as { title?: string; detail?: string; code?: string; extensions?: { code?: string } };
      detail = problem.detail ?? problem.title ?? detail;
      code = problem.code ?? problem.extensions?.code ?? code;
    } catch {
      // Keep the generated client's message when the server did not return JSON.
    }
    return new ApiError(detail, error.status, code, error.status >= 500 || error.status === 0);
  }
  if (error instanceof TypeError) {
    return new ApiError("连接服务失败。网络恢复后可安全重试；写操作会沿用原幂等键。", 0, "network_error", true);
  }
  if (error instanceof Error) return new ApiError(error.message, 0, "client_error", false);
  return new ApiError("发生未知错误。", 0, "unknown_error", false);
}

export async function apiCall<T>(action: () => Promise<T>): Promise<T> {
  try {
    return await action();
  } catch (error) {
    throw normalize(error);
  }
}

export function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : "请求未能完成，请稍后重试。";
}
