using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NUnit.Framework;

namespace RiskCase.IntegrationTests;

public sealed partial class HardeningTests
{
    [TestCase(10,3,5,3,2)]
    [TestCase(500,30,200,100,30)]
    public async Task Detail_query_count_is_constant_and_returns_all_collections(int tx,int signals,int notes,int links,int assessments)
    {
        var c=await Intake();await SeedScale(c,tx,signals,notes,links,assessments);
        _factory.ReadProbe.Commands.Clear();_factory.ReadProbe.Capture=true;
        JsonObject read;
        try { read=await Get(_analyst,$"/api/cases/{c["id"]}"); } finally { _factory.ReadProbe.Capture=false; }
        Assert.Multiple(()=>{
            Assert.That(read["transactions"]!.AsArray().Count,Is.EqualTo(tx));Assert.That(read["signals"]!.AsArray().Count,Is.EqualTo(signals));
            Assert.That(read["notes"]!.AsArray().Count,Is.EqualTo(notes));Assert.That(read["links"]!.AsArray().Count,Is.EqualTo(links));Assert.That(read["assessments"]!.AsArray().Count,Is.EqualTo(assessments));
            Assert.That(_factory.ReadProbe.Commands.Count(x=>x.Sql.Contains("risk_cases")||x.Sql.Contains("audit_events")),Is.EqualTo(9));
        });
    }
    // Opt-in measurement, never a hardware-dependent CI timing assertion.
    [Test, Explicit("Run with --filter FullyQualifiedName~Measure_case_detail and RISKCASE_BENCHMARK_OUTPUT set.")]
    public async Task Measure_case_detail()
    {
        var output = Environment.GetEnvironmentVariable("RISKCASE_BENCHMARK_OUTPUT") ?? throw new InvalidOperationException("Choose a benchmark output directory.");
        Directory.CreateDirectory(output);
        var results = new List<object>();
        foreach (var (name, tx, signals, notes, links, assessments) in new[] {
            ("small",10,3,5,3,2), ("medium",100,15,50,30,10), ("large",500,30,200,100,30) })
        {
            var c = await Intake(); var id = Guid.Parse(c["id"]!.ToString());
            await SeedScale(c, tx, signals, notes, links, assessments);
            _factory.ReadProbe.Commands.Clear(); _factory.ReadProbe.Started.Clear(); _factory.ReadProbe.Capture = true;
            var times = new List<double>(); var allocations = new List<long>(); var payload = 0; string? failure = null;
            // The baseline medium/large JOIN is cancelled at 2 s, rather than materializing billions of rows.
            for (var i = 0; i < 20; i++)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var before = GC.GetTotalAllocatedBytes(); var watch = Stopwatch.StartNew();
                try
                {
                    using var response = await _analyst.GetAsync($"/api/cases/{id}", timeout.Token);
                    var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token); payload = bytes.Length;
                    Assert.That((int)response.StatusCode, Is.EqualTo(200), System.Text.Encoding.UTF8.GetString(bytes));
                    var view = JsonNode.Parse(bytes)!;
                    Assert.Multiple(() => {
                        Assert.That(view["transactions"]!.AsArray().Count, Is.EqualTo(tx));
                        Assert.That(view["signals"]!.AsArray().Count, Is.EqualTo(signals));
                        Assert.That(view["notes"]!.AsArray().Count, Is.EqualTo(notes));
                        Assert.That(view["links"]!.AsArray().Count, Is.EqualTo(links));
                        Assert.That(view["assessments"]!.AsArray().Count, Is.EqualTo(assessments));
                    });
                    times.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(GC.GetTotalAllocatedBytes() - before);
                }
                catch (OperationCanceledException) { failure = "CANCELLED after 2000ms budget; full materialization NOT RUN"; break; }
            }
            _factory.ReadProbe.Capture = false;
            var commands = _factory.ReadProbe.Commands.ToArray();
            var unique = _factory.ReadProbe.Started.Where(x => x.Sql.Contains("risk_cases") || x.Sql.Contains("audit_events"))
                .DistinctBy(x => x.Sql).ToArray();
            var plans = new List<string>();
            await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await connection.OpenAsync();
            foreach (var (sql, parameters) in unique)
            {
                // Full baseline analysis on small; estimated plans for the explosive cases.
                var analyze = name == "small" || !sql.Contains("risk_case_links") || !sql.Contains("risk_signals");
                await using var explain = new NpgsqlCommand($"EXPLAIN ({(analyze ? "ANALYZE, BUFFERS, " : "")}FORMAT JSON) " + sql, connection) { CommandTimeout = 10 };
                foreach (var (key, value) in parameters) explain.Parameters.AddWithValue(key, value ?? DBNull.Value);
                plans.Add((string)(await explain.ExecuteScalarAsync())!);
            }
            await File.WriteAllTextAsync(Path.Combine(output, name + "-plans.json"), "[" + string.Join(",", plans) + "]");
            await File.WriteAllTextAsync(Path.Combine(output, name + "-sql.txt"), string.Join("\n\n", unique.Select(x => x.Sql)));
            var ordered = times.Order().ToArray();
            results.Add(new { name, transactions=tx, signals, notes, links, assessments,
                theoreticalSingleJoinRows=(long)tx*signals*notes*links*assessments,
                samples=times.Count, elapsedMs=times, medianMs=ordered.Length>0 ? ordered[ordered.Length/2] : (double?)null,
                p95Ms=ordered.Length>=20 ? ordered[(int)Math.Ceiling(ordered.Length*.95)-1] : (double?)null,
                bytes=payload, approximateProcessAllocationBytes=allocations, detailSqlStatements=unique.Length, sqlPerSuccessfulRequest=times.Count>0 ? commands.Length/times.Count : (int?)null,
                readerExecutionMs=commands.Select(x=>x.Milliseconds), failure });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "measurements.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented=true }));
    }

    private static async Task SeedScale(JsonObject c, int tx, int signals, int notes, int links, int assessments)
    {
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            DELETE FROM riskcase.risk_signals WHERE "AlertId"=@alert;
            DELETE FROM riskcase.risk_transactions WHERE "AlertId"=@alert;
            INSERT INTO riskcase.risk_transactions ("Id","AlertId","AccountId","ExternalReference","Direction","Amount","Currency","OccurredAt","Channel","CounterpartyReference","CounterpartyLabel","Created","LastModified")
              SELECT gen_random_uuid(),@alert,@account,'SCALE-TX-'||i,'Outgoing',120,'NZD',now(),'online','CP-'||i,'Synthetic Counterparty '||i,now(),now() FROM generate_series(1,@tx) i;
            INSERT INTO riskcase.risk_signals ("Id","AlertId","Code","Title","Explanation","Severity","TransactionReferences")
              SELECT gen_random_uuid(),@alert,'SCALE-'||i,'Synthetic signal '||i,'Synthetic signal with explicit test-only evidence.','High',ARRAY['SCALE-TX-1'] FROM generate_series(1,@signals) i;
            INSERT INTO riskcase.risk_case_notes ("Id","CaseId","AnalystId","Text","OccurredAt","CaseVersion")
              SELECT gen_random_uuid(),@id,'scale-analyst','Synthetic investigation note '||i,now(),10+i FROM generate_series(1,@notes) i;
            INSERT INTO riskcase.risk_case_links ("Id","CaseId","SourceNodeId","TargetNodeId","Kind","Rationale","AnalystId","OccurredAt","CaseVersion")
              SELECT gen_random_uuid(),@id,'account:'||CAST(@account AS text),'counterparty:CP-'||i,'Related','Synthetic link rationale','scale-analyst',now(),500+i FROM generate_series(1,@links) i;
            INSERT INTO riskcase.risk_assessments ("Id","CaseId","Level","Rationale","AssessorId","AssessedAt","CaseVersion","BasisVersion","IsLegacy")
              SELECT gen_random_uuid(),@id,'High','Synthetic immutable assessment '||i,'scale-analyst',now(),1000+i,900,false FROM generate_series(1,@assessments) i;
            UPDATE riskcase.risk_cases SET "Version"=2000 WHERE "Id"=@id;
            ANALYZE riskcase.risk_transactions; ANALYZE riskcase.risk_signals; ANALYZE riskcase.risk_case_notes;
            ANALYZE riskcase.risk_case_links; ANALYZE riskcase.risk_assessments;
            """, connection);
        command.Parameters.AddWithValue("id",Guid.Parse(c["id"]!.ToString())); command.Parameters.AddWithValue("alert",Guid.Parse(c["alert"]!["id"]!.ToString())); command.Parameters.AddWithValue("account",Guid.Parse(c["account"]!["id"]!.ToString()));
        command.Parameters.AddWithValue("tx",tx); command.Parameters.AddWithValue("signals",signals); command.Parameters.AddWithValue("notes",notes); command.Parameters.AddWithValue("links",links); command.Parameters.AddWithValue("assessments",assessments);
        await command.ExecuteNonQueryAsync();
    }
}
