import { AlertCircle } from "lucide-react";
import { ApiError, errorMessage } from "@/lib/api-error";
import { Button } from "@/components/ui/button";

export function ApiFailure({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const conflict = error instanceof ApiError && error.status === 412;
  const title = error instanceof ApiError ? ({ 400: "请检查填写内容", 403: "当前权限不允许此操作", 409: "当前业务状态不允许此操作", 503: "事务已回滚，请恢复原操作" } as Record<number,string>)[error.status] : undefined;
  return (
    <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950" role="alert">
      <AlertCircle className="mt-0.5 size-4 shrink-0" />
      <div className="min-w-0 flex-1">
        <p className="font-semibold">{conflict ? "记录已被其他窗口更新" : title ?? "操作未能完成"}</p>
        <p className="mt-1 leading-5">{conflict ? "请刷新当前案件；尚未提交的草稿仍保存在本浏览器中。" : errorMessage(error)}</p>
      </div>
      {onRetry && <Button variant="outline" size="sm" onClick={onRetry}>{conflict ? "刷新案件" : "重试"}</Button>}
    </div>
  );
}
