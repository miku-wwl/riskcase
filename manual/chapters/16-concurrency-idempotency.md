# 16｜乐观并发与幂等重试

### 历史成功与当前许可是两件事

A 的笔记在版本 N 成功提交，但响应丢失。主管随后把案件转派给 B，版本变为 N+1。A 重试原意图时，需要知道当时的笔记是否保存；这不意味着 A 还可以再增加一条笔记，也不意味着版本 N 仍是当前状态。

RiskCase 的读者角色可以查看全案。只要 A 仍有当前有效的调查角色，原用户范围内的历史成功可恢复；如果角色已撤销或账户锁定，回执不能绕过服务器授权。

### 回放后仍要确认当前状态

[figure: 16-concurrency-and-idempotency.svg]

服务器在幂等行为之前检查当前身份角色。相同用户、相同键、相同命令指纹回放原快照；新键仍经过 Handler 的当前负责人、版本和状态检查。前端按案件 Version 保留较新缓存，并在回放成功后重新读取当前案件。

### 两个新意图竞争同一旧版本

<!-- page -->

[figure: 15-postgresql-mvcc.svg]

[source: backend/RiskCase.Infrastructure/Data/Configurations/RiskCaseEntityConfigurations.cs#b.Property(x => x.Version).IsConcurrencyToken();|1]

**已知**：不同键的 T1 与 T2 都读到 v8。Handler 的预检查在两边都可能通过；EF 仍保留原始并发令牌 8，新业务动作将实体 Version 推进为 9。

1. 保存父行时，版本令牌参与条件更新。典型路径是按案件 ID 和原 Version=8 更新；T1 提交后，T2 的同一条件不能再匹配当前 v9，受影响行数为零。
2. EF 把条件失效识别为并发异常。本系统也将同版审计、评估或决定的唯一约束竞争映射为 412；因此不能假定每次都先走父 UPDATE 的零行分支。
3. 即使失败者之前已发出子 INSERT，外层事务仍回滚其全部业务、审计和回执。死锁或序列化中止另返回可保留原键重试的 503，不把数据库竞争当成功。

**判读**：MVCC 决定每次读能看到什么，Version 条件防止新写覆盖旧依据，幂等键防止同一意图执行两次。三个机制互相配合，不能互相代替。

### 命令类型也属于一次业务意图

<!-- page -->

[source: backend/RiskCase.Application/Common/Behaviours/IdempotencyBehaviour.cs#public static string Fingerprint|12]

Start 与 Review 具有相同的 CaseId 和 ExpectedVersion 字段；旧版 Resume 也曾具有同一形状，新版则增加了 Reason。如果指纹只有字段内容，误用同一个键可能把一种命令的结果冒充另一种。指纹因此包括命令类型，同时排除传输幂等键本身。不同类型或内容复用旧键返回 409。

[source: backend/RiskCase.Infrastructure/Data/PostgresIdempotencyStore.cs#var response = await action();|5]

这是新意图成功路径的关键五行。Handler 返回时还没有提交外层事务；回执在 action 之后创建并保存，最后才 COMMIT。业务记录、审计、回执要么一起成为持久事实，要么都不提交。不要把 Handler 的 SaveChanges 误画成最终提交。

### 丢响应、刷新与安全恢复

<!-- page -->

**已知**：本轮真实浏览器使用合成数据，A 经办 Investigating v4 并提交 High 评估。拦截器等服务器返回成功后才中断浏览器响应；随后主管转派给 B，当前版本到 v6。独立读取确认最终只有一条评估及其审计。下列步骤解释观察到的结果。

1. Browser 捕获理由草稿代次 r1，先把账号、案件、assessment、原正文、ExpectedVersion=4、UUID 键 K 存入本标签页 sessionStorage，再发 PUT。Cookie、X-CSRF-TOKEN、If-Match "4" 一同发送。
2. ASP.NET Core 选择评估路由，认证/端点授权及反伪造检查通过；端点将正文和头转换为 AssessRiskCaseCommand。
3. MediatR 查当前角色并验证输入；幂等存储开始事务，取得 scope/K 的事务级 advisory lock。没有原回执才执行 Handler。
4. Handler 加载案件及规则历史，核对 v4 与当前经办关系。领域方法形成 v5 的评估 A1；EF 明确将 A1 标为 Added，并把审计和案件版本变化保存。实际 SQL 顺序不是业务提交顺序。
5. Presenter 在现有写事务中读取一致响应；能看到本事务自己的新记录。此时 Handler 的 SaveChanges 仍不是 COMMIT。
6. 外层存储序列化该 v5 响应并新增回执 K，再 SaveChanges、COMMIT。评估、审计、版本、回执一起提交；若此后 HTTP 响应丢失，浏览器只知道结果未知。
7. 主管的新意图将经办人改为 B，推进到 v6 并使当前评估投影失效。A1 仍保留历史；转派不能抹去 A 的评估责任。
8. A 刷新后恢复原键、正文和 If-Match "4"。当前调查角色仍有效时，K 返回历史 v5，Handler 不再执行；若角色撤销或账户锁定，拒绝恢复，不用旧键绕过授权。
9. React 不把 v5 覆盖已有 v6 缓存，并 GET 确认当前状态；恢复按钮保留草稿。普通成功回调也只有捕获代次仍匹配才清理，A → B → A 不能被 r1 的旧回调擦除。

**适用边界**：未知结果期间不发送同案另一种意图。明确的 400/409/412/428 可结束失败意图，但 idempotency_key_reused、401/403 与网络失败不能证明最初未提交。旧版回执还要用指纹、同案后继版本和原审计互证。应用测试可模拟提交后断线，不能据此证明机器断电、磁盘持久性、备份或灾备恢复。

### 从真实界面解释一次恢复

<!-- page -->

[screenshot: round4/unknown-pending.png]

服务器已经为 v5 返回成功后，浏览器拦截器才中断响应。网页出现待恢复提示，原 sessionStorage 意图仍持有键 K 和 If-Match "4"；这时页面没有足够信息宣布首次失败或自动创建新操作。

[screenshot: round4/unknown-recovered.png]

主管转派到 v6 后，A 刷新并点击“恢复上次操作”。观察到重放响应 Version=5，页面与重新 GET 均为 v6；原 High 判断标为历史评估，没有被误认作当前有效判断。独立 PostgreSQL 读取确认：评估 1 条、AssessmentRecorded 1 条、原键回执 1 条，其响应版本仍为 5。

**判读**：恢复回答旧操作已成功，GET 回答现在的案件状态。原理由在本地草稿中保留，回执没有复制成第二条业务判断。这里验证的是一次真实提交后丢响应场景，不是所有基础设施故障的证明。

### 恢复语义的推理练习

<!-- page -->

**练习**

1. A 被转派后，回放成功为什么不等于有权新增写入？
2. 回放得到 v12，缓存已有 v14，页面应保留哪个版本？
3. 为什么刷新后重试不能换一个新键或更新原 If-Match？

**参考解**

1. 同键恢复的是先前已授权且已提交的结果。换新键是新业务动作，仍需当前经办关系、有效角色、正确状态和预期版本。
2. 保留 v14 并 GET 重新确认。v12 是历史成功证据，不是当前状态；草稿是否清空另由捕获代次判断，不能看案件版本就删除文本。
3. 换键可能执行第二次业务动作；改 If-Match 会改变原命令指纹。恢复必须保持原意图，确认失败后才能由用户针对新状态提出新操作。

**小结**：历史结果解决未知提交，当前授权保护访问，案件版本保护新写入，单调缓存和重新读取保护当前视图。
