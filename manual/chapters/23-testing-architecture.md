# 23｜单元、集成与端到端测试

### 测试按边界分工

单元测试快速检查领域不变量和应用行为；PostgreSQL 集成测试验证迁移、HTTP、事务、并发与权限；Playwright E2E 验证真实浏览器中的分析员和主管流程。测试总数不代表覆盖质量，关键是每个边界回答不同问题。

测试仅放在 `test/`，生产项目不引用 NUnit、Moq 或 Playwright 包。

### 测试层次与证据

[figure: 23-testing-pyramid.svg]

底层单元测试关注纯规则；中间集成测试启动 WebApplicationFactory 与 PostgreSQL；顶层 E2E 测试使用 Vite、API、Chromium 与合成数据库。CI 为集成和 E2E 分别建立 PostgreSQL 服务，避免内存数据库冒充生产行为。

### 集成测试覆盖真实并发

<!-- page -->

[source: test/integration/RiskCase.IntegrationTests/HardeningTests.cs#public async Task Concurrent_writers_share_snapshot|12]

工作流测试检查匿名拒绝、告警重复投递、案件生命周期、幂等笔记、过期版本、并发写入互斥、主管独立处置、审计记录及审核员只读。它需要一个可丢弃的 `ConnectionStrings__RiskCaseDb`，没有数据库时测试明确跳过而不伪报通过。

### 让失败条件能推翻设计承诺

<!-- page -->

**已知**：两条 notes 请求使用不同幂等键，但都从 v8 出发。若只是依次发送，请求二读到 v9，只能验证旧版本拒绝，不能证明真正竞争安全。

1. 测试专用拦截器在两条聚合读取都执行后阻塞，确认它们已经取得相同旧状态；然后显式释放屏障，不靠任意睡眠碰运气。
2. 两写继续保存。允许 200/412 任一请求获胜，但不允许两次都成功。
3. 通过独立数据库连接读取案件版本、审计、笔记、关系和回执，核对只多一条笔记及配套审计/成功回执。HTTP 状态本身不足以证明没有部分写入。

**判读**：CaseReadBarrier 只注册在 test/ 的工厂，生产不引用拦截器。浏览器延迟响应与 ABA 测试检验客户端意图保护，不能替代 PostgreSQL 对事务与约束的验证。

### 练习与本章小结

<!-- page -->

**练习**

1. 什么行为适合单元测试，什么必须由真实 PostgreSQL 验证？
2. 为什么内存数据库不能充分证明 PostgreSQL advisory lock 与 MVCC 行为？
3. 一个 UI 按钮可见，如何证明对应端点确实拒绝审计员写入？

**参考解**

1. 状态与独立性适合领域单元测试；唯一约束、事务回滚、MVCC、并发版本必须用真实 PostgreSQL。浏览器测试负责观察用户编辑与迟到响应的交互。
2. 内存库没有 PostgreSQL 行锁、事务级 advisory lock、快照或 SQLSTATE；它通过不代表真实数据库竞争路径通过。
3. 用审计员真实会话直接 POST 该端点，断言 403，再用独立 SQL 对比案件版本、子记录、审计及回执均未增加。看到按钮隐藏或只看到HTTP失败都不够。

**小结**：单元、集成和 E2E 通过不同边界提供证据。CI 使用 PostgreSQL 和真实浏览器验证主要业务链。
