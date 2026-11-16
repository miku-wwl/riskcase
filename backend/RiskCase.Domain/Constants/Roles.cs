namespace RiskCase.Domain.Constants;

public static class Roles
{
    public const string Analyst = "RiskAnalyst";
    public const string SeniorAnalyst = "SeniorAnalyst";
    public const string Supervisor = "RiskSupervisor";
    public const string Auditor = "RiskAuditor";
    public const string CaseReaders = Analyst + "," + SeniorAnalyst + "," + Supervisor + "," + Auditor;
    public const string CaseInvestigators = Analyst + "," + SeniorAnalyst + "," + Supervisor;
}
