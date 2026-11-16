import { defineConfig, devices } from "@playwright/test";

const apiUrl = process.env.RISKCASE_API_URL ?? "http://127.0.0.1:5250";
const frontendUrl = "http://127.0.0.1:5173";
const connectionString = process.env["ConnectionStrings__RiskCaseDb"] ?? "Host=localhost;Port=5432;Database=riskcase_e2e;Username=postgres;Password=postgres";

export default defineConfig({
  testDir: ".",
  testMatch: "**/*.spec.ts",
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? "list" : "html",
  use: {
    baseURL: frontendUrl,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    ...devices["Desktop Chrome"],
  },
  webServer: [
    {
      command: "dotnet run --configuration Release --no-build --no-launch-profile --project backend/RiskCase.Api/RiskCase.Api.csproj --urls http://127.0.0.1:5250",
      cwd: "../..",
      url: `${apiUrl}/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: "Testing",
        ConnectionStrings__RiskCaseDb: connectionString,
      },
    },
    {
      command: "npm --prefix frontend run dev -- --host 127.0.0.1",
      cwd: "../..",
      url: frontendUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: { RISKCASE_API_URL: apiUrl },
    },
  ],
  outputDir: "test-results",
});
