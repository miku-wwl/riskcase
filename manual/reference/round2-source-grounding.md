# Round 2 教学来源与验证边界

实现基准：`df126c70de6473f380acce209269860d7186613b`。源码节选由 source-map.md 逐个定位；出版修改未改变业务实现。

| 教学主题 | 主导源码 | 内部状态与依据 |
|---|---|---|
| 冻结复核基础、历史作者独立 | Domain/Entities/RiskCase.cs、RiskAssessment.cs；RiskCaseCommands.cs | VERIFIED：状态矩阵、真实 API、SQL 历史读取及返回/转派/重评组合 |
| 子实体状态、原子事务 | LoadForWriteAsync、PostgresIdempotencyStore、EF 配置 | VERIFIED：双读取屏障与回执插入故障后的独立 PostgreSQL 查询；语句顺序由源码解释 |
| 历史成功回放、当前授权 | AuthorizationBehaviour、IdentityService、IdempotencyBehaviour | VERIFIED：角色撤销、转派后恢复、同键竞争及旧回执操作证明兼容；不声称所有撤权/提交时序均已遍历 |
| 服务器版本与草稿代次 | case-state.ts、drafts.ts、api.ts、CaseWorkspace | VERIFIED：真实 Chromium + PostgreSQL 的 ABA、倒序响应、跨案/账号/标签页、检查后并发编辑及刷新恢复 |
| 旧数据迁移 | AssessmentReviewIntegrity 迁移 | VERIFIED：初始模式和旧复核数据升级；未知作者和丢失理由不补造 |
| 六张新机制图 | build_round2_figures.py | ILLUSTRATIVE：来自上述实现的概念图；版本与编辑代次为教学示意，非伪造运行日志 |
| 界面截图 | test/e2e/00-workflow.spec.ts、hardening.spec.ts | VERIFIED：应用与 PostgreSQL 合成资料；原始图片保留，出版注释只生成派生副本 |

框架层机制还核对以下一手说明。应用特定保证仍由本仓库实现和实际测试承担：

- [EF Core 并发令牌与冲突处理](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [EF Core 显式事务与 SaveChanges](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [PostgreSQL 17 事务隔离与语句快照](https://www.postgresql.org/docs/17/sql-set-transaction.html)
- [React 通过 key 重建工作区状态](https://react.dev/learn/preserving-and-resetting-state)
- [TanStack Query structuralSharing 配置](https://tanstack.com/query/latest/docs/framework/react/reference/interfaces/QueryOptions)

未运行的故障边界：真实进程在 COMMIT 精确时刻被杀死、实际双事务死锁编排及外部生产金融系统。教材没有把这些情况写成已验证结果。独立静态审查的七项判断、完整再现范围和遗留限制见 docs/round-2/ENGINEERING-HANDOFF.md。
