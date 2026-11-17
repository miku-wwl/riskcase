BEGIN READ ONLY;
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
 'receipts',(SELECT count(*) FROM riskcase.idempotency_receipts WHERE "ResponseJson"::jsonb ->> 'Id' = '0a97f278-7df5-4a56-9112-69aad8b552a1' ),
 'receiptVersions',(SELECT json_agg(("ResponseJson"::jsonb->>'Version')::int ORDER BY ("ResponseJson"::jsonb->>'Version')::int) FROM riskcase.idempotency_receipts WHERE "ResponseJson"::jsonb ->> 'Id' = '0a97f278-7df5-4a56-9112-69aad8b552a1' )
) FROM riskcase.risk_cases c WHERE c."Id"='0a97f278-7df5-4a56-9112-69aad8b552a1';
ROLLBACK;

BEGIN READ ONLY;
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
 'receipts',(SELECT count(*) FROM riskcase.idempotency_receipts WHERE "Scope"='5c62b9a5-5f8d-496e-8b81-84ebdfe9afbc' AND "Key"='b904598e-2349-46c3-9ed9-59aad331110f' ),
 'receiptVersions',(SELECT json_agg(("ResponseJson"::jsonb->>'Version')::int ORDER BY ("ResponseJson"::jsonb->>'Version')::int) FROM riskcase.idempotency_receipts WHERE "Scope"='5c62b9a5-5f8d-496e-8b81-84ebdfe9afbc' AND "Key"='b904598e-2349-46c3-9ed9-59aad331110f' )
) FROM riskcase.risk_cases c WHERE c."Id"='0f9b7c10-5440-44ba-92e7-a32d81927668';
ROLLBACK;
