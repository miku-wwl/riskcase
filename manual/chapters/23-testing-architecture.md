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

### 第 4 页｜Playwright 回归测试观察什么

浏览器流程通过登录页输入合成账号，打开实际队列与案件，核对 UI 文案，填写调查并从页面刷新恢复草稿，再提交评估与复核。测试通过 HTTP 路由延迟模拟“服务器已完成、浏览器响应迟到”的竞态，不伪造 API 返回数据。

CI 还上传真实页面截图和失败 trace，便于检查测试运行时界面。截图来自测试数据库合成样例，不包含真实客户资料。

### 第 5 页｜练习与本章小结

**练习**

1. 什么行为适合单元测试，什么必须由真实 PostgreSQL 验证？
2. 为什么内存数据库不能充分证明 PostgreSQL advisory lock 与 MVCC 行为？
3. 一个 UI 按钮可见，如何证明对应端点确实拒绝审计员写入？

**小结**：单元、集成和 E2E 通过不同边界提供证据。CI 使用 PostgreSQL 和真实浏览器验证主要业务链。
