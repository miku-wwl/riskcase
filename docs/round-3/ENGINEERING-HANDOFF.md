# RiskCase Round 3 工程交接

工程建议：**PASS**。F8–F11 均经独立复现后修复，核心业务保证的回归已通过；未知旧身份采用明确拒绝最终处置的策略。该建议针对本仓库的应用工程和教学冻结，不将缺失的历史资料或本机性能结果扩张为额外承诺。

## 基线与交付身份

| 项目 | 核验结果 |
|---|---|
| 仓库 / 分支 | `git@github.com:miku-wwl/riskcase.git` / `main` |
| 起点 | `4667f194e4847e06a0764d40f0bc78be7420541c`，与开始时 fetched origin/main 相同；工作区干净 |
| 最终工程实现 SHA | `65b28959e6172a5371ccd0171257e5c24f8dbfa7` |
| 工程实现 CI | [44049024666](https://github.com/miku-wwl/riskcase/actions/runs/44049024666)，三个 job 实际 success |
| 出版交付头 | 包含本交接与最终 PDF 的提交；用 `git log -1 --format=%H -- docs/round-3/ENGINEERING-HANDOFF.md` 定位，最终回执报告该完整 SHA 和其 Actions 结果 |
| 原测试基线 | 39 Domain + 6 Application + 29 PostgreSQL + 9 Browser = 83 |
| 本地最终功能测试 | 47 + 13 + 55 + 15 = **130 passed**，0 failed，0 功能跳过/未运行 |

所有更改在现有 main 完成，没有分支、PR、重写历史或 force push。范围限于 RiskCase 的 backend、frontend、test、manual、docs/round-3 和说明文档。没有修改 EchoCase、LedgerCore 或其他项目。出版提交仅调整教材、截图捕获及一条澄清 SQL 顺序的注释，生产行为与工程实现 SHA 相同。

## 独立复现与修复矩阵

| ID / 初始级别 | 分类与实际修复前结果 | 根因 / 修复 | 回归 / 最终状态 |
|---|---|---|---|
| F8 / HIGH | **CONFIRMED**。真实 R1 schema 插入多个旧作者；运行已发布 R2 迁移、重新分派、重评及提交后，旧作者的决定返回 200。独立 SQL 确认为 `Resolved/12/old-author-1`。R2 起点重复得到同一结果。 | R1 审计保留真实命令 ActorId，R2 却只重建最后的 NULL 作者快照；Decide 仅检查完整评估集合。新增不可变来源参与记录，恢复所有仍可核实的旧作者；领域同时排除新旧作者。真正未知身份明确 409 拒绝结案。 | `Upgrade_recovers_all_old_authors_and_rejects_finalization`：R1/R2 × 单/多作者；角色提升、退回、转派、重评不能洗掉旧身份，第三位主管可成功。独立连接核对案件、来源、旧审计及已提交决定。**FIXED** |
| F9 / MEDIUM | **CONFIRMED**。测试专用 EF ReaderExecuted 屏障在第一条 SQL 已取快照后暂停；并发写入提交后，原 GET 返回案件 v5 / 审计 v6，或案件 v6 / 决定审计 v7。三个正确断言均在修复前失败。 | READ COMMITTED 下主查询与审计各取新快照。独立 GET 采用短 REPEATABLE READ；已有 RC 写事务先 FOR SHARE 案件主行，复用事务；已有 RR/Serializable 直接复用。分拆集合和审计都在边界内。旧回执中过晚的审计只在回放响应中限于历史版本，不改写存档。 | `Detail_is_one_snapshot_across_collections_and_audit` 三种并发；升级库并发评估；`Snapshot_in_existing_ingestion_transaction_uses_parent_lock_without_nested_transaction`；旧回执混入未来事件回归。独立 SQL 确认新版本确已提交。**FIXED** |
| F10 / MEDIUM | **CONFIRMED**。复核中无正文退回成功 200，原 handler 写空审计正文；缺少业务反馈内容。新规则的正确拒绝断言修复前失败。 | 新 ResumeRequest / Command 传递 Reason；去除首尾空白，20–1500 Unicode scalar。验证器、领域及 handler 共同保护，接受的原因与实际 actor/time/version 写入不可变审计。旧无正文只能恢复已提交回执，新操作 400。 | 长度、空白、缺失、无权、过期版本、竞争退回、同键重放/改原因、重新评估与提交、真实 R2 回执升级、浏览器取消/确认/ABA/丢响应/刷新恢复。**FIXED** |
| F11 / MEDIUM | **CONFIRMED**。实际 SQL 为同级集合 JOIN；小型 ANALYZE 根输出 900 + 审计 1。中、大型请求均在 2 s 取消，没有假装测得完整执行时间。 | CaseDetails 在 Infrastructure 选择 AsSplitQuery；Presenter 在 F9 的一致性边界内读取，固定 9 条详情 SQL。保留全部集合，不隐式分页或截断。 | 两种规模的非计时完整性及固定 SQL 数断言进入正常 CI；显式真实 PostgreSQL 性能程序比较三种规模，实际计划及样本保留。**FIXED** |

修复前观察见 [reproduction-before.txt](reproduction-before.txt)。`test/reproduction/Verify-Round2Baseline.ps1` 从固定 SHA 的归档重现 F8，不重置工作区。正确的独立性断言在基线应失败；必须同时核对两条独立 SQL 观察，不能将构建失败误当缺陷复现。

### 重要源文件

- F8：Domain `RiskCase.cs`、`LegacyAssessorParticipation.cs`；Application `RiskCaseCommands.cs`、`RiskCasePresenter.cs`、`RiskCaseContracts.cs`；Infrastructure 实体配置、新迁移及不可变保存边界。
- F9/F11：`IApplicationDbContext.cs`、`ApplicationDbContext.cs`、`RiskCasePresenter.cs`、`PostgresIdempotencyStore.cs`。
- F10：API `Endpoints/RiskCases.cs`；应用命令/验证器与 `IdempotencyBehaviour.cs`；Domain Resume；生成 SDK、`api.ts`、案件页面和错误反馈。
- 所有测试探针、升级及性能数据工厂、复现程序均在 `test/`。生产项目没有引用测试项目，没有测试开关进入业务代码。

### 为什么旧测试没发现

旧独立性测试使用新建的完整评估，没有 R1/R2 的作者空缺；原读取测试没有强制安排两条 SQL 之间的提交；返回操作从未要求原因；原详情用小种子记录，没有测量多个集合同时增长。新测试采用真实旧 schema、独立 SQL、可控屏障和完整集合基数，分别能推翻这些原假设。

## 其他观察与修复

1. **同版本元数据无法刷新（CONFIRMED）**：`newestCase` 的 `>=` 同时用于当前 GET 与历史回执。迁移恢复身份但不推进业务版本时，刷新可保留旧元数据。浏览器明确模拟旧元数据，当前真实 GET 的同版本结果仍无法更新的断言先失败。现在 GET 使用 `confirmedCase`（只拒绝严格更旧版本），历史 mutation 保留原同版本保护。没有将模拟响应用于教材截图。
2. **登录切换观察对象生命周期（观察确认，因果机制经源码分析）**：一次原有浏览器测试中登录与 session HTTP 均为 200，UI 仍停在登录页；原用例立即重复三次通过，不能声称旧失败可稳定复现。logout 的 `client.clear()` 会移除仍被观察的 session 查询。改为保留 session/csrf 查询实例，清除受保护查询与 mutation；旧用户回调仍核对身份。两个定向场景 × 三次共六次通过，全套无重试也通过。

没有发现尚未修复的 F1–F7 回归：历史评估作者不能自审；Reviewing 冻结；历史追加；当前角色/锁定状态保护回放；分派目标有效；正反重复关系 409 且无部分记录；迟到响应不能覆盖更高版本或清掉新代次输入。

## 数据迁移与历史语义

新增：`20261117081044_LegacyAssessorProvenance`。已发布 R1/R2 迁移和 Designer 完全未改，校验摘要在 [migration-checksums.json](migration-checksums.json)。模型无待生成差异。

迁移为没有非 legacy 完整评估对应的 AssessmentRecorded 事件建立来源记录，保留 CaseId、CaseVersion、ActorId 和 SourceAuditId。不要求作者今天仍是活跃 Identity 用户，避免删除账号后丢失历史责任。保留的旧评估若没有同版本审计，记录 SourceAssessmentId 及明确未知身份；空/系统来源也不会当作可证明的人类评估者。来源外键限定同案，来源唯一，来源必须二选一，EF 与 PostgreSQL 触发器禁止改写或删除。

可恢复：仍存在的审计作者、事件时间/版本及保留摘要；最后保留的评估等级、理由和时间。不可恢复：早已覆盖的完整理由，或者已经丢失且不再留有任何痕迹的事件。完整旧评估中的 AssessorId 不被偷偷补写；身份依据独立保存。已结案旧决定保持原 ID、理由、版本和可空 AssessmentId。

| 起点 | 实际验证 | 结果 |
|---|---|---|
| Fresh | 新独立数据库运行三次迁移及真实初始化，检查历史表、种子、无 pending migration | PASS |
| R1 → R3 | 单/多作者、多个版本；未知/空身份、缺失审计、不完整正文、旧决定；R2 先失效旧复核，再 R3 恢复已知身份 | PASS |
| R2 → R3 | 在已应用 R2 的库建立历史，再升级；真实 R2 schema 中存入旧无正文回执与无理由审计，升级后安全回放，原存档不变 | PASS |
| 重复执行 | 两次 MigrateAsync，无重复重建；案件 ID、已知理由与旧审计不变，索引有效，触发器拒绝篡改 | PASS |

Down 明确拒绝销毁来源历史；如必须撤销发布，采用经验证备份恢复。身份真正未知的活跃旧案件**不能自动结案**，也没有自动解锁/编造身份的入口。

## 验证结果与边界

| 检查 | PASS | FAIL | SKIPPED / NOT RUN |
|---|---:|---:|---|
| Domain | 47 | 0 | 0 |
| Application | 13 | 0 | 0 |
| PostgreSQL 集成 | 55 | 0 | 发现 56 项，1 个 Explicit 性能项不随常规套件执行；已另行执行 |
| Chromium 浏览器 | 15 | 0 | 0；本地最终全套 retries=0 |
| 显式性能程序 | 基线 1 次、优化后 1 次均通过采集断言 | 0 | 基线 medium/large 超过取消预算的完整物化及 ANALYZE 未运行 |
| .NET restore/build；TS / Vite | 完成 | 0 | 0 |
| OpenAPI / NSwag / SDK 漂移 | 完成；重新生成一致，工程 CI 同样核验 | 0 | 0 |
| 新迁移 / 模型一致性 | 三路径完成，无 pending model change | 0 | 0 |
| 生产/测试依赖隔离与改动范围 | 完成 | 0 | 0 |

机器可读的逐项功能结果在 [verification.json](verification.json)。原始 TRX 与浏览器 traces 保存在忽略的本地 `test/results` / `test/e2e/test-results`，不作为教材内容。

新增数据一致性场景包括父版本竞争、并发评估读取、最终决定读取、已有写事务内的重复摄入读取、原因竞争与回滚、旧响应恢复及不可变来源；原有原子性、CSRF、角色/资源、重复关系/摄入和未知结果测试继续执行。没有用 EF InMemory 或任意睡眠替代数据库时序屏障。

## 性能结果

PostgreSQL 17.12、.NET 10.0.300 Release、Windows 同一本机实例，真实认证 TestServer 请求。每个已完成规模 20 个样本，p95 使用 nearest rank；SQL 数不计 BEGIN/COMMIT 协议消息。

| 数据（交易/信号/笔记/关系/评估） | 原中位 / p95 ms | 新中位 / p95 ms | 原 / 新详情 SQL |
|---|---:|---:|---:|
| 10/3/5/3/2 | 146.145 / 179.252 | 25.467 / 43.131 | 2 / 9 |
| 100/15/50/30/10 | 2,000 ms 预算取消，0 个完成样本 | 36.397 / 68.281 | 取消前尝试 1 / 9 |
| 500/30/200/100/30 | 2,000 ms 预算取消，0 个完成样本 | 74.159 / 108.926 | 取消前尝试 1 / 9 |

小型实际计划根输出从总计 901 行变为 25 行；分开采集的 ANALYZE 执行时间之和从 37.552 ms 变为 1.595 ms。新中、大型总根输出为 207、862 行。小型响应字节 15,441 → 15,463；新中、大型 132,842 / 614,497 字节。小型近似进程托管分配中位数 4,181,732 → 556,708 bytes；不是独占 EF 分配。

集合乘积 2250 万 / 90 亿是基数代数计算，不是两个取消查询的已测行数。高延迟数据库往返、生产负载和独占 EF 物化开销没有测量，不提供虚构 speedup。完整条件、实际计划、样本、分配统计及可复用程序见 [performance/README.md](performance/README.md)。正常 CI 只断言完整数据及固定查询数，不设硬件计时门槛。

## 教材与出版

出版物：`manual/dist/RiskCase-Financial-Risk-Management-Engineering-Textbook.pdf`。**127 页 / 24 章**；SHA-256：`3efeb42ad0bfde4423bfdcd5aaa365dad9e1d542bf8c5e992ed8182bc9557e76`。最终视觉覆盖以 [出版 QA](../../manual/reference/round3-publication-qa.md) 与 [结构核验](../../manual/reference/round3-pdf-structure.json) 为准。

重点修订 5、6、8、14–18、20、22、24 章；新增旧作者来源、两种快照、退回闭环、查询形状四张原创 SVG，重画 EF 加载图。加入真实退回对话框、原因审计、重新提交的评估记录；另外保留验证提示与退回调查状态原生截图。队列原长图保留，并采用新的视口截图避免缩成不可读细条。

所有页均已完整渲染并逐页覆盖检查；迭代后以页面栅格哈希确认未变范围，变化页另行检查。重要机制、源码、截图与附录按完整分辨率复核。26 个源码锚点自动重新定位，最终数量和范围记于 source-map；没有用执行日志或测试目录填充正文。具体页清单、最终校验值与已修正版式问题记录在出版 QA。

## 剩余真实限制与冻结决定

- 已经完全丢失的历史理由/事件无法重建；已知身份缺口阻止结案，没有自动豁免流程。这是保持未知的业务策略。
- 详情仍完整返回历史和交易，图谱/响应按记录总数增长；9 条查询增加往返，高网络延迟条件没有测量。回执保留与旧稿相同，没有新增自动过期清理。
- 已有写事务的读取保证依赖所有应用案件变更同时更新主行；绕过应用只改子表的管理员 SQL 不受这条应用约定覆盖。

F8–F11 没有未修复的已知业务正确性阻碍。完成最终出版头的三项 CI 核对后，RiskCase 可冻结为应用工程与图解教材仓库。不得将这个冻结结论解释为真实银行生产认证。
