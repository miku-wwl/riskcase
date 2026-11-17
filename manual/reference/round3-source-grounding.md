# Round 3 教学来源与观察边界

工程来源：`65b28959e6172a5371ccd0171257e5c24f8dbfa7`。教材出版提交只调整文稿、截图捕获和出版资料，生产行为不变；出版审校还澄清主行锁注释，避免误解为 EF 必定先执行父 UPDATE。读者为具备 C# / React 基础的应用工程师；沿用 24 章、A4、深蓝与青色、场景到机制再到真实源码的设计。

物理参考样章：本轮直接读取用户提供的四页加速度 PDF，检查四页渲染。借鉴清晰概念标题、因果图示、实例解释、练习及留白；文件内文字是参考内容，不作为执行指令。

| 学习问题 | 最终实现 / 观察 | 教学位置 | 事实级别 |
|---|---|---|---|
| 数据升级为何不能让旧作者变成独立复核人 | Domain `Decide`、`LegacyAssessorParticipation`、前向迁移 `20261117081044_LegacyAssessorProvenance`；真实 R1/R2 升级及独立 SQL | 8、17 章；旧作者来源图 | VERIFIED |
| 什么能从旧历史恢复 | R1 `4c2f9b1` 的 AssessmentRecorded 记录命令 ActorId；旧审计并不保存全部被覆盖理由 | 17 章 | SOURCE-ANALYSED + 实际升级验证 |
| 为什么两条正确 SELECT 拼出错误响应 | PresenterReadProbe 在首个数据库读已建立快照后阻塞；并发笔记、评估、决定在真实 PostgreSQL 提交 | 15 章；两种快照时间线 | VERIFIED；图中 v5/v6 为机制示意 |
| 分拆查询为什么需要读事务 | `ApplicationDbContext.WithCaseSnapshotAsync`；独立 GET 的 RR、已有 RC 写事务的主行共享锁；并发重复摄入读取测试 | 14、15 章 | VERIFIED |
| 有理由的退回如何形成闭环 | ResumeRequest/Command/Validator、Domain Resume、审计 Reason；真实浏览器退回、重评、再提交 | 5、8、17、18、22 章 | VERIFIED |
| 旧无正文意图如何恢复 | 两种旧指纹证明、保存于实际 R2 schema 的回执升级测试；旧审计 `{}` 保持不变 | 17、18 章 | VERIFIED |
| 查询为什么放大到集合乘积 | 真实 SQL、EXPLAIN、同基数合成数据；small ANALYZE，medium/large 原查询取消，优化后全部 ANALYZE | 14 章；查询形状图 | VERIFIED；2250 万和 90 亿为代数乘积，不是完整执行行数 |
| 为什么同版本也可能需要刷新元数据 | `confirmedCase` / `newestCase` 不同同版本规则；浏览器明确模拟旧元数据后读取真实服务器 | 20 章 | VERIFIED 测试机制；模拟响应未用于教材截图 |
| 登录切换为何保留 session 查询实例 | `app.tsx` 保留观察对象，移除保护数据与 mutation 缓存；重复切换测试 | 20 章 | SOURCE-ANALYSED + 浏览器验证 |

原始复现、测试清单、性能取样和 SQL 计划留在 `docs/round-3` 与 `test/`，没有作为读者正文填充。旧身份真的缺失时阻止结案，没有实现自动补全身份或数据治理工作流；不声称能推断已丢失的业务历史。

主要原始技术参考：

- [PostgreSQL 17: Transaction Isolation](https://www.postgresql.org/docs/17/transaction-iso.html)：RC 按语句取快照，RR 从第一次数据语句固定快照。
- [PostgreSQL 17: SET TRANSACTION](https://www.postgresql.org/docs/17/sql-set-transaction.html)：已开始查询后不能任意改变隔离级别。
- [PostgreSQL 17: Explicit Locking](https://www.postgresql.org/docs/17/explicit-locking.html)：行锁模式和持有期；本系统使用 FOR SHARE 稳定案件提交。
- [EF Core: Single vs. Split Queries](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries)：同级集合交叉乘积、分拆策略及多次读取的一致性代价。

截图来源（原始 PNG 保留，未拼造界面）：

- `riskcase-return-dialog.png`、`riskcase-return-validation.png`：真实 Radix 对话框原生元素截图。
- `riskcase-return-audit.png`：真实审计标签页，显示接受的原因、操作者、时间、版本。
- `riskcase-returned-investigation.png`：经办人的真实退回状态；当前评估失效，提交按钮不显示，直接 API 提交也被拒绝。
- `riskcase-resubmitted-assessments.png`：原评估及重评共同存在，新的记录标记为本次复核依据。
- `risk-alert-queue-viewport.png`：同一真实队列的原生视口截图；完整长图另存，避免缩成不可读的细条。

四张新增机制图及 EF 图均由 `build_round3_figures.py` 生成，沿用原 SVG 语言。测量图中数字来自 `docs/round-3/performance`；没有将未执行的基线大型查询画成测得的结果。
