# 18｜OpenAPI 与 NSwag 客户端生成

### 第 1 页｜前后端契约要有一个来源

如果 React 团队手工维护类型、API 路径和 ASP.NET Core 请求模型，接口重命名后很容易出现“编译通过、运行失败”。RiskCase 从实际 API 生成 OpenAPI，再用 NSwag 生成 TypeScript 客户端与 DTO。

生成文件是构建产物并提交到仓库，代码评审能直接看见接口变化。开发者更新后端契约时，应重新生成客户端并一同提交。

### 第 2 页｜契约生成链

[figure: 18-openapi-nswag.svg]

ASP.NET Core 从端点与模型输出 OpenAPI JSON；NSwag 读取 JSON 并生成类型化 API 客户端；前端 adapter 为它统一加入 Cookie、CSRF 和幂等头。CI 重新生成并检查文件无差异，防止契约与客户端漂移。

### 第 3 页｜一个可空正文背后的兼容含义

[source: backend/RiskCase.Api/Endpoints/RiskCases.cs#public static async Task<IResult> Resume|9]

ResumeRequest 声明可空是为了让已提交的旧无正文请求到达幂等回放。新操作进入 Handler 后仍必须提供原因；带正文但长度无效的请求由验证器拒绝。可空传输类型与“允许新业务省略原因”不是同一含义。

NSwag 从这个端点生成 ResumeRequest 和 resume 的正文参数。页面与未知结果恢复路径都调用生成客户端；旧待恢复意图保持 bodyless，新意图保留原 reason、版本和幂等键，不能临时换键冒充重试。

### 第 4 页｜指纹变化不能破坏已提交操作的恢复

命令新增 Reason 后，其指纹形状改变。兼容证明只对 Reason 为 null 的旧请求计算去除该字段的旧形状，并核对同案、预期版本加一及 InvestigationResumed 审计。不同命令或改过原因的请求不能借旧键重放。

历史回执仍返回当时版本，客户端随后重新读取当前状态。旧回执若混入晚于案件版本的审计，只返回该历史版本内的事件。这个处理不修改数据库中的原审计或回执，也不把旧结果重新执行为新业务变化。

实际测试同时核对 Cookie、CSRF、400、403、409、412 与回放结果；生成成功本身不能证明这些语义。

### 第 5 页｜练习与本章小结

**练习**

1. 哪些代码由 NSwag 生成，哪些横切逻辑属于 adapter？
2. 为什么 CI 要在生成后检查 git diff？
3. 改字段类型后，除了编译客户端，还应验证哪些行为？

**小结**：OpenAPI 为请求和响应提供机器可读契约；NSwag 保持客户端与后端同步。生成类型减少形状错误，业务语义仍需测试。
