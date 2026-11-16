using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RiskCase.Infrastructure.Data;

namespace RiskCase.IntegrationTests;

public sealed class RiskCaseApiFactory : WebApplicationFactory<Program>
{
    public CaseReadBarrier Barrier { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings__RiskCaseDb to a disposable PostgreSQL test database.");

        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services => services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(Barrier)));
        builder.ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:RiskCaseDb"] = connectionString,
                ["RiskCase:IngestionApiKey"] = "integration-test-key"
            }));
    }
}
