# 15｜PostgreSQL MVCC 与事务

### 第 1 页｜同一请求不一定看到同一份数据库

READ COMMITTED 为每条语句建立快照。原实现先查到案件 v5，再单独读取审计；若另一个事务在中间提交 v6，第二条语句就能看到 v6 的事件。两条 SELECT 都没有脏读，组装出的响应却相互矛盾。

数据库事务的原子提交并不自动给读者提供跨语句快照。分拆集合后需要更多 SELECT，更应先定义整段读取的可见性边界。RiskCase 要求案件、集合、评估引用与审计来自可解释的同一版本视图。

### 第 2 页｜用时间线区分语句快照与请求快照

[figure: round3-consistent-snapshot.svg]

图中写事务在两次读取之间提交。上半部分各用新快照，导致 max(AuditHistory.Version) 大于案件 Version。下半部分的独立 GET 使用 REPEATABLE READ：第一次数据查询固定快照，后续集合与审计仍看到相同的已提交世界，写事务无需等待这个只读 GET。

真实 PostgreSQL 回归用测试专用读取屏障控制这一顺序，分别并发追加笔记、替换评估和最终处置。它验证返回的旧视图内部一致，也用独立连接确认新版本确已提交；单靠版本过滤无法保护分拆读取的其他集合。

### 第 3 页｜独立读取创建短生命周期快照

[source: backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs#await using var snapshot = await Database.BeginTransactionAsync|5]

该分支只在没有现有事务时执行。read 委托包含主记录、七个集合以及审计的完整读取，提交后立即释放快照。REPEATABLE READ 保证的是可见性，不替代写命令的权限、If-Match 或业务规则，也不应被长时间持有。

已有幂等事务不能再嵌套事务，也不能在已经查过回执后随意提高隔离级别。相同幂等键在 advisory lock 等待后仍要读到刚提交的回执，因此本系统保留这条写事务路径的 READ COMMITTED。

### 第 4 页｜已有写事务先稳定案件主行

[source: backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs#if (Database.CurrentTransaction is { } existing)|11]

已有 READ COMMITTED 事务先以 FOR SHARE 锁定目标案件，再读取分拆集合。所有应用业务写入都会更新案件版本，并把子记录和审计一起提交；共享锁阻止其他事务完成主行更新，因此该读取期间相关提交保持稳定。自己的写事务已持有的锁可复用，不会启动嵌套事务。

此保证依赖本系统“所有案件变更都更新主行”的写入约定，不能推广到绕过应用、只改子表的外部 SQL。已有 REPEATABLE READ 或 SERIALIZABLE 事务直接复用快照。幂等锁、案件行锁和乐观版本各自保护不同的竞争边界。

### 第 5 页｜预测读取与提交的交错

**练习**

1. READ COMMITTED 下两次 SELECT 都正确，为何响应仍会矛盾？
2. 只把审计限制为 Version ≤ v，为什么不能独自保护分拆查询？
3. 为什么独立 GET 使用快照，而幂等写事务保留 READ COMMITTED 并锁主行？

**小结**：语句可见性、事务原子性和业务版本是三个概念。详情查询的性能策略必须与明确的快照或锁边界配套。

机制参考：PostgreSQL 17 的 Transaction Isolation、SET TRANSACTION 与 SELECT 文档，以及 EF Core 的 Single vs. Split Queries；链接和源码映射见 manual/reference/round3-source-grounding.md。
