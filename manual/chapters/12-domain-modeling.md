# 12｜领域建模与业务不变量

### 围绕调查而不是名词建模

建模不是给每个业务名词都建一张表。客户、账户、交易、告警是证据资料；RiskCase 是包含生命周期与调查操作的核心对象。笔记、关联、评估和处置由案件拥有，写入时统一递增版本并产生审计事件。

这样的边界让“案件已结案不能继续改写”“复核人必须独立”等规则有一个可靠的执行位置。

### 案件聚合保护哪些不变量

[figure: 12-riskcase-aggregate.svg]

案件对象协调状态、当前负责人、评估、笔记、关联和决定。关系实体不是孤立 CRUD 入口：它们的新增要通过案件命令完成。该聚合边界关注调查一致性，不把客户全部资料都载入同一个聚合。

### 领域方法表达意图

<!-- page -->

[source: backend/RiskCase.Domain/Entities/RiskCase.cs#public RiskAssessment RecordAssessment|18]

`RecordAssessment` 表达“对当前调查基础形成一次有作者的判断”，返回新增的不可变记录。更新投影与追加历史是同一个领域动作。`Decide` 会验证状态与复核者，失败时拒绝修改。工厂方法建立有效初始对象；EF Core 的私有 setter 和集合用于物化，不应成为 API 更新模型。

### 把状态规则写成可观察结果

<!-- page -->

**已知**：Investigating v5、BasisVersion=4，当前经办人为 A。A 评估 High，然后补一条笔记，再尝试提交复核。

1. RecordAssessment 先验证状态、作者、枚举和理由；推进到 v6，并追加不可变记录 A1，A1.BasisVersion=4。当前投影指向 A1。
2. AddNote 推进到 v7，保存笔记，并将 BasisVersion 置为 7；CurrentAssessmentId、ReviewedAssessmentId 和当前理由等投影被清空，A1 历史仍在。
3. 此时 RequestReview 被拒绝，直到 A 针对新基础形成 A2。即使 A 认为风险仍为 High，也必须明确重新确认依据。

**判读**：Version 是业务动作顺序，BasisVersion 标识调查事实基础。一次评估推进 Version，不改变被评估的事实基础；二者不能混用。

### 练习与本章小结

<!-- page -->

**练习**

1. 为什么风险信号不适合成为案件聚合的一部分？
2. 用一句业务语言定义“提交复核”的前置条件。
3. 如何避免 EF Core 公共 setter 让任意 API 请求绕过领域方法？

**参考解**

1. 本项目将来源告警和信号作为稳定调查输入，案件聚合集中保护人员调查写入。这里是围绕一致性做取舍，并非声称信号在所有系统中都不能进入案件聚合。
2. 只有当前经办人在 Investigating 对最新调查基础形成有效评估，才能提交；提交会固定评估引用并冻结事实。
3. API 接收命令 DTO，Handler 调用意图明确的领域方法；实体 private setter 服务于持久化映射。不能直接把客户端 JSON 绑定到跟踪实体后 SaveChanges。

**小结**：聚合边界围绕必须一起保持的一致性选择。意图式领域方法与不变量测试使业务规则可读、可复查。
