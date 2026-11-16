using RiskCase.Application.Common.Behaviours;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using NUnit.Framework;

namespace RiskCase.Application.UnitTests;

[TestFixture]
public sealed class IdempotencyFingerprintTests
{
    private sealed record SampleCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, string Rationale) : IIdempotentRequest;
    private sealed record OtherCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, string Rationale) : IIdempotentRequest;

    [Test]
    public void Different_command_types_never_share_a_fingerprint_for_identical_fields()
    {
        var first = new SampleCommand(Guid.NewGuid().ToString(), Guid.NewGuid(), 4, "same");
        var other = new OtherCommand(first.IdempotencyKey, first.CaseId, first.ExpectedVersion, first.Rationale);
        Assert.That(IdempotencyBehaviour<SampleCommand, string>.Fingerprint(first), Is.Not.EqualTo(IdempotencyBehaviour<OtherCommand, string>.Fingerprint(other)));
    }

    [Test]
    public void Fingerprint_ignores_retry_key_but_includes_business_payload()
    {
        var caseId = Guid.Parse("b6cdaf01-0f58-4f2b-86df-411a6dcf75ae");
        var first = new SampleCommand("a3f05476-3d29-4111-bfd4-f5f65a9e4a22", caseId, 4, "核查后决定继续观察该风险档案。");
        var retry = first with { IdempotencyKey = "9912a7ab-7b7b-4ce1-b904-21f456c03a0a" };
        var changed = first with { Rationale = "重新评估后提交新的处置依据。" };

        Assert.That(IdempotencyBehaviour<SampleCommand, string>.Fingerprint(first),
            Is.EqualTo(IdempotencyBehaviour<SampleCommand, string>.Fingerprint(retry)));
        Assert.That(IdempotencyBehaviour<SampleCommand, string>.Fingerprint(first),
            Is.Not.EqualTo(IdempotencyBehaviour<SampleCommand, string>.Fingerprint(changed)));
    }

    [Test]
    public async Task Behaviour_runs_a_valid_command_through_the_idempotency_store()
    {
        var store = new DirectIdempotencyStore();
        var behavior = new IdempotencyBehaviour<SampleCommand, string>(store);
        var request = new SampleCommand(Guid.NewGuid().ToString(), Guid.NewGuid(), 2, "提交可重试的调查操作。");
        var response = await behavior.Handle(request, _ => Task.FromResult("saved"), CancellationToken.None);

        Assert.That(response, Is.EqualTo("saved"));
        Assert.That(store.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void Behaviour_rejects_a_non_uuid_write_key()
    {
        var behavior = new IdempotencyBehaviour<SampleCommand, string>(new DirectIdempotencyStore());
        var request = new SampleCommand("missing", Guid.NewGuid(), 1, "提交可重试的调查操作。");

        var error = Assert.Throws<ApplicationFault>(() => behavior.Handle(request, _ => Task.FromResult("unused"), CancellationToken.None));

        Assert.That(error!.Code, Is.EqualTo("idempotency_key_required"));
    }

    private sealed class DirectIdempotencyStore : IIdempotencyStore
    {
        public int CallCount { get; private set; }

        public Task<TResponse> ExecuteAsync<TResponse>(string key, string fingerprint, Func<Task<TResponse>> action, CancellationToken cancellationToken,
            Func<string, string, bool>? legacyReplayProof = null)
        {
            CallCount++;
            return action();
        }
    }
}
