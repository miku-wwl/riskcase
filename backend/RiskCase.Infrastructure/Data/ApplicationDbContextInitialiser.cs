using RiskCase.Domain.Constants;
using RiskCase.Domain.Entities;
using RiskCase.Domain.Enums;
using RiskCase.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RiskCase.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app, bool includeDemoData)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initialiser.InitialiseAsync();
        if (includeDemoData)
            await initialiser.SeedAsync();
    }
}

public sealed class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    TimeProvider clock)
{
    public async Task InitialiseAsync()
    {
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to apply RiskCase database migrations.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        await EnsureRoleAsync(Roles.Analyst);
        await EnsureRoleAsync(Roles.SeniorAnalyst);
        await EnsureRoleAsync(Roles.Supervisor);
        await EnsureRoleAsync(Roles.Auditor);

        var analyst = await EnsureUserAsync("analyst@riskcase.local", "RiskAnalyst!2026", Roles.Analyst);
        await EnsureUserAsync("senior@riskcase.local", "RiskSenior!2026", Roles.SeniorAnalyst);
        var supervisor = await EnsureUserAsync("supervisor@riskcase.local", "RiskSupervisor!2026", Roles.Supervisor);
        await EnsureUserAsync("auditor@riskcase.local", "RiskAuditor!2026", Roles.Auditor);
        await SeedSyntheticCasesAsync(analyst.Id, supervisor.Id);
    }

    private async Task EnsureRoleAsync(string name)
    {
        if (await roleManager.RoleExistsAsync(name))
            return;
        var result = await roleManager.CreateAsync(new IdentityRole(name));
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create role '{name}': {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Could not create demo user '{email}': {string.Join("; ", created.Errors.Select(x => x.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var added = await userManager.AddToRoleAsync(user, role);
            if (!added.Succeeded)
                throw new InvalidOperationException($"Could not assign demo user '{email}' to '{role}': {string.Join("; ", added.Errors.Select(x => x.Description))}");
        }
        return user;
    }

    private async Task SeedSyntheticCasesAsync(string analystId, string supervisorId)
    {
        if (await context.RiskCases.AnyAsync())
            return;

        var now = clock.GetUtcNow();
        var c1 = Customer.Create("RC-10482", "Aroha Thompson", "Personal Banking", "NZ", "Standard");
        var c2 = Customer.Create("RC-20931", "Mateo Chen", "Small Business", "NZ", "Elevated");
        var c3 = Customer.Create("RC-30514", "Mina Patel", "Personal Banking", "AU", "Standard");
        var c4 = Customer.Create("RC-40772", "Noah Williams", "Personal Banking", "NZ", "Standard");
        var a1 = Account.Create(c1.Id, "02-****-8452", "Everyday", "NZD", DateOnly.FromDateTime(now.AddYears(-4).UtcDateTime));
        var a2 = Account.Create(c2.Id, "03-****-1269", "Business", "NZD", DateOnly.FromDateTime(now.AddYears(-2).UtcDateTime));
        var a3 = Account.Create(c3.Id, "11-****-6408", "Everyday", "AUD", DateOnly.FromDateTime(now.AddYears(-1).UtcDateTime));
        var a4 = Account.Create(c4.Id, "02-****-5173", "Everyday", "NZD", DateOnly.FromDateTime(now.AddYears(-6).UtcDateTime));

        var alert1Id = Guid.NewGuid();
        var refs1 = new[] { "TX-77104", "TX-77118", "TX-77142", "TX-77164" };
        var alert1 = RiskAlert.Receive(alert1Id, "UP-TR-00841", "Northstar Monitor", "seed-alert-1", RiskAlertCategory.TransactionRisk,
            now.AddHours(-2), RiskSeverity.High, 87, c1.Id, a1.Id,
            [RiskSignal.Explain(alert1Id, "VELOCITY_30M", "短时间转账频率上升", "30 分钟内出现 3 笔金额相近的对外转账，频率高于该客户过去 90 天的观察区间。", RiskSeverity.High, refs1),
             RiskSignal.Explain(alert1Id, "NEW_COUNTERPARTY", "出现新交易对手", "其中 2 笔转账流向近期首次出现的交易对手；该信号提示核查关系，不代表交易已被证明为欺诈。", RiskSeverity.Medium, refs1[1..3]),
             RiskSignal.Explain(alert1Id, "SHARED_COUNTERPARTY", "不同客户账户出现相同交易对手", "一笔关联账户交易与当前观察对象流向同一交易对手；这只是需要解释的关系线索，不足以推断账户持有人串通。", RiskSeverity.Medium, [refs1[3]])]);
        var alert2Id = Guid.NewGuid();
        var refs2 = new[] { "TX-88013", "TX-88027" };
        var alert2 = RiskAlert.Receive(alert2Id, "UP-FR-00317", "Northstar Monitor", "seed-alert-2", RiskAlertCategory.Fraud,
            now.AddHours(-9), RiskSeverity.Critical, 96, c2.Id, a2.Id,
            [RiskSignal.Explain(alert2Id, "DEVICE_CHANGE", "近期设备发生变化", "告警来源报告账户在短时间内从未见设备上发起新收款人转账。", RiskSeverity.Critical, refs2),
             RiskSignal.Explain(alert2Id, "BENEFICIARY_BURST", "短时新增收款人", "两个新收款人先后收到转账，建议核对客户确认记录与访问轨迹。", RiskSeverity.High, refs2)]);
        var alert3Id = Guid.NewGuid();
        var refs3 = new[] { "TX-99008", "TX-99021", "TX-99036" };
        var alert3 = RiskAlert.Receive(alert3Id, "UP-AM-00192", "Northstar Monitor", "seed-alert-3", RiskAlertCategory.AmlIndicator,
            now.AddHours(-27), RiskSeverity.Medium, 68, c3.Id, a3.Id,
            [RiskSignal.Explain(alert3Id, "PASS_THROUGH_PATTERN", "资金快速转出", "观察窗口内入账资金在较短时间内转往多个对手方；需结合账户用途与其他事实判断。", RiskSeverity.Medium, refs3)]);
        var alert4Id = Guid.NewGuid();
        var refs4 = new[] { "TX-44017", "TX-44029" };
        var alert4 = RiskAlert.Receive(alert4Id, "UP-TR-00902", "Northstar Monitor", "seed-alert-4", RiskAlertCategory.TransactionRisk,
            now.AddDays(-2), RiskSeverity.Low, 18, c4.Id, a4.Id,
            [RiskSignal.Explain(alert4Id, "PAYROLL_DAY_SPIKE", "发薪日交易集中", "短时支出集中在固定发薪日；既往资料显示该时间段存在相似模式，须核验后再排除告警。", RiskSeverity.Low, refs4)]);

        var transactions = new[]
        {
            RiskTransaction.Record(alert1Id, a1.Id, refs1[0], TransactionDirection.Outgoing, 19800m, "NZD", now.AddMinutes(-42), "Online", "CP-701", "Harbour Equipment Ltd"),
            RiskTransaction.Record(alert1Id, a1.Id, refs1[1], TransactionDirection.Outgoing, 19500m, "NZD", now.AddMinutes(-28), "Online", "CP-845", "South Ridge Services"),
            RiskTransaction.Record(alert1Id, a1.Id, refs1[2], TransactionDirection.Outgoing, 20100m, "NZD", now.AddMinutes(-11), "Mobile", "CP-845", "South Ridge Services"),
            RiskTransaction.Record(alert1Id, a2.Id, refs1[3], TransactionDirection.Outgoing, 8100m, "NZD", now.AddMinutes(-6), "Online", "CP-845", "South Ridge Services"),
            RiskTransaction.Record(alert2Id, a2.Id, refs2[0], TransactionDirection.Outgoing, 7420m, "NZD", now.AddHours(-9).AddMinutes(-18), "Mobile", "CP-114", "Koru Trading"),
            RiskTransaction.Record(alert2Id, a2.Id, refs2[1], TransactionDirection.Outgoing, 6810m, "NZD", now.AddHours(-9).AddMinutes(-7), "Mobile", "CP-229", "Meridian Parts"),
            RiskTransaction.Record(alert3Id, a3.Id, refs3[0], TransactionDirection.Incoming, 28600m, "AUD", now.AddHours(-28), "International", "CP-901", "Synthetic Overseas Sender"),
            RiskTransaction.Record(alert3Id, a3.Id, refs3[1], TransactionDirection.Outgoing, 9300m, "AUD", now.AddHours(-27).AddMinutes(-22), "Online", "CP-412", "Westline Consulting"),
            RiskTransaction.Record(alert3Id, a3.Id, refs3[2], TransactionDirection.Outgoing, 8700m, "AUD", now.AddHours(-27).AddMinutes(-4), "Online", "CP-663", "Cedar Field Imports"),
            RiskTransaction.Record(alert4Id, a4.Id, refs4[0], TransactionDirection.Incoming, 4280m, "NZD", now.AddDays(-2).AddHours(-1), "Payroll", "ORG-028", "Fictional Employer Ltd"),
            RiskTransaction.Record(alert4Id, a4.Id, refs4[1], TransactionDirection.Outgoing, 810m, "NZD", now.AddDays(-2).AddMinutes(-47), "Mobile", "CP-335", "Riverside Grocer")
        };

        var case1 = RiskCase.Domain.Entities.RiskCase.Open(alert1Id);
        case1.Assign(analystId, now.AddMinutes(-90));
        var case2 = RiskCase.Domain.Entities.RiskCase.Open(alert2Id);
        var case3 = RiskCase.Domain.Entities.RiskCase.Open(alert3Id);
        var case4 = RiskCase.Domain.Entities.RiskCase.Open(alert4Id);
        case4.Assign(analystId, now.AddDays(-2).AddHours(-1));
        case4.StartInvestigation(analystId, now.AddDays(-2).AddMinutes(-55));
        case4.RecordAssessment(analystId, RiskAssessmentLevel.Low, "发薪日与历史记录一致，资金用途符合既往日常消费模式；没有发现新的对手方或设备变更。", now.AddDays(-2).AddMinutes(-45));
        case4.RequestReview(analystId, now.AddDays(-2).AddMinutes(-35));
        case4.Decide(supervisorId, RiskDisposition.CloseFalsePositive, "复核确认交易时间与历史工资入账一致，交易规模和对手方符合既往模式；以误报结案并保留后续监测。", now.AddDays(-2).AddMinutes(-30));

        context.Customers.AddRange(c1, c2, c3, c4);
        context.Accounts.AddRange(a1, a2, a3, a4);
        context.RiskAlerts.AddRange(alert1, alert2, alert3, alert4);
        context.RiskTransactions.AddRange(transactions);
        context.RiskCases.AddRange(case1, case2, case3, case4);
        context.AuditEvents.AddRange(
            AuditEvent.Record(case1.Id, 1, "upstream:Northstar Monitor", "AlertIngested", "{}", now.AddHours(-2)),
            AuditEvent.Record(case1.Id, case1.Version, "seed:demo", "AnalystAssigned", "{}", now.AddMinutes(-90)),
            AuditEvent.Record(case2.Id, 1, "upstream:Northstar Monitor", "AlertIngested", "{}", now.AddHours(-9)),
            AuditEvent.Record(case3.Id, 1, "upstream:Northstar Monitor", "AlertIngested", "{}", now.AddHours(-27)),
            AuditEvent.Record(case4.Id, 1, "upstream:Northstar Monitor", "AlertIngested", "{}", now.AddDays(-2)),
            AuditEvent.Record(case4.Id, 2, analystId, "AnalystAssigned", "{}", now.AddDays(-2).AddHours(-1)),
            AuditEvent.Record(case4.Id, 3, analystId, "InvestigationStarted", "{}", now.AddDays(-2).AddMinutes(-55)),
            AuditEvent.Record(case4.Id, 4, analystId, "AssessmentRecorded", "{}", now.AddDays(-2).AddMinutes(-45)),
            AuditEvent.Record(case4.Id, 5, analystId, "ReviewRequested", "{}", now.AddDays(-2).AddMinutes(-35)),
            AuditEvent.Record(case4.Id, 6, supervisorId, "DispositionRecorded", "{}", now.AddDays(-2).AddMinutes(-30)));
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded four synthetic RiskCase alerts and demo identities for analyst, senior analyst, supervisor and auditor roles.");
    }
}
