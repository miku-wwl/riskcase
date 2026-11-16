# 11｜MediatR、CQRS 与应用工作流

### 第 1 页｜命令改变事实，查询读取视图

“把案件分给分析员”是命令；“列出高风险且待复核案件”是查询。命令包含期望版本与幂等键，执行后更改持久化状态；查询包含筛选和分页，不改变业务状态。

CQRS 在这里是一种代码组织方式，不意味着部署两套数据库或引入消息总线。写模型遵守领域规则，读模型按工作区需要投影数据。

### 第 2 页｜MediatR 管线

[figure: 11-mediatr-pipeline.svg]

请求依次经过异常记录、性能计时、日志最小化、授权、验证和幂等行为，再进入唯一 handler。具体注册顺序以 DI 配置为准。横切关切集中在行为中，handler 不必重复编写相同检查。

### 第 3 页｜领域方法仍是最后规则层

[source: backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs#public sealed class AddCaseNoteCommandHandler|28]

Handler 读取案件、调用领域方法、保存变更并重新加载响应视图。MediatR 负责分派，不自动提供数据库事务或业务规则；事务由 Infrastructure 幂等存储包裹，规则由 RiskCase 实体执行。

### 第 4 页｜从 HTTP 命令到响应的实测路径

添加笔记时，API 把 `If-Match` 版本、幂等键和正文转成 `AddCaseNoteCommand`。验证器限制正文长度；授权行为排除只读审计员；幂等行为获得事务；handler 调用 `AddNote` 并保存；最终响应包含新版本和审计历史。

失败路径也可解释：无权限返回 403，过期版本返回 412，验证失败返回 400，重复但内容相同的幂等请求重放原结果。

### 第 5 页｜练习与本章小结

**练习**

1. 哪些判断应放领域对象，哪些放请求验证器？
2. 为什么使用 MediatR 并不自动保证事务完整性？
3. 把一次失败请求按验证、授权、版本和幂等四个阶段分类。

**小结**：MediatR 统一分派命令与查询，pipeline 承载横切行为；handler 协调用例，领域实体维护规则，基础设施负责持久化。
