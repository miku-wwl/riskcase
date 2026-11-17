# 11｜MediatR、CQRS 与应用工作流

### 命令改变事实，查询读取视图

“把案件分给分析员”是命令；“列出高风险且待复核案件”是查询。命令包含期望版本与幂等键，执行后更改持久化状态；查询包含筛选和分页，不改变业务状态。

CQRS 在这里是一种代码组织方式，不意味着部署两套数据库或引入消息总线。写模型遵守领域规则，读模型按工作区需要投影数据。

### MediatR 管线

[figure: 11-mediatr-pipeline.svg]

日志最小化是请求预处理器。自定义行为按 UnhandledException、Authorization、Validation、Performance、Idempotency 的顺序包裹 Handler；成功后反向退出。异常记录、计时、授权和事务并非同一边界，下面用真实注册解释。

### 领域方法仍是最后规则层

<!-- page -->

[source: backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs#public async Task<RiskCaseView> Handle(AddCaseNoteCommand|7]

Handler 读取案件、调用领域方法、保存变更并重新加载响应视图。MediatR 负责分派，不自动提供数据库事务或业务规则；事务由 Infrastructure 幂等存储包裹，规则由 RiskCase 实体执行。

### 从 HTTP 命令到响应的实测路径

<!-- page -->

**已知**：当前经办人提交一条有效笔记。用调用的进入/退出顺序读源码，而不要把所有功能都当一个平面列表。

1. Logging 是 RequestPreProcessor，先记录请求类型与用户 ID。随后自定义行为按注册顺序进入：UnhandledException → Authorization → Validation → Performance → Idempotency → Handler。
2. 授权失败就不会进入后续验证、幂等事务和 Handler。验证失败也不会开启业务事务。Performance 在这些检查之后计时；它只在 next 成功返回后停止计时并检查慢请求。
3. 幂等存储在新意图下调用 Handler。Handler 加载规则事实、调用领域、显式 Add 子记录、保存审计并读取响应；随后外层存储保存回执并提交。

**判读**：MediatR 不自动提供事务。此顺序由本项目 DI 和锁定的 MediatR 12.5.0 注册/包装实现共同确定；日志预处理发生在自定义异常包裹之外，也不是失败请求全程计时器。

实现参考：[MediatR 12.5.0 服务注册](https://github.com/jbogard/MediatR/blob/v12.5.0/src/MediatR/Registration/ServiceRegistrar.cs)与[请求行为包装](https://github.com/jbogard/MediatR/blob/v12.5.0/src/MediatR/Wrappers/RequestHandlerWrapper.cs)。这里说明的是本项目实际锁定版本的组合结果。

### 练习与本章小结

<!-- page -->

**练习**

1. 哪些判断应放领域对象，哪些放请求验证器？
2. 为什么使用 MediatR 并不自动保证事务完整性？
3. 把一次失败请求按验证、授权、版本和幂等四个阶段分类。

**参考解**

1. 格式/长度边界放 Validator，独立复核与可编辑状态放领域，当前角色和目标用户资格通过 Application 抽象查询。重要业务不变量不能只写在 UI。
2. MediatR 只组装处理链并派发请求。本项目由 PostgresIdempotencyStore 开始、提交或释放事务；没有这层显式边界，多个 SaveChanges 不会自然组成同一业务提交。
3. 无当前角色在授权层拒绝；格式错误在验证层拒绝；新操作的旧版本在 Handler/EF 返回 412；同键异命令在幂等存储返回 409。单一请求按实际遇到的最早失败结束。

**小结**：MediatR 统一分派命令与查询，pipeline 承载横切行为；handler 协调用例，领域实体维护规则，基础设施负责持久化。
