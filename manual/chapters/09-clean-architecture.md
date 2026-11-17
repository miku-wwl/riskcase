# 09｜从官方 CleanArchitecture 到 RiskCase

### 为什么先从架构边界开始

新建项目若先把所有逻辑塞进 API 控制器，后续会很难测试领域规则、替换存储或明确依赖方向。RiskCase 从 Jason Taylor CleanArchitecture v10.8.0 的真实生成解决方案起步，再把示例业务替换为金融风险调查。

我们保留四个真实项目与其注册机制，而不是仿照名字搭建一套空壳。上游版本、提交 SHA、生成方式和许可证均记在 `manual/reference/`。

### 编译依赖指向业务核心

[figure: 09-clean-architecture-dependencies.svg]

Domain 不依赖其他层；Application 依赖 Domain；Infrastructure 实现 Application 定义的接口；API 是启动与组合根。运行时，API 可以调用基础设施；编译依赖仍向内指向稳定的业务规则。

### 注册扩展保留模板风格

<!-- page -->

[source: backend/RiskCase.Application/DependencyInjection.cs#public static void AddApplicationServices|15]

应用层在自身注册 MediatR、验证器和 pipeline behaviors。Infrastructure 注册 `ApplicationDbContext`、Identity、幂等存储与拦截器。API 在 `Program.cs` 调用扩展并形成组合根。新实现归属于 RiskCase 的业务开发，不应误称为上游模板自带。

### 阅读项目的实际调用路径

<!-- page -->

**已知**：POST notes 进入 API 后，调用 ISender。怎样在不让 Application 依赖 EF 实现的情况下保存一条笔记？

1. API 将请求正文、Idempotency-Key 与 If-Match 组装成 AddCaseNoteCommand；MediatR 找到 Application 中的 Handler。
2. Handler 依赖 IApplicationDbContext，并调用领域 AddNote。这个接口声明用例需要的数据库能力，业务规则不需要知道连接字符串。
3. API 启动时调用 Infrastructure 注册扩展，将接口解析为 ApplicationDbContext。EF 生成实际 SQL，外层幂等存储完成事务。

**判读**：运行时会从内层调用接口进入外层实现，编译引用仍朝向 Application/Domain。两种箭头表达不同关系，不能用一次运行调用反推项目引用。

上游 MIT 许可与 RiskCase 根目录 Apache-2.0 的来源不同，保留 provenance 供核对；新增风险规则属于 RiskCase 的业务开发。

### 练习与本章小结

<!-- page -->

**练习**

1. 为什么 Application 可依赖 Domain，而不能引用 Infrastructure？
2. 在哪里注册 PostgreSQL 实现，在哪里使用它？
3. 阅读 provenance 文档，核对版本、SHA 和生成命令，再对照 `RiskCase.slnx` 的四个项目。

**参考解**

1. Application 使用领域规则及自身定义接口；若引用 Infrastructure，业务用例会被 EF/Identity 实现绑住并形成反向耦合。Domain 不引用其他三个项目。
2. Infrastructure 注册数据库与 Identity 实现，API 作为组合根调用注册扩展。Handler 只通过 IApplicationDbContext 使用它，运行时 DI 解析到实际上下文。
3. provenance 固定上游 tag、SHA 和许可；RiskCase.slnx 的项目引用才说明当前编译边界。模板来源能证明起点，不能替本项目新增风险规则背书。

**小结**：RiskCase 使用官方生成解决方案作为真实起点。四层结构保留，示例 Todo 业务被风险域替代；来源与新增内容都有明确边界。
