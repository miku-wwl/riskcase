"""Independently read the screenshot fixtures with psql, outside the application/EF context."""
import json
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from uuid import UUID

root = Path(__file__).resolve().parents[2]
manifest = json.loads((root / "manual/figures/round4/capture-manifest.json").read_text(encoding="utf-8"))
psql = Path(os.environ["TEMP"]) / "riskcase-round2-postgres/pgsql/bin/psql.exe"
args = [str(psql), "-h", "127.0.0.1", "-p", "55439", "-U", "postgres", "-d", "riskcase_round4_publication", "-X", "-qAt", "-v", "ON_ERROR_STOP=1"]
results = []
queries = []
for observation in manifest["observations"]:
    case_id = str(UUID(observation["caseId"]))
    pending = observation.get("originalPending", [])
    if pending:
        key = str(UUID(pending[0]["key"]))
        scope = str(UUID(pending[0]["userId"]))
        receipt = f'''"Scope"='{scope}' AND "Key"='{key}' '''
    else:
        receipt = f'''"ResponseJson"::jsonb ->> 'Id' = '{case_id}' '''
    sql = f'''BEGIN READ ONLY;
SELECT json_build_object(
 'caseId',c."Id", 'caseVersion',c."Version", 'status',c."Status",
 'currentAssessmentId',c."CurrentAssessmentId", 'reviewedAssessmentId',c."ReviewedAssessmentId",
 'assessments',(SELECT count(*) FROM riskcase.risk_assessments WHERE "CaseId"=c."Id"),
 'assessmentAudits',(SELECT count(*) FROM riskcase.audit_events WHERE "CaseId"=c."Id" AND "Operation"='AssessmentRecorded'),
 'assessmentVersions',(SELECT json_agg("CaseVersion" ORDER BY "CaseVersion") FROM riskcase.risk_assessments WHERE "CaseId"=c."Id"),
 'basisVersions',(SELECT json_agg("BasisVersion" ORDER BY "CaseVersion") FROM riskcase.risk_assessments WHERE "CaseId"=c."Id"),
 'auditVersions',(SELECT json_agg("CaseVersion" ORDER BY "CaseVersion") FROM riskcase.audit_events WHERE "CaseId"=c."Id"),
 'decisions',(SELECT count(*) FROM riskcase.risk_decisions WHERE "CaseId"=c."Id"),
 'decisionAssessmentId',(SELECT "AssessmentId" FROM riskcase.risk_decisions WHERE "CaseId"=c."Id" LIMIT 1),
 'receipts',(SELECT count(*) FROM riskcase.idempotency_receipts WHERE {receipt}),
 'receiptVersions',(SELECT json_agg(("ResponseJson"::jsonb->>'Version')::int ORDER BY ("ResponseJson"::jsonb->>'Version')::int) FROM riskcase.idempotency_receipts WHERE {receipt})
) FROM riskcase.risk_cases c WHERE c."Id"='{case_id}';
ROLLBACK;
'''
    query = subprocess.run(args, input=sql, text=True, capture_output=True, check=True)
    fact = json.loads(query.stdout.strip())
    if pending:
        assert (fact["caseVersion"], fact["assessments"], fact["assessmentAudits"], fact["receipts"], fact["receiptVersions"]) == (6, 1, 1, 1, [5]), fact
        assert fact["currentAssessmentId"] is None, fact
    else:
        assert (fact["caseVersion"], fact["assessments"], fact["assessmentAudits"], fact["decisions"]) == (11, 2, 2, 1), fact
        assert fact["assessmentVersions"] == [5, 9] and fact["basisVersions"] == [4, 8], fact
        assert fact["auditVersions"] == list(range(1, 12)), fact
        assert fact["decisionAssessmentId"] == fact["reviewedAssessmentId"] == fact["currentAssessmentId"], fact
    results.append({"scenario": observation["scenario"], "facts": fact})
    queries.append(sql)
reference = root / "manual/reference"
(reference / "round4-runtime-reads.sql").write_text("\n".join(queries), encoding="utf-8")
(reference / "round4-runtime-verification.json").write_text(json.dumps({"engineering_sha": manifest["engineeringSha"], "verified_at": datetime.now(timezone.utc).isoformat(), "method": "independent psql READ ONLY transaction", "database": "riskcase_round4_publication", "results": results}, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print("Verified both publication workflows by independent read-only PostgreSQL queries.")
