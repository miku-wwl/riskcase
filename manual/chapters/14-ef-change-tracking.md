# 14｜EF Core Change Tracking

### 第 1 页｜加载规则事实与加载显示资料是两件事

领域在内存里判断重复关系与独立作者。如果 Handler 只加载案件主行，空 Links 并不能证明数据库里没有关系；没有加载旧作者来源，也不能证明主管没参与过历史评估。写入必须显式加载规则所需事实。

详情展示则同时需要客户、账户、交易、信号、笔记和历史。多个同级集合用一个 JOIN 读取会相乘：十笔交易可能被每条笔记、关系和评估重复携带。AsNoTracking 省去了跟踪开销，却不会改变这个 SQL 结果形状。

### 第 2 页｜观察集合相乘，再选择读取策略

[figure: round3-query-shape.svg]

小型合成案件的原详情 JOIN 实际输出 900 行，加审计为 901 行。优化后九条详情查询合计输出 25 行，完整保留每个集合。20 次含认证的 TestServer 请求，中位数由 146.1 ms 降至 25.5 ms；这不是网络或生产负载指标。

中、大型原查询分别具有 2250 万与 90 亿行的集合乘积，在 2 秒预算取消，不能编造其完成时间。优化后的中位数为 36.4 ms、74.2 ms，p95 为 68.3 ms、108.9 ms。原始计划和取样条件在 docs/round-3/performance；这些结果说明该负载更安全，不证明所有部署都更快。

### 第 3 页｜写入加载历史，查询复用一致性边界

[source: backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs#protected async Task<RiskCase.Domain.Entities.RiskCase> LoadForWriteAsync|10]

这里加载 Links、Assessments 与 LegacyAssessorParticipation，再核对预期版本和资源权限。领域返回新增子实体，Handler 用 DbSet.Add 明确标为 Added。父对象的并发令牌与这些 INSERT 仍在外层同一事务中提交或回滚。

只读 CaseDetails 由 Infrastructure 选择 AsSplitQuery；Presenter 把所有读取置于 WithCaseSnapshotAsync 内。分拆策略减少重复行，一致性策略则防止不同 SQL 拼出相互矛盾的视图，两者缺一不可。

### 第 4 页｜固定九条查询的代价与边界

[figure: 14-ef-change-tracking.svg]

[source: backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs#public IQueryable<RiskCaseEntity> CaseDetails|1]

查询数不随集合条数增长，避免了 N+1；但每个详情从两条 SQL 增为九条。在高网络延迟数据库上，小案件可能付出更多往返成本，本机结果不能替这种部署作结论。完整集合没有分页，响应和图谱仍按记录总数增长。

分拆查询不是普遍规则。应先确认实际 SQL 和行数，再比较投影、分页或选择性加载。EF Core 官方“Single vs. Split Queries”也明确要求考虑多次读取的一致性；本系统具体快照边界见下一章。

### 第 5 页｜从对象状态预测数据库结果

**练习**

1. 为什么空导航集合不能证明数据库中没有子记录？
2. 有非默认 GUID 的新子对象为何仍应明确声明 Added？
3. 子记录先插入、父版本更新随后冲突，事务应该留下哪些已提交记录？

**小结**：加载保证领域判断有完整事实；跟踪状态决定生成哪些 SQL；事务提交决定它们是否一起成为持久化事实。
