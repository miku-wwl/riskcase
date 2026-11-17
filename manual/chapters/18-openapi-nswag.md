# 18｜OpenAPI 与 NSwag 客户端生成

### 前后端契约要有一个来源

如果 React 团队手工维护类型、API 路径和 ASP.NET Core 请求模型，接口重命名后很容易出现“编译通过、运行失败”。RiskCase 从实际 API 生成 OpenAPI，再用 NSwag 生成 TypeScript 客户端与 DTO。

生成文件是构建产物并提交到仓库，代码评审能直接看见接口变化。开发者更新后端契约时，应重新生成客户端并一同提交。

### 契约生成链

[figure: 18-openapi-nswag.svg]

ASP.NET Core 从端点与模型输出 OpenAPI JSON；NSwag 读取 JSON 并生成类型化 API 客户端；前端 adapter 为它统一加入 Cookie、CSRF 和幂等头。CI 重新生成并检查文件无差异，防止契约与客户端漂移。

### 一个可空正文背后的兼容含义

<!-- page -->

[source: backend/RiskCase.Api/Endpoints/RiskCases.cs#public static async Task<IResult> Resume|9]

ResumeRequest 声明可空是为了让已提交的旧无正文请求到达幂等回放。新操作进入 Handler 后仍必须提供原因；带正文但长度无效的请求由验证器拒绝。可空传输类型与“允许新业务省略原因”不是同一含义。

NSwag 从这个端点生成 ResumeRequest 和 resume 的正文参数。页面与未知结果恢复路径都调用生成客户端；旧待恢复意图保持 bodyless，新意图保留原 reason、版本和幂等键，不能临时换键冒充重试。

### 指纹变化不能破坏已提交操作的恢复

<!-- page -->

**已知**：退回调查接口现在需要 Reason，但历史成功操作可能没有正文。如何让旧成功可恢复，又不允许新操作省略理由？

1. API/生成 DTO 将 ResumeRequest 与正文标为可空，保留旧请求抵达幂等回放的通道；新网页始终构造有 Reason 的 ResumeRequest。
2. 当前授权仍先执行。旧指纹还需与案件 ID、后继版本和原审计操作互证；能证明是原成功才回放。
3. 新键下若没有理由，进入 Handler 时返回 400 return_reason_required；理由不满足长度同样拒绝。可空传输形状不等于可省略业务条件。

**判读**：生成成功只能证明契约能够生成。实际 Cookie、CSRF、400/403/409/412、旧回放和恢复按钮行为仍需请求级验证。旧回执按历史版本返回，随后 GET 当前状态。

### 练习与本章小结

<!-- page -->

**练习**

1. 哪些代码由 NSwag 生成，哪些横切逻辑属于 adapter？
2. 为什么 CI 要在生成后检查 git diff？
3. 改字段类型后，除了编译客户端，还应验证哪些行为？

**参考解**

1. generated/riskcase-api.ts 中的 DTO、URL 与序列化来自 NSwag。lib/api.ts 管 Cookie/CSRF 与待恢复意图；case-state.ts 管版本协调。这些手写策略不应重复声明 DTO。
2. 检查重新生成后的差异能发现忘记提交的契约变化，防止后端和已提交客户端不同步；它不证明运行时权限、错误码和业务语义正确。
3. 应实际验证 JSON 枚举、可空正文、Cookie、反伪造头、If-Match、同键回放及失败码。新增 Reason 后，旧成功可恢复，而新无理由退回必须失败。

**小结**：OpenAPI 为请求和响应提供机器可读契约；NSwag 保持客户端与后端同步。生成类型减少形状错误，业务语义仍需测试。
