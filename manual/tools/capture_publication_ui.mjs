// Authentic publication captures. Uses the existing Playwright installation; no app/test edits.
import { chromium, expect } from "../../test/e2e/node_modules/@playwright/test/index.mjs";
import { mkdir, writeFile, readFile } from "node:fs/promises";
import { createHash, randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
import assert from "node:assert/strict";

const baseURL = process.env.RISKCASE_WEB_URL ?? "http://127.0.0.1:15173";
const directory = fileURLToPath(new URL("../figures/round4/", import.meta.url));
await mkdir(`${directory}/raw`, { recursive: true });
const browser = await chromium.launch({ headless: true });
const manifest = { engineeringSha: "2177004a39a534ee1796b8391a590763cce62fc2", capturedAt: new Date().toISOString(), baseURL, synthetic: true, images: [], observations: [] };
const contexts = [];

async function login(kind) {
  const context = await browser.newContext({ baseURL, viewport: { width: 1440, height: 1050 }, deviceScaleFactor: 2 });
  contexts.push(context);
  const page = await context.newPage();
  await page.goto("/");
  await page.getByLabel("工作邮箱").fill(`${kind}@riskcase.local`);
  await page.getByLabel("密码", { exact: true }).fill(`Risk${kind === "analyst" ? "Analyst" : kind === "senior" ? "Senior" : "Supervisor"}!2026`);
  await page.getByRole("button", { name: "安全登录" }).click();
  await expect(page.getByRole("heading", { name: "风险告警队列", exact: true })).toBeVisible();
  await expect(page.getByText("正在准备安全会话…")).not.toBeVisible();
  return page;
}

async function json(response) {
  assert(response.ok(), `${response.status()} ${await response.text()}`);
  return response.json();
}
async function get(page, id) { return json(await page.request.get(`/api/cases/${id}`)); }
async function change(page, current, operation, data) {
  const csrf = await json(await page.request.get("/api/security/antiforgery"));
  const options = { headers: { "X-CSRF-TOKEN": csrf.requestToken, "Idempotency-Key": randomUUID(), "If-Match": `"${current.version}"` }, data };
  return json(await page.request[operation === "assessment" ? "put" : "post"](`/api/cases/${current.id}/${operation}`, options));
}
async function ingest(page, seed, suffix) {
  return json(await page.request.post("/api/ingestion/alerts", {
    headers: { "X-Alert-Ingestion-Key": "round4-publication-key", "Idempotency-Key": randomUUID() },
    data: { externalAlertId: `PUB-R4-${suffix}-${randomUUID().slice(0, 6)}`, sourceSystem: "Textbook Synthetic Monitor", category: seed.alert.category,
      detectedAt: new Date(Math.max(...seed.transactions.map(t => new Date(t.occurredAt).getTime())) + 60_000).toISOString(), severity: seed.alert.severity, riskScore: seed.alert.riskScore,
      customerId: seed.customer.id, accountId: seed.account.id, signals: seed.signals,
      transactions: seed.transactions.map(t => ({ ...t, accountId: t.accountMaskedNumber === seed.account.maskedNumber ? seed.account.id : seed.graphNodes.find(n => n.type === "account" && n.label === t.accountMaskedNumber).id.slice(8) })) },
  }));
}
async function capture(page, name, locator, purpose, clip) {
  await expect(page.locator("[data-testid='case-version']")).toBeVisible().catch(() => {});
  await page.screenshot({ path: `${directory}/raw/${name}.png`, fullPage: true });
  if (clip) await page.screenshot({ path: `${directory}/${name}.png`, clip });
  else await locator.screenshot({ path: `${directory}/${name}.png` });
  const bytes = await readFile(`${directory}/${name}.png`);
  const rawBytes = await readFile(`${directory}/raw/${name}.png`);
  manifest.images.push({ file: `round4/${name}.png`, raw: `round4/raw/${name}.png`, sha256: createHash("sha256").update(bytes).digest("hex"), raw_sha256: createHash("sha256").update(rawBytes).digest("hex"), purpose, caseVersion: await page.getByTestId("case-version").textContent().catch(() => null), url: page.url() });
}
async function version(page, number) { await expect(page.getByTestId("case-version")).toHaveText(`v${number}`); }

try {
  const analyst = await login("analyst");
  const supervisor = await login("supervisor");
  const queue = await json(await analyst.request.get("/api/cases?search=UP-TR-00841"));
  const seed = await get(analyst, queue.items[0].id);
  const user = await json(await analyst.request.get("/api/session"));
  const senior = await login("senior");
  const seniorUser = await json(await senior.request.get("/api/session"));
  let current = await ingest(analyst, seed, "WORKFLOW");
  current = await change(analyst, current, "assignment", { analystId: user.id });
  const workflowId = current.id;
  await analyst.goto(`/cases/${workflowId}`);
  await version(analyst, 2);
  await analyst.getByRole("button", { name: "开始调查", exact: true }).click();
  await version(analyst, 3);
  await capture(analyst, "investigation-facts", analyst.getByRole("tabpanel"), "Customer/account and observed transaction facts at v3");
  await analyst.getByLabel("调查笔记", { exact: true }).fill("核对四条合成交易：Aroha 与 Mateo 的账户均出现 CP-845；共享对手方仍不足以证明共同控制，需核实经营背景。");
  await analyst.getByRole("button", { name: "保存笔记", exact: true }).click();
  await version(analyst, 4);
  await analyst.getByLabel("评估依据", { exact: true }).fill("已核对交易时间、引用与账户来源；短时转出及共享新对手方需要补查经营背景，现有资料不构成欺诈或共同控制的证明。");
  await analyst.getByLabel("评估风险等级", { exact: true }).selectOption("high");
  await analyst.getByRole("button", { name: "保存风险评估", exact: true }).click();
  await version(analyst, 5);
  await analyst.getByRole("button", { name: "提交独立复核", exact: true }).click();
  await version(analyst, 6);
  assert.equal(await analyst.getByLabel("调查笔记", { exact: true }).count(), 0);
  await capture(analyst, "review-readonly", analyst.locator("main aside"), "Reviewing freezes investigation forms on server-aligned UI");

  await supervisor.goto(`/cases/${workflowId}`);
  await version(supervisor, 6);
  await supervisor.getByRole("tab", { name: "关系图谱", exact: true }).click();
  const graph = supervisor.locator(".react-flow");
  await expect(graph.locator(".react-flow__node")).toHaveCount(seed.graphNodes.length);
  await capture(supervisor, "graph-overview", graph, "Native React Flow overview before any pan/zoom");
  const nodes = graph.locator(".react-flow__node");
  const txAndCounterparty = await nodes.evaluateAll(items => items.filter(item => /交易|交易对手/.test(item.textContent)).map(item => { const b = item.getBoundingClientRect(); return { x: b.x, y: b.y, right: b.right, bottom: b.bottom }; }));
  const x = Math.min(...txAndCounterparty.map(b => b.x)) - 8;
  const y = Math.min(...txAndCounterparty.map(b => b.y)) - 8;
  const right = Math.max(...txAndCounterparty.map(b => b.right)) + 8;
  const bottom = Math.max(...txAndCounterparty.map(b => b.bottom)) + 8;
  await capture(supervisor, "graph-transactions", graph, "Native crop: transaction nodes connect to retained counterparty references", { x, y, width: right - x, height: bottom - y });
  const pair = await nodes.evaluateAll(items => items.filter(item => /客户|账户/.test(item.textContent)).map(item => { const b = item.getBoundingClientRect(); return { x: b.x, y: b.y, right: b.right, bottom: b.bottom }; }));
  const px = Math.min(...pair.map(b => b.x)) - 8, py = Math.min(...pair.map(b => b.y)) - 8;
  await capture(supervisor, "graph-ownership", graph, "Native crop: actual customer/account nodes and ownership edges", { x: px, y: py, width: Math.max(...pair.map(b => b.right)) + 8 - px, height: Math.max(...pair.map(b => b.bottom)) + 8 - py });

  await supervisor.getByRole("button", { name: "退回调查", exact: true }).click();
  const reason = "请核验交易对手账户的实际受益所有人，并补充高频转出与客户经营活动之间的证据关系，再重新提交风险评估。";
  await supervisor.getByLabel("退回原因", { exact: true }).fill(reason);
  await capture(supervisor, "return-dialog", supervisor.getByRole("dialog"), "Genuine return reason confirmation, before submission");
  await supervisor.getByRole("button", { name: "确认退回并记录", exact: true }).click();
  await version(supervisor, 7);
  await supervisor.getByRole("tab", { name: "审计记录", exact: true }).click();
  await expect(supervisor.getByText(reason, { exact: true })).toBeVisible();
  await capture(supervisor, "return-audit", supervisor.getByRole("tabpanel"), "Accepted return reason, real actor/time/version at v7");
  await analyst.reload();
  await version(analyst, 7);
  await analyst.getByLabel("调查笔记", { exact: true }).fill("补充核验：对手方属于共同商户的可能性尚未排除；已检查观察交易引用，但仍需外部受益所有权资料确认。");
  await analyst.getByRole("button", { name: "保存笔记", exact: true }).click();
  await version(analyst, 8);
  await analyst.getByLabel("评估风险等级", { exact: true }).selectOption("medium");
  await analyst.getByLabel("评估依据", { exact: true }).fill("新增核验未证实共同控制，风险由高调整为中；保留历史高风险理由，仍需主管复核外部受益所有权与经营背景的不确定性。");
  await analyst.getByRole("button", { name: "保存风险评估", exact: true }).click();
  await version(analyst, 9);
  await analyst.getByRole("button", { name: "提交独立复核", exact: true }).click();
  await version(analyst, 10);
  await supervisor.reload();
  await version(supervisor, 10);
  await supervisor.getByRole("tab", { name: "评估历史", exact: true }).click();
  await expect(supervisor.getByText("本次复核依据", { exact: true })).toBeVisible();
  await capture(supervisor, "assessment-history", supervisor.getByRole("tabpanel"), "Both High v5/Basis v4 and Medium v9/Basis v8 retained; reviewed assessment is A2");
  await supervisor.getByLabel("处置建议", { exact: true }).selectOption("escalate");
  await supervisor.getByLabel("处置依据", { exact: true }).fill("独立复核已核对两次评估及退回反馈；仍需外部资料确认受益所有权，记录升级调查供下游人工团队后续处理，不表示外部已完成行动。");
  await supervisor.getByRole("button", { name: "提交处置决定", exact: true }).click();
  await supervisor.getByRole("button", { name: "确认并记录处置", exact: true }).click();
  await version(supervisor, 11);
  await capture(supervisor, "final-decision", supervisor.locator("main aside"), "Independent supervisor's final decision and downstream boundary at v11");
  manifest.observations.push({ scenario: "return/reassess/resubmit/independent-decision", caseId: workflowId, versions: [1,2,3,4,5,6,7,8,9,10,11], final: await get(supervisor, workflowId) });

  // Capture a truly committed operation followed by an intentionally lost browser response.
  let ambiguous = await ingest(analyst, seed, "UNKNOWN");
  ambiguous = await change(analyst, ambiguous, "assignment", { analystId: user.id });
  ambiguous = await change(analyst, ambiguous, "start");
  ambiguous = await change(analyst, ambiguous, "notes", { text: "评估前已核对合成交易、来源与账户；剩余外部资料缺口明确保留，等待独立复核。" });
  await analyst.goto(`/cases/${ambiguous.id}`);
  await version(analyst, 4);
  const assessmentReason = "短时转出和共享对手方仍需核验经营用途，保留资料缺口及反证，提交高风险评估供独立主管复核。";
  await analyst.getByLabel("评估依据", { exact: true }).fill(assessmentReason);
  await analyst.getByLabel("评估风险等级", { exact: true }).selectOption("high");
  let committed;
  let releaseCommit;
  const commitObserved = new Promise(resolve => { releaseCommit = resolve; });
  await analyst.route(`**/api/cases/${ambiguous.id}/assessment`, async route => {
    const response = await route.fetch();
    committed = await response.json();
    assert.equal(response.status(), 200);
    releaseCommit();
    await route.abort("failed");
  });
  await analyst.getByRole("button", { name: "保存风险评估", exact: true }).click();
  await commitObserved;
  assert.equal(committed.version, 5);
  await expect(analyst.getByRole("button", { name: "恢复上次操作", exact: true })).toBeVisible();
  await analyst.setViewportSize({ width: 1024, height: 1050 });
  await capture(analyst, "unknown-pending", analyst.getByRole("status").filter({ hasText: "上次写入结果尚未确认" }), "Real server commit followed by route abort; pending intent remains");
  await analyst.setViewportSize({ width: 1440, height: 1050 });
  const originalPending = await analyst.evaluate(() => Object.keys(sessionStorage).filter(key => key.startsWith("riskcase:pending:")).map(key => JSON.parse(sessionStorage.getItem(key))));
  const reassigned = await change(supervisor, committed, "assignment", { analystId: seniorUser.id });
  assert.equal(reassigned.version, 6);
  await analyst.unroute(`**/api/cases/${ambiguous.id}/assessment`);
  await analyst.reload();
  await version(analyst, 6);
  const replies = [];
  analyst.on("response", async response => { if (response.url().endsWith(`/api/cases/${ambiguous.id}/assessment`)) replies.push(await response.json()); });
  await analyst.getByRole("button", { name: "恢复上次操作", exact: true }).click();
  await expect(analyst.getByRole("button", { name: "恢复上次操作", exact: true })).not.toBeVisible();
  await version(analyst, 6);
  const recovered = await get(analyst, ambiguous.id);
  assert.equal(recovered.assessments.length, 1);
  assert.equal(recovered.auditHistory.filter(a => a.operation === "AssessmentRecorded").length, 1);
  assert.equal(recovered.currentAssessmentId, null);
  await analyst.getByRole("tab", { name: "评估历史", exact: true }).click();
  await capture(analyst, "unknown-recovered", analyst.getByRole("tabpanel"), "Recovery replays v5 while current UI remains v6; one historical assessment");
  manifest.observations.push({ scenario: "assessment-commit/lost-response/reassignment/reload/replay", caseId: ambiguous.id, originalPending, historicalResponseVersion: replies[0]?.version, currentVersion: recovered.version, assessmentCount: recovered.assessments.length, assessmentAuditCount: recovered.auditHistory.filter(a => a.operation === "AssessmentRecorded").length, currentAssessmentId: recovered.currentAssessmentId });

  await analyst.setViewportSize({ width: 760, height: 960 });
  await analyst.goto("/");
  await analyst.getByRole("textbox", { name: "搜索案件", exact: true }).fill("PUB-R4");
  const card = analyst.locator(`a[href='/cases/${ambiguous.id}']:visible`);
  await expect(card).toBeVisible();
  await capture(analyst, "queue-card", card, "Genuine compact queue card: identity, severity, state and ownership at readable size");
  await writeFile(`${directory}/capture-manifest.json`, JSON.stringify(manifest, null, 2) + "\n");
  console.log(`Captured ${manifest.images.length} authentic UI regions; ${manifest.observations.length} interpreted workflows.`);
} finally {
  for (const context of contexts) await context.close();
  await browser.close();
}
