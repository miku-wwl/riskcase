# 教材源码索引

下表由 `manual/tools/build_textbook.py` 从本仓库实际源码锚点生成。行号对应当前源码；PDF 代码面板显示同一文件范围。

| 章节 | 文件与行号 |
|---|---|
| 01《RiskCase 要解决的是什么问题》 | `backend/RiskCase.Api/Endpoints/AlertIngestion.cs:17-38`（`public static async Task<IResult> Ingest`） |
| 02《理解银行风险告警》 | `backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs:83-104`（`public sealed class IngestAlertCommandHandler`） |
| 03《客户、账户与交易》 | `backend/RiskCase.Domain/Entities/Account.cs:17-33`（`public static Account Create`） |
| 04《可解释的风险信号》 | `backend/RiskCase.Domain/Entities/RiskSignal.cs:18-35`（`public static RiskSignal Explain`） |
| 05《RiskCase 生命周期与不变量》 | `backend/RiskCase.Domain/Entities/RiskCase.cs:123-134`（`public void RequestReview`） |
| 06《分析员队列与分派》 | `backend/RiskCase.Application/RiskCases/RiskCaseQueries.cs:17-38`（`public async Task<RiskCaseQueuePage> Handle`） |
| 07《关系调查与图谱》 | `backend/RiskCase.Application/RiskCases/RiskCasePresenter.cs:43-64`（`public static RiskCaseView Map`） |
| 08《风险评估与处置》 | `backend/RiskCase.Domain/Entities/RiskCase.cs:147-165`（`public RiskDecision Decide`） |
| 09《从官方 CleanArchitecture 到 RiskCase》 | `backend/RiskCase.Application/DependencyInjection.cs:8-22`（`public static void AddApplicationServices`） |
| 10《ASP.NET Core 请求管线》 | `backend/RiskCase.Api/Program.cs:6-27`（`var builder`） |
| 11《MediatR、CQRS 与应用工作流》 | `backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs:314-335`（`public sealed class AddCaseNoteCommandHandler`） |
| 12《领域建模与业务不变量》 | `backend/RiskCase.Domain/Entities/RiskCase.cs:105-122`（`public RiskAssessment RecordAssessment`） |
| 13《输入验证与资源授权》 | `backend/RiskCase.Application/Common/Behaviours/AuthorizationBehaviour.cs:22-43`（`public async Task<TResponse> Handle`） |
| 14《EF Core Change Tracking》 | `backend/RiskCase.Application/RiskCases/RiskCaseCommands.cs:260-269`（`protected async Task<RiskCase.Domain.Entities.RiskCase> LoadForWriteAsync`） |
| 14《EF Core Change Tracking》 | `backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs:23-23`（`public IQueryable<RiskCaseEntity> CaseDetails`） |
| 15《PostgreSQL MVCC 与事务》 | `backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs:43-47`（`await using var snapshot = await Database.BeginTransactionAsync`） |
| 15《PostgreSQL MVCC 与事务》 | `backend/RiskCase.Infrastructure/Data/ApplicationDbContext.cs:32-42`（`if (Database.CurrentTransaction is { } existing)`） |
| 16《乐观并发与幂等重试》 | `backend/RiskCase.Application/Common/Behaviours/IdempotencyBehaviour.cs:31-42`（`public static string Fingerprint`） |
| 17《审计历史与决定完整性》 | `backend/RiskCase.Domain/Entities/RiskCase.cs:151-159`（`Require(!_legacyAssessorParticipation.Any`） |
| 18《OpenAPI 与 NSwag 客户端生成》 | `backend/RiskCase.Api/Endpoints/RiskCases.cs:90-98`（`public static async Task<IResult> Resume`） |
| 19《React 19 与 shadcn/ui 应用结构》 | `frontend/src/app.tsx:19-40`（`const CaseDetailPage = lazy`） |
| 20《TanStack Query 与本地草稿同步》 | `frontend/src/lib/drafts.ts:32-45`（`const clearIfUnchanged`） |
| 21《用 React Flow 表达风险关系》 | `frontend/src/pages/case-detail-page.tsx:269-290`（`function RiskGraph({ caseView }`） |
| 22《分析员完整工作流》 | `frontend/src/pages/case-detail-page.tsx:89-110`（`const action = useMutation`） |
| 23《单元、集成与端到端测试》 | `test/integration/RiskCase.IntegrationTests/RiskCaseApiWorkflowTests.cs:35-56`（`public async Task PostgreSql_workflow`） |
| 24《跨层故障恢复与工程取舍》 | `backend/RiskCase.Api/Infrastructure/ProblemDetailsExceptionHandler.cs:13-34`（`var problem = exception switch`） |
