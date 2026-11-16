using System.Text.Json;
using System.Text.Json.Serialization;
using RiskCase.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    await app.InitialiseDatabaseAsync(includeDemoData: true);
}
else if (builder.Configuration.GetValue<bool>("RiskCase:ApplyMigrationsOnStartup"))
{
    await app.InitialiseDatabaseAsync(includeDemoData: false);
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseFileServer();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");
app.MapEndpoints(typeof(Program).Assembly);
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
