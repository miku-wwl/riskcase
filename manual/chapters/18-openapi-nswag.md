# 18｜OpenAPI 与 NSwag 客户端生成

### 第 1 页｜前后端契约要有一个来源

如果 React 团队手工维护类型、API 路径和 ASP.NET Core 请求模型，接口重命名后很容易出现“编译通过、运行失败”。RiskCase 从实际 API 生成 OpenAPI，再用 NSwag 生成 TypeScript 客户端与 DTO。

生成文件是构建产物并提交到仓库，代码评审能直接看见接口变化。开发者更新后端契约时，应重新生成客户端并一同提交。

### 第 2 页｜契约生成链

[figure: 18-openapi-nswag.svg]

ASP.NET Core 从端点与模型输出 OpenAPI JSON；NSwag 读取 JSON 并生成类型化 API 客户端；前端 adapter 为它统一加入 Cookie、CSRF 和幂等头。CI 重新生成并检查文件无差异，防止契约与客户端漂移。

### 第 3 页｜生成客户端与适配器

[source: frontend/src/lib/api.ts#export async function runIdempotentCaseMutation|25]

生成器管理 endpoint 与合同形状；`api.ts` 则集中处理跨切面请求选项、认证凭据、反伪造 token、幂等键和错误规范化。页面调用查询 hook 或 adapter，不另建一套完全独立的手工 HTTP SDK。

### 第 4 页｜一次类型变化如何传播

后端新增 AssessmentView、评估作者与被复核评估 ID 后，NSwag 同步生成 TypeScript 模型。OpenAPI 也声明 400、401、403、404、409、412、428 与事务重试 503 等相关错误语义。真实 API 测试仍需确认路由、Cookie、CSRF、枚举和请求头在运行时确实按契约工作。

契约生成并不自动证明业务语义正确：同一字段即使类型相同，也可能改了单位或含义。重要的业务字段要通过 API 测试与文档说明。

### 第 5 页｜练习与本章小结

**练习**

1. 哪些代码由 NSwag 生成，哪些横切逻辑属于 adapter？
2. 为什么 CI 要在生成后检查 git diff？
3. 改字段类型后，除了编译客户端，还应验证哪些行为？

**小结**：OpenAPI 为请求和响应提供机器可读契约；NSwag 保持客户端与后端同步。生成类型减少形状错误，业务语义仍需测试。
