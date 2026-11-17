# Case-detail measurement

Real PostgreSQL 17.12 and .NET SDK 10.0.300, Release build, Windows, loopback server on 55439. Requests use the ASP.NET Core TestServer with cookie authentication against real services and PostgreSQL. These are endpoint measurements in process, not TCP, TLS or production load measurements. No Docker/EF InMemory or mocked database was used.

`test/integration/RiskCase.IntegrationTests/CaseDetailBenchmark.cs` is opt-in. Set `ConnectionStrings__RiskCaseDb` to a dedicated disposable database, and `RISKCASE_BENCHMARK_OUTPUT` to an output directory. Run:

```powershell
dotnet test test/integration/RiskCase.IntegrationTests/IntegrationTests.csproj -c Release --filter FullyQualifiedName~Measure_case_detail --logger trx
```

The fixture ingests separate synthetic cases, then creates the specified table cardinalities. No performance data is added to production demo seeding. Use a dedicated database and discard it after review. The before measurement was taken with production source at `4667f194e4847e06a0764d40f0bc78be7420541c` plus test probes only. The after measurement used the Round 3 implementation. Same database server, Release configuration, authenticated actor, synthetic cardinalities, field lengths, endpoint and sampling method. UUIDs and timestamps differ. Each completed scale has 20 samples, including the first. Median is the upper middle order statistic, p95 nearest rank (19th of 20). Authentication SQL is included in endpoint totals; details-only statement counts are separate. Transaction BEGIN/COMMIT protocol messages are not counted as SQL reader commands.

| Scale (tx/signals/notes/links/assessments) | Before median / p95 ms | After median / p95 ms | Before / after detail SQL | Before / after bytes |
|---|---:|---:|---:|---:|
| Small 10/3/5/3/2 | 146.145 / 179.252 | 25.467 / 43.131 | 2 / 9 | 15,441 / 15,463 |
| Medium 100/15/50/30/10 | Cancelled at 2,000 ms; 0 completed | 36.397 / 68.281 | 1 attempted before cancellation / 9 | NOT RETURNED / 132,842 |
| Large 500/30/200/100/30 | Cancelled at 2,000 ms; 0 completed | 74.159 / 108.926 | 1 attempted before cancellation / 9 | NOT RETURNED / 614,497 |

Full baseline medium/large materialization and ANALYZE were deliberately NOT RUN beyond the cancellation budget. No before median or speedup ratio is claimed for those scales. Estimated plans (EXPLAIN without ANALYZE) are retained. The sibling product, derived from actual fixture cardinalities, is 900 / 22,500,000 / 9,000,000,000 rows; it is not a measured completed result for the two cancelled scales. Optimizer plan estimates differ and must not be confused with that algebraic product.

For the small case, actual EXPLAIN ANALYZE root output across the two detail statements was 901 rows, versus 25 across the nine optimized statements. Summed representative plan execution times were 37.552 ms versus 1.595 ms. After: medium 207 root rows / 1.617 ms; large 862 / 5.258 ms. These separate EXPLAIN observations are not endpoint latency percentiles. See JSON plans for buffers, estimates, loops, actual rows and execution time.

All optimized response collections are independently counted against fixture cardinalities; none is truncated. Payload size barely changes for small (+22 bytes for the new empty legacy-author field). Pagination and graph reduction are **not** implemented: payload, graph construction and memory still grow with the sum of retained records. More round trips (9 detail readers, 12 including authentication versus 2 and 5) may regress small requests on a high-latency database connection; that deployment condition was not measured here. REPEATABLE READ holds an MVCC snapshot for the short GET; existing write transactions take a parent share lock and may wait behind a case update.

`approximateProcessAllocationBytes` uses process-wide managed allocation deltas, so includes authentication, serialization and any concurrent runtime allocations. It is an approximation, not exclusive EF allocation. `readerExecutionMs` measures ExecuteReader completion and excludes later row streaming/materialization. Endpoint latency includes full response consumption. Memory and materialization overhead cannot be isolated perfectly from these probes; no exact EF-only metric is claimed.

The ordinary integration suite tests full collection counts and a constant nine detail reader statements at small and large scales. CI has no wall-clock latency threshold. The benchmark is explicit and is executed separately; its omission from normal discovery is intentional, not an unverified correctness test.
