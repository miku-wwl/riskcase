# 10｜ASP.NET Core 请求管线

### 一次浏览器请求会经过什么

浏览器发起 `PUT /api/cases/{id}/assessment` 时，ASP.NET Core 先处理异常、路由、跨域、身份验证和授权，再进入最小 API endpoint。端点检查并发版本和幂等键后发送命令；执行结束将案件视图或 Problem Details 写回 HTTP 响应。

请求管线由顺序连接的中间件和端点过滤器组成。顺序改变可能改变安全行为，因此要读 `Program.cs`，不能只看路由函数。

### 管线顺序与职责

[figure: 10-aspnet-request-pipeline.svg]

异常处理位于靠前位置，才能转换后续异常。`UseAuthentication` 解析 Cookie 用户；`UseAuthorization` 执行路由策略；Endpoint filter 在敏感组检查反伪造 token。跨域与前端开发代理也明确限定来源。

### 组合根定义请求路径

<!-- page -->

[source: backend/RiskCase.Api/Program.cs#app.UseExceptionHandler();|12]

程序先注册各层，再按开发或测试环境执行迁移和种子，最后配置 HTTPS、异常、静态文件、路由、安全中间件及 endpoint。生产环境默认不自动迁移；除非明确设置，启动不会变更数据库结构。

### Cookie 与 CSRF 的配合

<!-- page -->

**已知**：已登录分析员对自己 Investigating 案件发出 PUT /api/cases/{id}/assessment，带 JSON 等级/理由、Cookie、反伪造头、UUID 键及 If-Match "7"。

1. UseRouting 选择端点，认证解析 Cookie，授权检查端点策略。JSON/参数绑定将输入交给端点调用链；反伪造 Endpoint filter 验证写请求来源。不能把这个 filter 误画成一定发生在正文绑定之前。
2. Endpoint 解析引号包裹的版本，建立 AssessRiskCaseCommand，经 ISender 进入应用链。缺失或格式错误的 If-Match 返回 428；失败的反伪造检查返回 400。
3. 应用层再查当前角色、验证命令，并决定执行新写入还是回放。成功响应携带案件视图和 ETag；异常处理将已映射的应用错误写成 Problem Details。

**适用边界**：CSRF token 与 Cookie 不能证明当前经办关系。上游摄入使用独立 X-Alert-Ingestion-Key；它不是浏览器 Cookie 会话，也不经过同一身份入口。

### 练习与本章小结

<!-- page -->

**练习**

1. 如果先执行授权再路由，授权中间件如何知道目标 endpoint 的策略？
2. 异常处理为什么需要包住业务执行？
3. CSRF token 与登录 Cookie 各自证明什么？它们不能替代什么？

**参考解**

1. 目标 endpoint 的 metadata 由路由选择。授权必须在知道目标策略后执行；显式 UseRouting 在本应用里先于 UseAuthorization。
2. 外层异常处理才能捕获下游应用异常并转为 Problem Details。它不使失败事务自动提交；事务仍由幂等存储的作用域控制。
3. Cookie 确定会话身份；反伪造 token 防护浏览器自动带 Cookie 的跨站写入。二者均不证明调用人是当前经办人，也不替代 If-Match 或领域状态规则。

**小结**：ASP.NET Core 管线决定请求的安全与执行顺序。Cookie 身份、角色授权、反伪造 token 和业务幂等各自解决不同问题。
