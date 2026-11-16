# 09｜从官方 CleanArchitecture 到 RiskCase

### 第 1 页｜为什么先从架构边界开始

新建项目若先把所有逻辑塞进 API 控制器，后续会很难测试领域规则、替换存储或明确依赖方向。RiskCase 从 Jason Taylor CleanArchitecture v10.8.0 的真实生成解决方案起步，再把示例业务替换为金融风险调查。

我们保留四个真实项目与其注册机制，而不是仿照名字搭建一套空壳。上游版本、提交 SHA、生成方式和许可证均记在 `manual/reference/`。

### 第 2 页｜编译依赖指向业务核心

[figure: 09-clean-architecture-dependencies.svg]

Domain 不依赖其他层；Application 依赖 Domain；Infrastructure 实现 Application 定义的接口；API 是启动与组合根。运行时，API 可以调用基础设施；编译依赖仍向内指向稳定的业务规则。

### 第 3 页｜注册扩展保留模板风格

[source: backend/RiskCase.Application/DependencyInjection.cs#public static void AddApplicationServices|30]

应用层在自身注册 MediatR、验证器和 pipeline behaviors。Infrastructure 注册 `ApplicationDbContext`、Identity、幂等存储与拦截器。API 在 `Program.cs` 调用扩展并形成组合根。新实现归属于 RiskCase 的业务开发，不应误称为上游模板自带。

### 第 4 页｜阅读项目的实际调用路径

一个 HTTP 命令先由 API 绑定，再交给 `ISender`；MediatR 进入 Application handler；handler 调用 `IApplicationDbContext` 与领域方法；DI 在启动时把该接口映射到 Infrastructure 的 EF Core 实现。查询同样从 API 进入 Application，但只返回视图对象。

官方许可副本在 `manual/reference/CleanArchitecture-MIT-License.txt`。RiskCase 根目录的 Apache-2.0 许可和上游模板 MIT 许可来源不同，保留来源文本便于审查。

### 第 5 页｜练习与本章小结

**练习**

1. 为什么 Application 可依赖 Domain，而不能引用 Infrastructure？
2. 在哪里注册 PostgreSQL 实现，在哪里使用它？
3. 阅读 provenance 文档，核对版本、SHA 和生成命令，再对照 `RiskCase.slnx` 的四个项目。

**小结**：RiskCase 使用官方生成解决方案作为真实起点。四层结构保留，示例 Todo 业务被风险域替代；来源与新增内容都有明确边界。
