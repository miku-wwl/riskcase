# 14｜EF Core Change Tracking

### 第 1 页｜对象怎样变成 SQL 写入

EF Core 查询实体时可将其放入 Change Tracker。分析员调用领域方法后，实体字段变化；`SaveChangesAsync` 比较跟踪状态，生成 `INSERT` 或 `UPDATE`。应用层通过 `IApplicationDbContext` 使用所需 DbSet 与保存方法，而不依赖 EF 的具体类型。

查询工作区可使用 `AsNoTracking`，因为它只读取并投影视图；写入命令需要载入可跟踪案件，以更新版本并持久化子实体。

### 第 2 页｜新增、修改、删除的追踪状态

[figure: 14-ef-change-tracking.svg]

实体从 Added 到 Unchanged，修改时从 Unchanged 到 Modified；保存后再回到 Unchanged。删除使用 Deleted 状态。对象集合只是内存表示；真实写入仍要等待 SQL 成功并由事务提交。

### 第 3 页｜EF 配置和接口边界

[source: backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs#protected override void OnModelCreating|24]

`ApplicationDbContext` 发现并应用实体配置，并暴露案件、告警、客户、审计和幂等回执集合。Infrastructure 配置 PostgreSQL 类型、关系、索引与 concurrency token；Application 接口只抽象用例需要的数据库操作。

### 第 4 页｜同一事务里的多条记录

一次新增笔记同时会改变案件版本、新增笔记和新增审计事件。若幂等行为开启事务，这些保存与幂等回执在同一事务提交。只在内存里看到更新，不代表数据库已提交；提交失败时整个事务回滚。

读取视图用 `AsNoTracking` 降低追踪成本，并通过 Presenter 汇总客户、账户、交易、信号和审计数据。DTO 不是 EF 实体，避免客户端字段绑定直接覆盖数据库模型。

### 第 5 页｜练习与本章小结

**练习**

1. 为什么读取队列通常适合无追踪查询？
2. `SaveChangesAsync` 和事务 `CommitAsync` 是什么关系？
3. 幂等回执若晚于业务保存、却在另一个事务保存，可能出现什么窗口？

**小结**：Change Tracker 识别实体变化并生成 SQL。领域方法修改跟踪实体，EF 配置映射关系，事务提交才让变化成为持久化事实。
