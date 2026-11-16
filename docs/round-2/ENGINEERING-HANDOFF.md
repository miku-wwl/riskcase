# RiskCase Round 2 — Engineering Handoff

## Baseline and scope

- Reviewed baseline: `4c2f9b10d2003bdb78b5b667282df2ff2a41711b`.
- Before editing, origin was verified as `git@github.com:miku-wwl/riskcase.git`, local/remote main matched the baseline, and the working tree was clean. Remote main was fetched.
- Existing Clean Architecture, .NET 10, PostgreSQL, Identity, React 19, TanStack Query, React Flow and NSwag remain in place. Work stays on main, without a PR or additional branch. No EchoCase files were changed.
- Tests use synthetic identities and banking observations. Dispositions complete this application's investigation record; downstream monitoring, escalation and AML work are not executed by RiskCase.

## Original finding reassessment

| ID | Independent classification / baseline reproduction | Root cause and impact | Remediation | Regression evidence / final status |
|---|---|---|---|---|
| F1 independent supervisor | **CONFIRMED**. Baseline PostgreSQL API sequence S investigates → assesses → submits → reassigns to A → S decides returned 200. | Independence compared the current assignee with the reviewer; responsibility could be laundered through assignment. | Immutable assessment author; review/decision bind assessment IDs; reviewing assignment forbidden. Any historical assessor on this case is permanently ineligible for final review, including after return and reassessment. Supervisors may investigate, but must then use another eligible supervisor for final review. | Domain transition matrix and `Assessor_cannot_self_review_after_reassignment_or_role_combination`, including return/reassign/reassess variant. **RESOLVED**. |
| F2 review freeze | **CONFIRMED**. Baseline domain and real API allowed facts during Reviewing. | EnsureEditable accepted Reviewing, allowing the reviewed basis to change. | Only Investigating permits notes/links/assessment. Reviewing freezes assignment as well. Explicit return invalidates current assessment and submission; later evidence/assignment changes also require reassessment. | `Review_freezes_facts_assignment_and_assessment_and_keeps_database_unchanged`, deterministic concurrent review-versus-note test, read-only browser review screen. **RESOLVED**. |
| F3 assessment auditability | **CONFIRMED**. Baseline API retained only the second rationale, with no assessment-history collection/table. | Mutable projection plus generic assessment event discarded earlier rationale and actual author. | Append-only risk_assessments with level, rationale, author, timestamp, case/basis versions. Review audit references a specific assessment; decisions have a same-case composite FK to it. EF guards and database triggers prevent update/delete of assessment, decision and audit history. | `Assessment_history_preserves_both_rationales_and_reviewed_identity` independently queries PostgreSQL; append-only SQL rejection test and upgrade migration test. **RESOLVED**, legacy loss explicitly recorded. |
| F4 replay authorization/freshness | **PARTIALLY CONFIRMED**. Real baseline replay recovered the old success after reassignment. That is intentional for a user who still has all-case read access; it did not execute a new write. Static review confirmed reliance on stale cookie roles and unconditional cache replacement. | Historical response was treated as current state; current role revocation was not checked against Identity before replay. | Live Identity role/lockout check precedes idempotency. Per-user receipt scope retained. New operations still require resource ownership/version/state. Frontend keeps the larger server version, revalidates after success, and preserves original unknown-outcome intent across reload. | Lost-response/reassignment API test, revoked-role-cookie test, same-key concurrent replay, delayed older browser response, reload recovery with the original UUID. **RESOLVED** with the historical-success semantics preserved. |
| F5 assignment targets | **CONFIRMED**. Baseline accepted nonexistent and auditor IDs and persisted assignment/audit changes. | Nonempty string validation did not establish identity or investigation eligibility. | Application uses IIdentityService to validate a current, unlocked Analyst/SeniorAnalyst/Supervisor identity. Role abstraction remains outside Domain. Invalid targets return 400 assignee_ineligible. Listing and assignment eligibility agree. | Negative assignment API tests with independent version/audit/receipt checks; valid analyst, senior and supervisor paths exercised in other scenarios. **RESOLVED**. |
| F6 duplicate relationships | **CONFIRMED**. Baseline duplicate request with a new key/current version returned 500. | Links were not loaded for the domain duplicate check. | Load persisted Links before write, normalize endpoint order, keep unique index, map its expected conflict to 409 as a final safeguard. | Same/reversed endpoint tests check no partial audit or receipt through independent reads. **RESOLVED**. |
| F7 draft ABA | **CONFIRMED by source analysis**. Baseline clear compared only strings, which cannot distinguish A/r1 from A/r3. A full baseline browser replay was not run. | Editing history and workspace identity were absent from clear conditions; hook state could survive a key change. | UUID editing generations captured in DraftSnapshot; current and persisted generation checks; keyed user/case workspaces; user-scoped cache keys and response guards. Pending transport intent retains operation identity and original server version separately. | Real browser A→B→A, intentional same-text second save, delayed response, case switch, account switch, another-tab edit and reload recovery. **RESOLVED**. |

The first three domain regressions were run before production edits and failed. The baseline API regression run failed seven cases, confirming the defects above; the replay test's baseline failure also exposed inconsistent 409 versus 403 authorization semantics. Baseline TRX files are local verification outputs under ignored `test/results/`, not reader-facing textbook content. Existing happy-path tests missed combined state, assignment and response-order conditions; test count alone was not the problem.

## Additional material findings

1. **Assessment became stale after new investigation facts.** Adding a note/link or changing investigator could leave an old assessment eligible for submission. New basis-changing actions invalidate the current projection and require a new assessment. Historical records remain intact. Domain and API regressions cover this rule.
2. **Cross-command idempotency collision.** Start, Review and Resume can serialize identical fields. Fingerprints now include the concrete command type, so a stored Start response cannot impersonate Review. Same-key/different-command API and unit tests cover this.
3. **Malformed ingestion could reach database errors or rounding.** Duplicate transaction references, duplicate signal codes, excess monetary scale/range, invalid currency format, malformed references, invalid enums, absent subjects/accounts and incorrect subject ownership are rejected before writes. Related-account observations may legitimately belong to another customer. Domain-level source identity is normalized before locking/querying; payload comparison canonicalizes unordered observations, decimal representation and UTC microsecond time. Retained legacy payloads can be compared without overwriting provenance.
4. **Cookie role revocation lag.** Resource commands/queries and analyst listing now consult current Identity eligibility; a revoked cookie cannot recover a protected response. A newly registered identity receives no business privileges. Current ownership denial uses 403; stale version uses 412.
5. **Numeric undefined filters and unstable ties.** Undefined numeric enum filters now return 400, and queue ordering has a case-ID tie breaker.
6. **Rerun-dependent tests and server reuse.** Existing workflow tests mutated seed state. They now create their own synthetic cases; Playwright owns its app services and refuses silently reusing an arbitrary listener. Local test ports can be overridden without changing application defaults.

## Enforced role/resource/action policy

| Actor | Read all cases/history | Investigate, assess, submit | Assign | Return from review | Final disposition |
|---|---|---|---|---|---|
| Anonymous | 401 | 401 | 401 | 401 | 401 |
| Registered, no business role | 403 | 403 | 403 | 403 | 403 |
| RiskAnalyst / SeniorAnalyst | Yes | Current assignee, correct state/version | Self-claim unassigned; no takeover | Current assignee | 403 |
| RiskSupervisor | Yes | Current assignee, same domain rules | Any eligible target, New/Investigating only | Any reviewing case | Reviewing, current submitted assessment, not current assignee/submitting actor/any historical assessor |
| RiskAuditor | Yes | 403 | 403 | 403 | 403 |

Cookie writes require CSRF request tokens. API headers carry UUID Idempotency-Key and quoted positive If-Match versions. 400 means invalid input/eligibility, 401 unauthenticated, 403 insufficient current authority, 404 absent case, 409 domain or key conflict, 412 stale/concurrent write, and 428 missing/invalid precondition. PostgreSQL deadlock/serialization aborts are mapped to retryable 503, with the original intent retained. All business writes and idempotency receipt writes use the same PostgreSQL transaction.

## Migration and historical semantics

Migration `AssessmentReviewIntegrity` adds assessment history, projection/submission identifiers, decision assessment reference and database append-only triggers. It preserves known old rationale/level/time as IsLegacy records with an explicitly unknown author. Earlier overwritten reasons cannot be reconstructed. Existing resolved decisions are retained with nullable legacy AssessmentId; new domain decisions require a known assessment ID. Existing Reviewing cases are returned to Investigating, advance their version, and receive LegacyReviewInvalidated audit events. New current assessments must be authored again; the current assignee is never used to fabricate historical identity.

Old idempotency receipts lacked command identity. They remain in the database but cannot safely be replayed by guessing their operation type; a fingerprint conflict prompts current-state reconciliation. This is a deliberate upgrade compatibility boundary. Existing source-alert duplicates can instead compare the retained immutable business payload, so old fingerprint representation does not force a new alert.

Fresh-schema startup and an initial-migration → populated legacy-review fixture → latest-migration path are both tested. Migration tests allocate and drop only their uniquely named disposable database. Production/test project references are one-way: tests depend on application projects; production projects do not reference test projects.

## Verification results

Local environment: .NET SDK 10.0.300, Windows, isolated PostgreSQL 17.12 listening on loopback, Chromium through Playwright 1.65.1. This uses real PostgreSQL, not EF InMemory. Docker's local API did not respond, so an official EDB PostgreSQL binary archive was used in a task-specific temporary directory; no deployment or infrastructure was introduced into RiskCase.

- Domain: **39 passed**, including the full 4-state × 8-operation transition matrix and history/independence rules.
- Application: **6 passed**, including fresh role checks and cross-command fingerprint distinction.
- PostgreSQL integration: **28 passed** in the final local run; includes independent Npgsql reads, deterministic two-reader barriers, competing writes, review-versus-facts, same-key retries, duplicate ingestion, invalid payloads, history/decision references, role revocation, CSRF, API preconditions, upgrade migration and a receipt-insert failure after business SaveChanges.
- Browser: **8 passed**, complete investigator/supervisor workflow plus ABA, reverse response ordering, commit-followed-by-lost-response and reload, case switch, tab collision, account switch and review freeze. Network barriers gate actual service responses; application production code contains no test switches.
- Frontend: NSwag generation, TypeScript checking and production Vite build passed. The committed generated SDK is checked for drift in CI.
- There is no claim that a passing test proves all possible schedules. Actual OS-level process death at COMMIT and genuine database deadlock orchestration were not exercised; the exercised rollback and lost-response windows are specifically identified above.

Final GitHub checks and final publication QA are recorded in the final chat handoff for the exact delivered main SHA. The lightweight three-job application CI runs the added regressions; it does not deploy or publish infrastructure.

## Publication and remaining boundaries

The existing Chinese textbook is revised in place under manual/. Revisions focus on intake, lifecycle, assignment, assessment/review, domain loading, authorization, EF persistence, PostgreSQL visibility, idempotency, audit history, generated contracts, React generations and adversarial test reasoning. Editable diagrams and source excerpts are regenerated. Authentic screenshots come from the real services and PostgreSQL synthetic fixtures; detailed assessment history is captured natively rather than fabricated.

Remaining product boundaries: downstream financial actions are recorded recommendations only; the application has no automated receipt retention/expiry policy, enterprise identity provisioning, or external immutable audit archive. Legacy overwritten assessment reasons are irrecoverable and explicitly marked. These are documented boundaries, not hidden claims of implementation or certification.
