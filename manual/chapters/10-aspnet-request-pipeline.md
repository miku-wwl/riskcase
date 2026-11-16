# 10｜ASP.NET Core 请求管线

### 第 1 页｜一次浏览器请求会经过什么

浏览器发起 `POST /api/cases/{id}/assessment` 时，ASP.NET Core 先处理异常、路由、跨域、身份验证和授权，再进入最小 API endpoint。端点检查并发版本和幂等键后发送命令；执行结束将案件视图或 Problem Details 写回 HTTP 响应。

请求管线由顺序连接的中间件和端点过滤器组成。顺序改变可能改变安全行为，因此要读 `Program.cs`，不能只看路由函数。

### 第 2 页｜管线顺序与职责

[figure: 10-aspnet-request-pipeline.svg]

异常处理位于靠前位置，才能转换后续异常。`UseAuthentication` 解析 Cookie 用户；`UseAuthorization` 执行路由策略；Endpoint filter 在敏感组检查反伪造 token。跨域与前端开发代理也明确限定来源。

### 第 3 页｜组合根定义请求路径

[source: backend/RiskCase.Api/Program.cs#var builder|38]

程序先注册各层，再按开发或测试环境执行迁移和种子，最后配置 HTTPS、异常、静态文件、路由、安全中间件及 endpoint。生产环境默认不自动迁移；除非明确设置，启动不会变更数据库结构。

### 第 4 页｜Cookie 与 CSRF 的配合

浏览器自动附带登录 Cookie，所以另一个站点可能诱使浏览器发送用户不想做的请求。RiskCase 的浏览器写操作还要求反伪造 token：客户端先取 `/api/security/antiforgery`，服务器设置 Cookie 并返回 request token，写入时同时带回 `X-CSRF-TOKEN`。

上游告警使用独立的 `X-Alert-Ingestion-Key`，不属于浏览器 Cookie 会话，故走不同身份边界。

### 第 5 页｜练习与本章小结

**练习**

1. 如果先执行授权再路由，授权中间件如何知道目标 endpoint 的策略？
2. 异常处理为什么需要包住业务执行？
3. CSRF token 与登录 Cookie 各自证明什么？它们不能替代什么？

**小结**：ASP.NET Core 管线决定请求的安全与执行顺序。Cookie 身份、角色授权、反伪造 token 和业务幂等各自解决不同问题。
