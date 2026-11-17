# Round 4 教学事实与来源

固定工程基准：`2177004a39a534ee1796b8391a590763cce62fc2`。本轮只编辑 `manual/`；不改应用、测试、迁移或 CI。来源摘要与行号由 `source-map.md`、`round4-source-alignment.json` 给出；最终出版页码由 `publication-page-map.json` 实际生成，不用页数公式推算。

| 教学事实 | 实际核对位置 | 处理 |
|---|---|---|
| 评估使用 PUT | Api/Endpoints/RiskCases.cs Map、Assess；生成 SDK assessment | 改正原教材 POST；真实截图捕获 PUT 并确认成功后中断响应 |
| 调查事实与复核状态 | Domain RiskCase.Assign/AddNote/AddLink/RequestReview/ResumeInvestigation/Decide | 复核冻结、退回理由、新基础失效与历史保留一致 |
| 任何历史作者不得最终复核 | Domain Decide；Application LoadForWrite；IdentityService；配置与 LegacyAssessorProvenance 迁移 | 历史作者集合与旧作者来源均参与判断；未知身份不猜测 |
| SaveChanges 不是最终 COMMIT | PostgresIdempotencyStore.ExecuteAsync；Handler SaveAsync | 先形成业务与审计/响应，外层保存回执后提交同一事务；不保证 SQL 发出顺序 |
| 两个读快照与父行锁 | ApplicationDbContext.WithCaseSnapshotAsync、CaseDetails；Presenter.LoadSnapshotAsync | 独立 GET 用 RR；已有 RC 写事务在详情读前取得 FOR SHARE，依赖所有业务写更新父行 |
| EF 跟踪与并发 | 配置 Version.IsConcurrencyToken；Handler Include、DbSet.Add；Store 的 412/409/503 映射 | 完整规则事实与 Added 状态分别解释；版本/审计约束竞争均需整体回滚 |
| MediatR 实际组合顺序 | Application/DependencyInjection；Logging/UnhandledException/Authorization/Validation/Performance/Idempotency | Logging 是预处理器；自定义链为异常→授权→验证→性能→幂等→Handler |
| 同版 GET 与历史响应 | frontend/lib/case-state.ts newestCase / confirmedCase | mutation 用 >= 保留缓存；当前 GET 仅拒绝严格更旧的版本 |
| ABA 与跨标签草稿 | frontend/lib/drafts.ts；CaseWorkspace key、capture/clearIfUnchanged | UUID 代次不等于字符串值或案件 Version；只确认提交代次，不覆盖共享草稿 |
| 图谱实际交互 | CaseDetailPage RiskGraph / RiskGraphNode；Presenter.Map | 固定位置、draggable:false；选择不打开详情页，无逐边 URL 跳转；删去原教材不真实的交互承诺 |
| 上游唯一性 | IngestAlertCommandHandler.Canonicalize/Handle；输入验证与实体唯一索引 | scope/key 与来源身份两级锁；主账户所有权与其他交易账户存在性分别核对 |
| 模板来源 | provenance、四个 csproj、实际远端 tag v10.8.0 | `git ls-remote` 当轮确认 tag 指向 e5c08ecead8a52f6cf519477bf6c57668248c035 |

## 本轮运行观察

真实 .NET API + Vite + Chromium + PostgreSQL 17.12。使用新建的 `riskcase_round4_publication` 数据库，没有重置已有数据库。两条出版场景由真实摄入接口创建，检测时间设为最后观察后一分；这些是合成案例，不是实际银行记录或检测算法评估。

- 正常闭环：v1 接收，v2 分派，v3 开始，v4 笔记，v5 High，v6 复核，v7 有理由退回，v8 补查，v9 Medium，v10 再提交，v11 独立决定。
- 未知结果：评估在 v5 真正提交后浏览器中断响应；主管转派到 v6，A 刷新沿原键恢复，收到历史 v5，当前 UI/GET 仍为 v6。
- `verify_publication_capture.py` 用独立 psql READ ONLY 事务核对版本、评估、审计、决定引用与回执；具体结果另存 JSON/SQL，不塞入教材正文。
- `capture_publication_ui.mjs` 保留 12 张原生区域及各自全屏原图、时间、页面地址、哈希、版本和观察；书内选择 9 张。没有 mock 数据替换接口响应、伪造 UI 或重绘截图。
- 性能数字来自当轮实际阅读的 `docs/round-3/performance/README.md` 与原始计划，Round 4 没有重新运行性能基准。大型原查询在预算内取消，不报告其完成时间。

## 原始技术参考（当轮实际查阅）

- [MediatR 12.5.0 服务注册](https://github.com/jbogard/MediatR/blob/v12.5.0/src/MediatR/Registration/ServiceRegistrar.cs)与[请求包装](https://github.com/jbogard/MediatR/blob/v12.5.0/src/MediatR/Wrappers/RequestHandlerWrapper.cs)：预处理器包装先注册，再注册自定义行为；包装器反转集合后聚合，因此进入时保留注册顺序。结合本项目 DI 推导具体顺序。
- [ASP.NET Core Minimal API filters](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0)：过滤器获得 handler 参数并包住调用；不是任意位于正文绑定之前的全局中间件。
- [PostgreSQL 17 隔离](https://www.postgresql.org/docs/17/transaction-iso.html)：RC 在语句间可见新提交，RR 在第一次非事务控制语句建立快照；业务版本是另一个概念。
- [PostgreSQL 17 显式锁](https://www.postgresql.org/docs/17/explicit-locking.html)：FOR SHARE 阻止其他事务更新该行；事务级 advisory lock 在事务结束时释放。
- [EF Core 单查询与分拆查询](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries)：同级集合可能相乘，分拆减少重复但增加往返，并需显式考虑跨语句一致性。

## 发现但依本轮边界未修改的应用问题

1. **显示时区不统一**：Presenter.Map 直接把 UTC 时间格式化进 graphNodes.detail；事实表通过 frontend/lib/utils.ts formatDate 使用浏览器本地时区。本轮截图相差 13 小时。教材明确按 TX 引用对齐；源代码未改。
2. **默认合成种子的告警时间线**：ApplicationDbContextInitialiser.cs 的 UP-TR-00841 检测时间为 now−2h，附带交易为 now−42/28/11/6min，不能作为完整的上游因果时间序列。出版捕获使用独立的真实摄入案例与合理检测时间，不修改种子代码，不把样例视为检测算法证明。

这两项影响样例展示与教学使用；不据此声称真实摄入工作流无法执行，也不把 Round 4 扩大为应用修复轮。
