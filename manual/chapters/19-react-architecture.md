# 19｜React 19 与 shadcn/ui 应用结构

### 把调查任务组织成工作区

登录页建立会话；队列页帮助分析员定位任务；案件页按概览、交易、信号、图谱、调查笔记和审计分区。组件复用一致的标题、徽章、输入框、卡片、标签页与确认对话框，让用户更容易理解风险级别和当前阶段。

shadcn/ui 的代码组件放在应用内，可阅读和修改；它不是运行时黑盒组件包。Tailwind 提供统一设计 token 与响应式布局。

### 页面与组件边界

[figure: 19-react-application-structure.svg]

路由决定页面，页面组织业务区域，基础 UI 组件提供按钮和表单。`AppShell` 统一导航和身份展示；case detail 延迟加载，降低首次队列页面的 JavaScript 包体积。

### 从懒加载声明到真实路由

<!-- page -->

[source: frontend/src/app.tsx#const CaseDetailPage = lazy|1]

[source: frontend/src/app.tsx#<Route path="cases/:caseId"|1]

React Router 路由加载 `CaseDetailPage`；`lazy()` 将工作区拆成独立 chunk，`Suspense` 在首次加载期间呈现占位。常用队列首屏不必下载 React Flow 等较重的图谱依赖。

### 从队列进入真实工作区

<!-- page -->

[screenshot: round4/investigation-facts.png]

工作区保留案件身份与状态，并将高密度事实放入标签页。客户与账户字段、历史交易、风险信号和审计时间线来自 API。交互图只负责选择节点和展示关联，不将布局坐标写入风险模型。

响应式页面缩窄时让表格横向滚动或调整列，避免把风险理由压缩到不可读的字号。

### 练习与本章小结

<!-- page -->

**练习**

1. 为什么案件详情值得拆分为懒加载 chunk？
2. shadcn/ui 代码组件与只写 Tailwind class 的差别是什么？
3. 哪些工作区信息应通过标签页组织，哪些需要始终可见？

**参考解**

1. 常用队列不需立即下载包含 React Flow 的案件工作区。lazy 降低首次加载量，但首次进入案件时需要加载占位与错误恢复，这是取舍。
2. shadcn/ui 提供可编辑的交互组件与可访问语义；Tailwind 负责样式工具类。按钮、Dialog 和 Tabs 的行为不能只靠一串 class 替代。
3. 案件身份、状态、负责人和版本应持续可见；大量交易、信号、图谱及审计按任务放入标签页。切换标签不应改变业务事实或清空未提交草稿。

**小结**：React 页面围绕实际调查任务组织。可复用 UI、路由和懒加载帮助维持一致性与首屏性能。
