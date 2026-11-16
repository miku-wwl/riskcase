# 14｜EF Core Change Tracking

### 第 1 页｜内存中没有，不代表数据库中没有

案件的 Links 集合初始为空。如果写入 Handler 只加载案件主体，领域内的 Any 检查就可能认为一个已持久化端点对不存在。数据库唯一约束最终会拒绝重复，但这不等于应用已经正确解释了业务冲突。

同样，独立复核需要知道所有历史评估作者。把这些规则需要的集合显式加载，才让聚合在真实事实基础上判断；无须把客户和交易等所有资料都纳入可写聚合。

### 第 2 页｜加载、跟踪和提交分别做什么

[figure: 14-ef-change-tracking.svg]

Include 提供规则所需的持久化事实，Change Tracker 记录对象变化，数据库约束保护最终完整性。三者职责不同。领域拒绝重复关系时应返回 409；数据库唯一约束仍保留，防止另一条写入路径绕过规则。

### 第 3 页｜可写聚合明确加载规则所需的集合

[source: backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs#protected async Task<RiskCase.Domain.Entities.RiskCase> LoadForWriteAsync|15]

这里加载 Links 和 Assessments，然后核对预期版本与资源写权限。领域方法返回新增子实体，Handler 通过相应 DbSet.Add 明确标记 Added。修改父对象版本和插入子实体是同一次业务动作的不同数据库语句。

### 第 4 页｜SaveChanges 成功还没有提交外层事务

新增笔记会更新案件版本、插入笔记与审计。Handler 的 SaveChanges 把这些变化发给 PostgreSQL；外层幂等存储随后再保存回执并 Commit。若回执写入失败，已经执行的业务 SQL 也应回滚。

针对这个窗口的真实 PostgreSQL 故障测试在回执插入时故意失败。独立连接随后看到案件版本、笔记和审计均没有增加。这个观察说明事务覆盖了整个业务边界，而不是仅仅证明一个 SaveChanges 被调用。

### 第 5 页｜从对象状态预测数据库结果

**练习**

1. 为什么空导航集合不能证明数据库中没有子记录？
2. 有非默认 GUID 的新子对象为何仍应明确声明 Added？
3. 子记录先插入、父版本更新随后冲突，事务应该留下哪些已提交记录？

**小结**：加载保证领域判断有完整事实；跟踪状态决定生成哪些 SQL；事务提交决定它们是否一起成为持久化事实。
