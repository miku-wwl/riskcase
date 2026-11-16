# 23｜单元、集成与端到端测试

### 第 1 页｜测试按边界分工

单元测试快速检查领域不变量和应用行为；PostgreSQL 集成测试验证迁移、HTTP、事务、并发与权限；Playwright E2E 验证真实浏览器中的分析员和主管流程。测试总数不代表覆盖质量，关键是每个边界回答不同问题。

测试仅放在 `test/`，生产项目不引用 NUnit、Moq 或 Playwright 包。

### 第 2 页｜测试层次与证据

[figure: 23-testing-pyramid.svg]

底层单元测试关注纯规则；中间集成测试启动 WebApplicationFactory 与 PostgreSQL；顶层 E2E 测试使用 Vite、API、Chromium 与合成数据库。CI 为集成和 E2E 分别建立 PostgreSQL 服务，避免内存数据库冒充生产行为。

### 第 3 页｜集成测试覆盖真实并发

[source: test/integration/RiskCase.IntegrationTests/RiskCaseApiWorkflowTests.cs#public async Task PostgreSql_workflow|32]

工作流测试检查匿名拒绝、告警重复投递、案件生命周期、幂等笔记、过期版本、并发写入互斥、主管独立处置、审计记录及审核员只读。它需要一个可丢弃的 `ConnectionStrings__RiskCaseDb`，没有数据库时测试明确跳过而不伪报通过。

### 第 4 页｜让失败条件能推翻设计承诺

如果两位写入者恰好顺序执行，即使结果正确，也没有证明真正的竞争边界。测试专用拦截器因此在两个案件读取语句均完成后才放行，让请求确定基于同一旧版本继续写入。最终一个成功、一个 412，独立数据库连接确认只增加一份业务记录与对应审计。

浏览器使用受控网络屏障固定“服务端已提交、页面尚未收到成功”的时刻，再编辑 A → B → A、切换案件或制造响应丢失。恢复时核对原幂等键，确认数据库中只保存一次。这些测试分别挑战服务器的原子性和客户端对用户意图的保护，不能相互替代。

### 第 5 页｜练习与本章小结

**练习**

1. 什么行为适合单元测试，什么必须由真实 PostgreSQL 验证？
2. 为什么内存数据库不能充分证明 PostgreSQL advisory lock 与 MVCC 行为？
3. 一个 UI 按钮可见，如何证明对应端点确实拒绝审计员写入？

**小结**：单元、集成和 E2E 通过不同边界提供证据。CI 使用 PostgreSQL 和真实浏览器验证主要业务链。
