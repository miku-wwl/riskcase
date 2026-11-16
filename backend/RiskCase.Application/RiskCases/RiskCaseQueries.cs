using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Application.Common.Security;
using RiskCase.Domain.Constants;
using RiskCase.Domain.Entities;
using RiskCase.Domain.Enums;

namespace RiskCase.Application.RiskCases;

[Authorize(Roles = Roles.CaseReaders)]
public sealed record GetRiskCasesQuery(string? Status, string? Category, string? Severity, string? Search, bool AssignedToMe, int Page, int PageSize)
    : IRequest<RiskCaseQueuePage>;

public sealed class GetRiskCasesQueryHandler(IApplicationDbContext db, IUser user)
    : IRequestHandler<GetRiskCasesQuery, RiskCaseQueuePage>
{
    public async Task<RiskCaseQueuePage> Handle(GetRiskCasesQuery request, CancellationToken cancellationToken)
    {
        if (request.Page is < 1 or > 10000 || request.PageSize is < 1 or > 100)
            throw new ApplicationFault(400, "pagination_invalid", "页码须在 1 至 10000 之间，每页条数须在 1 至 100 之间。");
        var query = db.RiskCases.AsNoTracking()
            .Include(x => x.Alert).ThenInclude(x => x.Customer)
            .Include(x => x.Alert).ThenInclude(x => x.Account)
            .Include(x => x.Alert).ThenInclude(x => x.Signals)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<RiskCaseStatus>(request.Status, true, out var status))
                throw new ApplicationFault(400, "status_filter_invalid", "档案状态筛选值无效。");
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!Enum.TryParse<RiskAlertCategory>(request.Category, true, out var category))
                throw new ApplicationFault(400, "category_filter_invalid", "告警类别筛选值无效。");
            query = query.Where(x => x.Alert.Category == category);
        }
        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            if (!Enum.TryParse<RiskSeverity>(request.Severity, true, out var severity))
                throw new ApplicationFault(400, "severity_filter_invalid", "告警严重度筛选值无效。");
            query = query.Where(x => x.Alert.Severity == severity);
        }
        if (request.AssignedToMe)
        {
            var userId = user.Id ?? throw new UnauthorizedAccessException();
            query = query.Where(x => x.AssignedAnalystId == userId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim().ToLowerInvariant()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Alert.Customer.DisplayName.ToLower(), term) ||
                EF.Functions.Like(x.Alert.Customer.CustomerNumber.ToLower(), term) ||
                EF.Functions.Like(x.Alert.ExternalAlertId.ToLower(), term) ||
                EF.Functions.Like(x.Alert.Account.MaskedNumber.ToLower(), term) ||
                x.Alert.Signals.Any(signal => EF.Functions.Like(signal.Title.ToLower(), term) || EF.Functions.Like(signal.Code.ToLower(), term)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var cases = await query.OrderByDescending(x => x.Alert.Severity == RiskSeverity.Critical ? 4
                : x.Alert.Severity == RiskSeverity.High ? 3
                : x.Alert.Severity == RiskSeverity.Medium ? 2 : 1)
            .ThenBy(x => x.Alert.DetectedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = cases.Select(x => new RiskCaseQueueItem(
            x.Id, x.Version, x.Status, x.AssignedAnalystId, x.Created, x.LastModified,
            new(x.Alert.Id, x.Alert.ExternalAlertId, x.Alert.SourceSystem, x.Alert.Category, x.Alert.DetectedAt, x.Alert.Severity, x.Alert.RiskScore),
            x.Alert.Customer.DisplayName, x.Alert.Customer.CustomerNumber, x.Alert.Account.MaskedNumber,
            x.Alert.Signals.Count,
            string.Join("、", x.Alert.Signals.OrderByDescending(s => s.Severity).Take(2).Select(s => s.Title))))
            .ToArray();
        return new RiskCaseQueuePage(items, request.Page, request.PageSize, totalItems, (int)Math.Ceiling(totalItems / (double)request.PageSize));
    }
}

[Authorize(Roles = Roles.CaseReaders)]
public sealed record GetRiskCaseQuery(Guid CaseId) : IRequest<RiskCaseView>;

public sealed class GetRiskCaseQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRiskCaseQuery, RiskCaseView>
{
    public Task<RiskCaseView> Handle(GetRiskCaseQuery request, CancellationToken cancellationToken) =>
        RiskCasePresenter.LoadAsync(db, request.CaseId, cancellationToken);
}
