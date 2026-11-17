import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

const reason = "已核验客户、合成交易与关联账户，记录当前已知事实并保留不确定性以供独立复核。";
function gate() { let release!: () => void; const promise = new Promise<void>(resolve => { release = resolve; }); return { promise, release }; }

async function login(page: Page, user = "analyst") {
  await page.goto("/");
  await page.getByLabel("工作邮箱").fill(`${user}@riskcase.local`);
  await page.getByLabel("密码", { exact: true }).fill(user === "supervisor" ? "RiskSupervisor!2026" : user === "senior" ? "RiskSenior!2026" : user === "auditor" ? "RiskAuditor!2026" : "RiskAnalyst!2026");
  await page.getByRole("button", { name: "安全登录" }).click();
  await expect(page.getByRole("heading", { name: "风险告警队列" })).toBeVisible();
}
async function change(page: Page, c: any, suffix: string, body?: object) {
  const token = await (await page.request.get("/api/security/antiforgery")).json();
  const response = await page.request.fetch(`/api/cases/${c.id}/${suffix}`, {
    method: suffix === "assessment" ? "PUT" : "POST", data: body,
    headers: { "X-CSRF-TOKEN": token.requestToken, "If-Match": `"${c.version}"`, "Idempotency-Key": crypto.randomUUID() },
  });
  expect(response.status(), await response.text()).toBe(200); return response.json();
}
async function createCase(page: Page) {
  const queue = await (await page.request.get("/api/cases?pageSize=100")).json();
  const subject = await (await page.request.get(`/api/cases/${queue.items[0].id}`)).json();
  const response = await page.request.post("/api/ingestion/alerts", {
    headers: { "X-Alert-Ingestion-Key": "e2e-test-key", "Idempotency-Key": crypto.randomUUID() },
    data: { externalAlertId: `BROWSER-${crypto.randomUUID()}`, sourceSystem: "Round2Browser", category: "transactionRisk", severity: "high", riskScore: 82,
      detectedAt: new Date().toISOString(), customerId: subject.customer.id, accountId: subject.account.id,
      signals: [{ code: "BROWSER", title: "合成风险线索", explanation: reason, severity: "high", transactionReferences: ["BROWSER-TX"] }],
      transactions: [{ externalReference: "BROWSER-TX", accountId: subject.account.id, direction: "outgoing", amount: 150, currency: subject.account.currency,
        occurredAt: new Date().toISOString(), channel: "online", counterpartyReference: "SYNTHETIC", counterpartyLabel: "Synthetic Counterparty" }] },
  });
  expect(response.status(), await response.text()).toBe(201);
  let c = await response.json();
  const session = await (await page.request.get("/api/session")).json();
  c = await change(page, c, "assignment", { analystId: session.id });
  c = await change(page, c, "start");
  return c;
}
async function open(page: Page, c: any) {
  await page.goto(`/cases/${c.id}`); await expect(page.getByLabel("调查笔记")).toBeVisible();
}

async function reviewFixture(page: Page, browser: import("@playwright/test").Browser) {
  await login(page); let c = await createCase(page);
  c = await change(page, c, "assessment", { level: "high", rationale: reason }); c = await change(page, c, "review");
  const context = await browser.newContext({ viewport: { width: 1440, height: 1050 } }); const supervisor = await context.newPage();
  await login(supervisor, "supervisor"); await supervisor.goto(`/cases/${c.id}`);
  await expect(supervisor.getByRole("button", { name: "退回调查", exact: true })).toBeVisible();
  return { c, context, supervisor };
}

test("review return requires a reason, records the audit feedback, and requires reassessment before resubmission", async ({ page, browser }) => {
  const { c, context, supervisor } = await reviewFixture(page, browser);
  const feedback = "请核验交易对手账户的实际受益所有人，并补充高频转出与客户经营活动之间的证据关系，再重新提交风险评估。";
  await supervisor.getByRole("button", { name: "退回调查", exact: true }).click();
  const dialog = supervisor.getByRole("dialog"); await expect(dialog.getByRole("button", { name: "确认退回并记录" })).toBeDisabled();
  await dialog.getByLabel("退回原因").fill("需要补充"); await expect(dialog.getByText(/当前 4 个字符/)).toBeVisible();
  await dialog.screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-return-validation.png", import.meta.url)) });
  await dialog.getByRole("button", { name: "取消", exact: true }).click();
  expect((await (await page.request.get(`/api/cases/${c.id}`)).json()).status).toBe("reviewing");
  await supervisor.getByRole("button", { name: "退回调查", exact: true }).click();
  await dialog.getByLabel("退回原因").fill(`  ${feedback}  `);
  await dialog.screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-return-dialog.png", import.meta.url)) });
  await dialog.getByRole("button", { name: "确认退回并记录" }).click(); await expect(dialog).not.toBeVisible();
  await supervisor.getByRole("tab", { name: "审计记录" }).click(); await expect(supervisor.getByTestId("return-audit-reason")).toHaveText(feedback);
  await supervisor.getByRole("tabpanel", { name: "审计记录" }).screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-return-audit.png", import.meta.url)) });
  await page.goto(`/cases/${c.id}`); await expect(page.getByLabel("调查笔记")).toBeVisible();
  await expect(page.getByRole("button", { name: "提交独立复核" })).not.toBeVisible();
  const returned = await (await page.request.get(`/api/cases/${c.id}`)).json(); expect(returned.currentAssessmentId).toBeNull();
  const csrf = await (await page.request.get("/api/security/antiforgery")).json();
  const premature = await page.request.post(`/api/cases/${c.id}/review`, { headers: { "X-CSRF-TOKEN": csrf.requestToken, "If-Match": `"${returned.version}"`, "Idempotency-Key": crypto.randomUUID() } });
  expect(premature.status()).toBe(409);
  await page.screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-returned-investigation.png", import.meta.url)), fullPage: true });
  await page.getByLabel("评估依据").fill(reason + "已补充受益所有人资料与交易频率核验结论。"); await page.getByRole("button", { name: "保存风险评估" }).click();
  await expect(page.getByRole("button", { name: "提交独立复核" })).toBeEnabled(); await page.getByRole("button", { name: "提交独立复核" }).click();
  await expect(page.getByText("待复核").first()).toBeVisible(); await page.getByRole("tab", { name: "评估历史" }).click();
  await page.getByRole("tabpanel", { name: "评估历史" }).screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-resubmitted-assessments.png", import.meta.url)) });
  const current = await (await page.request.get(`/api/cases/${c.id}`)).json(); expect(current.assessments).toHaveLength(2); expect(current.auditHistory.filter((a: any) => a.operation === "InvestigationResumed")).toHaveLength(1);
  await context.close();
});

test("return completion preserves an A to B to A reason generation typed while the response was delayed", async ({ page, browser }) => {
  const { c, context, supervisor } = await reviewFixture(page, browser); const received=gate(), release=gate();
  await supervisor.route(`**/api/cases/${c.id}/resume`, async route => { const response=await route.fetch(); received.release();await release.promise;await route.fulfill({response}); });
  await supervisor.getByRole("button", { name: "退回调查", exact: true }).click(); const field=supervisor.getByLabel("退回原因");
  await field.fill(reason);await supervisor.getByRole("button",{name:"确认退回并记录"}).click();await received.promise;
  await field.fill("较新的补充意见");await field.fill(reason);release.release();
  await expect(supervisor.getByRole("button",{name:"确认退回并记录"})).toBeDisabled();await expect(supervisor.getByText("案件已退回调查，原因已记录",{exact:true})).toBeVisible();await expect(field).toHaveValue(reason);
  await supervisor.reload();
  const persisted=await supervisor.evaluate(id=>Object.entries(localStorage).find(([key])=>key.includes(`:${id}:return-reason`)&&!key.includes(":cleared:"))?.[1],c.id);
  expect(JSON.parse(persisted!).value).toBe(reason);await context.close();
});

test("lost return response recovers the original reason and key after reload without duplicate audit", async ({ page, browser }) => {
  const { c,context,supervisor }=await reviewFixture(page,browser);let originalKey="";
  await supervisor.route(`**/api/cases/${c.id}/resume`,async route=>{originalKey=route.request().headers()["idempotency-key"];await route.fetch();await route.abort("failed");});
  await supervisor.getByRole("button",{name:"退回调查",exact:true}).click();await supervisor.getByLabel("退回原因").fill(reason);await supervisor.getByRole("button",{name:"确认退回并记录"}).click();
  await expect(supervisor.getByRole("dialog").getByRole("alert")).toBeVisible();await supervisor.reload();await supervisor.unroute(`**/api/cases/${c.id}/resume`);
  const retry=supervisor.waitForRequest(`**/api/cases/${c.id}/resume`);await supervisor.getByRole("button",{name:"恢复上次操作"}).click();expect((await retry).headers()["idempotency-key"]).toBe(originalKey);
  await expect(supervisor.getByRole("button",{name:"恢复上次操作"})).not.toBeVisible();await supervisor.getByRole("tab",{name:"审计记录"}).click();await expect(supervisor.getByTestId("return-audit-reason")).toHaveText(reason);
  const current=await(await supervisor.request.get(`/api/cases/${c.id}`)).json();expect(current.auditHistory.filter((a:any)=>a.operation==="InvestigationResumed")).toHaveLength(1);await context.close();
});

test("stale review return refreshes the case and preserves the unsaved feedback",async({page,browser})=>{
  const {c,context,supervisor}=await reviewFixture(page,browser);
  await supervisor.getByRole("button",{name:"退回调查",exact:true}).click();await supervisor.getByLabel("退回原因").fill(reason);
  await change(page,c,"resume",{reason:reason+"经办人主动撤回核验。"});
  await supervisor.getByRole("button",{name:"确认退回并记录"}).click();await expect(supervisor.getByRole("dialog").getByText("记录已被其他窗口更新")).toBeVisible();await expect(supervisor.getByLabel("退回原因")).toHaveValue(reason);
  const current=await(await supervisor.request.get(`/api/cases/${c.id}`)).json();expect(current.auditHistory.filter((a:any)=>a.operation==="InvestigationResumed")).toHaveLength(1);await context.close();
});

test("a current GET can reconcile historical metadata at the same business version",async({page,browser})=>{
  const {c,context,supervisor}=await reviewFixture(page,browser);
  await change(page,c,"resume",{reason});
  // Model a cached R2 representation: same committed business version, no historical reason.
  // Only this first response is altered; refresh below uses the real current PostgreSQL result.
  await supervisor.route(`**/api/cases/${c.id}`,async route=>{
    const response=await route.fetch();const historical=await response.json();
    historical.auditHistory.find((x:any)=>x.operation==="InvestigationResumed").detailsJson="{}";
    await route.fulfill({response,json:historical});
  },{times:1});
  await supervisor.reload();await supervisor.getByRole("tab",{name:"审计记录"}).click();
  await expect(supervisor.getByTestId("return-audit-reason")).toHaveText("历史退回记录未保存原因。");
  const version=await supervisor.getByTestId("case-version").textContent();
  await supervisor.getByRole("button",{name:"刷新案件",exact:true}).click();
  await expect(supervisor.getByTestId("return-audit-reason")).toHaveText(reason);
  await expect(supervisor.getByTestId("case-version")).toHaveText(version!);await context.close();
});

test("A → B → A edits survive an earlier successful response, and the same text can be submitted again intentionally", async ({ page }) => {
  await login(page); const c = await createCase(page); await open(page, c);
  const received = gate(), release = gate();
  await page.route(`**/api/cases/${c.id}/notes`, async route => { const response = await route.fetch(); received.release(); await release.promise; await route.fulfill({ response }); });
  await page.getByLabel("调查笔记").fill(reason);
  await page.getByRole("button", { name: "保存笔记" }).click(); await received.promise;
  await page.getByLabel("调查笔记").fill("B 是较新的调查意图"); await page.getByLabel("调查笔记").fill(reason); release.release();
  await expect(page.getByRole("button", { name: "保存笔记" })).toBeEnabled();
  await expect(page.getByLabel("调查笔记")).toHaveValue(reason);
  await page.reload(); await expect(page.getByLabel("调查笔记")).toHaveValue(reason);
  await page.unroute(`**/api/cases/${c.id}/notes`);
  await page.getByRole("button", { name: "保存笔记" }).click(); await expect(page.getByLabel("调查笔记")).toHaveValue("");
  const current = await (await page.request.get(`/api/cases/${c.id}`)).json(); expect(current.notes.filter((n: any) => n.text === reason)).toHaveLength(2);
});

test("an older delayed mutation result cannot replace a newer confirmed snapshot", async ({ page }) => {
  await login(page); const c = await createCase(page); await open(page, c);
  const received = gate(), release = gate(); let old: any;
  await page.route(`**/api/cases/${c.id}/notes`, async route => { const response = await route.fetch(); old = await response.json(); received.release(); await release.promise; await route.fulfill({ response }); });
  await page.getByLabel("调查笔记").fill(reason); await page.getByRole("button", { name: "保存笔记" }).click(); await received.promise;
  const newer = await change(page, old, "notes", { text: "这是另一窗口确认的新事实。" });
  await page.getByRole("button", { name: "刷新案件", exact: true }).click(); await expect(page.getByTestId("case-version")).toHaveText(`v${newer.version}`);
  const refreshed = gate(), releaseRefresh = gate();
  await page.route(`**/api/cases/${c.id}`, async route => { refreshed.release(); await releaseRefresh.promise; await route.continue(); });
  release.release(); await refreshed.promise;
  await expect(page.getByTestId("case-version")).toHaveText(`v${newer.version}`);
  releaseRefresh.release(); await page.unroute(`**/api/cases/${c.id}`);
});

test("a lost committed HTTP response is recoverable after reload with the original key and without duplicate notes", async ({ page }) => {
  await login(page); const c = await createCase(page); await open(page, c);
  let originalKey = "";
  await page.route(`**/api/cases/${c.id}/notes`, async route => { originalKey = route.request().headers()["idempotency-key"]; const result = await route.fetch(); expect(result.ok()).toBeTruthy(); await route.abort("failed"); });
  await page.getByLabel("调查笔记").fill(reason); await page.getByRole("button", { name: "保存笔记" }).click();
  await expect(page.getByRole("button", { name: "恢复上次操作" })).toBeVisible();
  await page.unroute(`**/api/cases/${c.id}/notes`); await page.reload();
  await expect(page.getByRole("button", { name: "恢复上次操作" })).toBeVisible();
  const retry = page.waitForRequest(request => request.url().endsWith(`/api/cases/${c.id}/notes`) && request.method() === "POST");
  await page.getByRole("button", { name: "恢复上次操作" }).click(); expect((await retry).headers()["idempotency-key"]).toBe(originalKey);
  await expect(page.getByRole("button", { name: "恢复上次操作" })).not.toBeVisible();
  const current = await (await page.request.get(`/api/cases/${c.id}`)).json(); expect(current.notes).toHaveLength(1);
  await expect(page.getByLabel("调查笔记")).toHaveValue(reason); // Recovery never discards uncertain local intent.
});

test("switching cases while a write is pending isolates both cache and draft", async ({ page }) => {
  await login(page); const first = await createCase(page), second = await createCase(page); await open(page, first);
  const received = gate(), release = gate();
  await page.route(`**/api/cases/${first.id}/notes`, async route => { const response = await route.fetch(); received.release(); await release.promise; await route.fulfill({ response }); });
  await page.getByLabel("调查笔记").fill(reason); await page.getByRole("button", { name: "保存笔记" }).click(); await received.promise;
  // A client-side navigation keeps the original request alive.
  await page.evaluate((id) => { history.pushState({}, "", `/cases/${id}`); window.dispatchEvent(new PopStateEvent("popstate")); }, second.id);
  await expect(page.getByLabel("调查笔记")).toHaveValue(""); await page.getByLabel("调查笔记").fill("第二案件的新草稿"); release.release();
  await expect(page.getByLabel("调查笔记")).toHaveValue("第二案件的新草稿"); await page.reload(); await expect(page.getByLabel("调查笔记")).toHaveValue("第二案件的新草稿");
  expect((await (await page.request.get(`/api/cases/${second.id}`)).json()).notes).toHaveLength(0);
});

test("review screens freeze investigation forms and expose the exact historical assessment", async ({ page, browser }) => {
  await login(page); let c = await createCase(page);
  c = await change(page, c, "assessment", { level: "high", rationale: reason }); c = await change(page, c, "review");
  await page.goto(`/cases/${c.id}`); await expect(page.getByText("待复核").first()).toBeVisible();
  await expect(page.getByLabel("调查笔记")).not.toBeVisible(); await page.getByRole("tab", { name: "关系图谱" }).click(); await expect(page.getByRole("button", { name: "记录关系" })).not.toBeVisible();
  await page.getByRole("tab", { name: "评估历史" }).click(); await expect(page.getByText("本次复核依据")).toBeVisible();
  const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } }); const supervisor = await context.newPage();
  await login(supervisor, "supervisor"); await supervisor.goto(`/cases/${c.id}`); await supervisor.getByRole("tab", { name: "评估历史" }).click();
  await expect(supervisor.getByText("本次复核依据")).toBeVisible();
  await supervisor.screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-assessment-review.png", import.meta.url)), fullPage: true });
  await supervisor.setViewportSize({ width: 1280, height: 900 });
  await supervisor.getByRole("tabpanel", { name: "评估历史" }).screenshot({ path: fileURLToPath(new URL("../../manual/figures/riskcase-assessment-history-detail.png", import.meta.url)) });
  await expect(supervisor.getByRole("button", { name: "提交处置决定" })).toBeVisible(); await expect(supervisor.getByRole("button", { name: "重新分配" })).not.toBeVisible();
  await context.close();
});

test("a completion in one tab never removes a newer persisted draft from another tab", async ({ page }) => {
  await login(page); const c = await createCase(page); await open(page, c);
  const received = gate(), release = gate();
  await page.route(`**/api/cases/${c.id}/notes`, async route => { const response = await route.fetch(); received.release(); await release.promise; await route.fulfill({ response }); });
  await page.getByLabel("调查笔记").fill(reason); await page.getByRole("button", { name: "保存笔记" }).click(); await received.promise;
  const second = await page.context().newPage(); await open(second, c); await second.getByLabel("调查笔记").fill("另一标签页仍在编辑的最新事实");
  release.release(); await expect(page.getByRole("button", { name: "保存笔记" })).toBeEnabled();
  await page.reload(); await expect(page.getByLabel("调查笔记")).toHaveValue("另一标签页仍在编辑的最新事实");
  await second.close();
});

test("account switching keeps drafts owned by their original identity", async ({ page }) => {
  await login(page); const c = await createCase(page); await open(page, c);
  await page.getByLabel("调查笔记").fill("分析员尚未提交的私人草稿");
  await page.getByRole("button", { name: "退出登录" }).click();
  await page.getByLabel("工作邮箱").fill("supervisor@riskcase.local"); await page.getByLabel("密码", { exact: true }).fill("RiskSupervisor!2026");
  await page.getByRole("button", { name: "安全登录" }).click();
  await expect(page.getByRole("banner").getByText("supervisor@riskcase.local")).toBeVisible();
  const session = await (await page.request.get("/api/session")).json();
  const latest = await (await page.request.get(`/api/cases/${c.id}`)).json();
  await change(page, latest, "assignment", { analystId: session.id });
  await page.getByRole("button", { name: "刷新案件", exact: true }).click();
  await expect(page.getByLabel("调查笔记")).toHaveValue("");
  await page.getByLabel("调查笔记").fill("主管独立保存的工作草稿"); await page.reload();
  await expect(page.getByLabel("调查笔记")).toHaveValue("主管独立保存的工作草稿");
});

test("repeated account handoffs keep the observed session query connected without a page reload",async({page})=>{
  await login(page);
  for(const user of ["supervisor","analyst","supervisor","analyst"]){
    await page.getByRole("button",{name:"退出登录"}).click();
    await page.getByLabel("工作邮箱").fill(`${user}@riskcase.local`);await page.getByLabel("密码",{exact:true}).fill(user==="supervisor"?"RiskSupervisor!2026":"RiskAnalyst!2026");
    await page.getByRole("button",{name:"安全登录"}).click();
    await expect(page.getByRole("banner").getByText(`${user}@riskcase.local`)).toBeVisible();
    expect((await(await page.request.get("/api/session")).json()).email).toBe(`${user}@riskcase.local`);
  }
});

test("clearing a successful generation does not overwrite a cross-tab edit interleaved after the storage check", async ({ page }) => {
  await login(page); const c = await createCase(page); await open(page, c);
  const received = gate(), release = gate();
  await page.route(`**/api/cases/${c.id}/notes`, async route => { const response = await route.fetch(); received.release(); await release.promise; await route.fulfill({ response }); });
  await page.getByLabel("调查笔记").fill(reason); await page.getByRole("button", { name: "保存笔记" }).click(); await received.promise;
  await page.evaluate((id) => {
    const key = Object.keys(localStorage).find(item => item.includes(`:${id}:note`) && !item.includes(":cleared:"))!;
    const originalGet = Storage.prototype.getItem, originalSet = Storage.prototype.setItem;
    let injected = false;
    Storage.prototype.getItem = function(name) {
      const value = originalGet.call(this, name);
      if (this === localStorage && name === key && !injected) {
        injected = true;
        originalSet.call(this, key, JSON.stringify({ key, value: "并发标签页在检查之后保存的新意图", revision: crypto.randomUUID() }));
      }
      return value;
    };
  }, c.id);
  release.release(); await expect(page.getByText("调查笔记已保存", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "保存笔记" })).toBeDisabled();
  await page.reload(); await expect(page.getByLabel("调查笔记")).toHaveValue("并发标签页在检查之后保存的新意图");
});
