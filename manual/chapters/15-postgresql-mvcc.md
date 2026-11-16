# 15｜PostgreSQL MVCC 与事务

### 第 1 页｜并发读写不是简单覆盖

PostgreSQL 使用 MVCC 为事务提供一致的行版本视图。一个事务读取时，另一个事务可以继续执行；更新提交前，其他事务按隔离级别看到的仍是可见版本。提交后，新事务看到新的行版本。

MVCC 解决并发读写可见性，不知道 RiskCase 的“谁能结案”规则，也不会自动阻止两个用户根据旧版本覆盖业务判断。

### 第 2 页｜两个事务观察同一案件

[figure: 15-postgresql-mvcc.svg]

事务 T1 和 T2 都读到案件版本 8。T1 更新并提交版本 9；T2 的数据库语句如果带有旧版本条件，就更新不到目标行。EF 将这种冲突报告为 `DbUpdateConcurrencyException`，应用转换为 HTTP 412。

### 第 3 页｜数据库事务承担原子性

[source: backend/RiskCase.Infrastructure/Data/PostgresIdempotencyStore.cs#public async Task<TResponse> ExecuteAsync|44]

存储先开启事务，再通过 PostgreSQL advisory transaction lock 串行化相同用户和幂等键。它读取已有回执；不存在时才执行命令，将响应序列化为回执并保存，然后提交。异常会沿调用链返回，未提交事务在释放时回滚。

### 第 4 页｜不要混淆隔离级别与业务锁

MVCC 不等于无锁；数据库仍会对写入和唯一约束进行协调。advisory lock 是应用选择的事务级互斥信号，用于同一幂等范围。它不是案件版本锁，也不会自动防止用不同幂等键并发编辑同一案件。

唯一索引处理外部告警编号竞争；EF 的版本条件处理过期案件更新；幂等键锁住重复业务请求。三者针对不同冲突来源。

### 第 5 页｜练习与本章小结

**练习**

1. T1 写入版本 9 后，T2 为什么不能把版本 8 覆盖回去？
2. `pg_advisory_xact_lock` 与 `If-Match` 分别保护什么？
3. 为什么幂等回执必须与命令写入在同一数据库事务？

**小结**：MVCC 提供事务可见性；业务并发仍需版本条件、唯一约束或锁。原子事务把业务变化与回执绑定为一次提交。
