import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

test("analyst investigates a cross-account alert and an independent supervisor records the disposition", async ({ browser }) => {
  const analystContext = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
  const analyst = await analystContext.newPage();
  await analyst.goto("/");
  await analyst.getByLabel("工作邮箱").fill("analyst@riskcase.local");
  await analyst.getByLabel("密码", { exact: true }).fill("RiskAnalyst!2026");
  await analyst.getByRole("button", { name: "安全登录" }).click();
  await expect(analyst.getByRole("heading", { name: "风险告警队列" })).toBeVisible();
  await expect(analyst.getByText("正在准备安全会话…")).not.toBeVisible();
  const queue = await (await analyst.request.get("/api/cases?search=UP-TR-00841")).json();
  const seed = await (await analyst.request.get(`/api/cases/${queue.items[0].id}`)).json();
  const scenarioAlertId = `BOOK-${crypto.randomUUID().slice(0, 8)}`;
  const intake = await analyst.request.post("/api/ingestion/alerts", {
    headers: { "X-Alert-Ingestion-Key": "e2e-test-key", "Idempotency-Key": crypto.randomUUID() },
    data: { externalAlertId: scenarioAlertId, sourceSystem: "Textbook Synthetic Monitor", category: seed.alert.category,
      detectedAt: seed.alert.detectedAt, severity: seed.alert.severity, riskScore: seed.alert.riskScore,
      customerId: seed.customer.id, accountId: seed.account.id, signals: seed.signals,
      transactions: seed.transactions.map((t: any) => ({ ...t, accountId: t.accountMaskedNumber === seed.account.maskedNumber ? seed.account.id : seed.graphNodes.find((n: any) => n.type === "account" && n.label === t.accountMaskedNumber).id.slice(8) })) },
  });
  expect(intake.status(), await intake.text()).toBe(201);
  const created = await intake.json();
  const session = await (await analyst.request.get("/api/session")).json();
  const csrf = await (await analyst.request.get("/api/security/antiforgery")).json();
  const assignment = await analyst.request.post(`/api/cases/${created.id}/assignment`, { headers: { "X-CSRF-TOKEN": csrf.requestToken, "If-Match": `"${created.version}"`, "Idempotency-Key": crypto.randomUUID() }, data: { analystId: session.id } });
  expect(assignment.status(), await assignment.text()).toBe(200);
  await analyst.reload();
  await expect(analyst.getByRole("link", { name: scenarioAlertId, exact: true })).toBeVisible();
  await expect(analyst.getByText("正在读取档案…")).not.toBeVisible();
  await analyst.screenshot({ path: fileURLToPath(new URL("../../manual/figures/risk-alert-queue.png", import.meta.url)), fullPage: true });
  await analyst.screenshot({ path: fileURLToPath(new URL("../../manual/figures/risk-alert-queue-viewport.png", import.meta.url)), fullPage: false });

  await analyst.getByRole("link", { name: scenarioAlertId, exact: true }).click();
  await expect(analyst.getByRole("heading", { name: /Aroha Thompson/ })).toBeVisible();
  await analyst.getByRole("tab", { name: /风险信号/ }).click();
  await expect(analyst.getByText("出现新交易对手")).toBeVisible();
  await analyst.getByRole("tab", { name: "调查事实" }).click();
  await analyst.getByRole("button", { name: "开始调查" }).click();
  await expect(analyst.getByText("调查中").first()).toBeVisible();
  await expect(analyst.getByText("调查已开始")).not.toBeVisible();
  await analyst.evaluate(() => window.scrollTo(0, 0));
  await analyst.screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-investigation-workspace.png", import.meta.url)), fullPage: true });

  const originalNote = "核对三笔转账后，发现另一个客户账户也与相同交易对手发生交易；该关系仍需解释。";
  await analyst.getByLabel("调查笔记").fill(originalNote);
  await analyst.reload();
  await expect(analyst.getByLabel("调查笔记")).toHaveValue(originalNote);

  await analyst.route("**/api/cases/*/notes", async (route) => {
    const response = await route.fetch();
    await new Promise((resolve) => setTimeout(resolve, 700));
    await route.fulfill({ response });
  });
  await analyst.getByRole("button", { name: "保存笔记" }).click();
  const newerNote = "补充核验：交易对手名称相同，但暂时没有证据证明两位客户存在关联。";
  await analyst.getByLabel("调查笔记").fill(newerNote);
  await expect(analyst.getByLabel("调查笔记")).toHaveValue(newerNote);
  await expect(analyst.getByText(originalNote)).toBeVisible();

  await analyst.getByLabel("评估依据").fill("交易频率、新交易对手和跨账户重复关系同时出现；现有事实不足以直接证明欺诈，需主管独立复核。  ");
  await analyst.getByLabel("评估风险等级").selectOption("high");
  await analyst.getByRole("button", { name: "保存风险评估" }).click();
  await expect(analyst.getByText("当前风险评估")).toBeVisible();
  await analyst.getByRole("button", { name: "提交独立复核" }).click();
  await expect(analyst.getByText("待复核").first()).toBeVisible();
  await analystContext.close();

  const supervisorContext = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
  const supervisor = await supervisorContext.newPage();
  await supervisor.goto("/");
  await supervisor.getByLabel("工作邮箱").fill("supervisor@riskcase.local");
  await supervisor.getByLabel("密码", { exact: true }).fill("RiskSupervisor!2026");
  await supervisor.getByRole("button", { name: "安全登录" }).click();
  await expect(supervisor.getByRole("heading", { name: "风险告警队列" })).toBeVisible();
  await supervisor.getByRole("link", { name: "等待复核" }).click();
  await supervisor.getByRole("link", { name: scenarioAlertId, exact: true }).click();

  await supervisor.getByRole("tab", { name: "关系图谱" }).click();
  await expect(supervisor.getByText("Mateo Chen")).toBeVisible();
  await expect(supervisor.getByText("South Ridge Services").first()).toBeVisible();
  await supervisor.evaluate(() => window.scrollTo(0, 0));
  await supervisor.screenshot({ path: fileURLToPath(new URL("../../manual/figures/risk-relationship-graph.png", import.meta.url)), fullPage: true });
  await supervisor.getByRole("tab", { name: "审计记录" }).click();
  await expect(supervisor.getByText("案件已提交复核", { exact: true })).toBeVisible();
  await expect(supervisor.getByText(/版本/).first()).toBeVisible();

  await supervisor.getByLabel("处置建议").selectOption("escalate");
  await supervisor.getByLabel("处置依据").fill("独立复核确认共享交易对手与异常频率需要进一步调查；保留原始记录并升级至欺诈调查团队。 ");
  await supervisor.getByRole("button", { name: "提交处置决定" }).click();
  await expect(supervisor.getByRole("dialog")).toBeVisible();
  await supervisor.getByRole("button", { name: "确认并记录处置" }).click();
  await expect(supervisor.getByText("已结案").first()).toBeVisible();
  await supervisor.getByRole("tab", { name: "审计记录" }).click();
  await expect(supervisor.getByText("独立处置已记录")).toBeVisible();
  await expect(supervisor.getByText("升级调查").last()).toBeVisible();
  await expect(supervisor.getByText("处置决定已记录")).not.toBeVisible();
  await supervisor.evaluate(() => window.scrollTo(0, 0));
  await supervisor.screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-audit-history.png", import.meta.url)), fullPage: true });
  await supervisorContext.close();
});
