using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RiskCase.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")
            ?? "Host=localhost;Port=5432;Database=riskcase;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
        return new ApplicationDbContext(options);
    }
}
